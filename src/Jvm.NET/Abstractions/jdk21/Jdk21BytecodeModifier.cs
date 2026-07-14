using System.Runtime.InteropServices;
using Jvm.NET.Interop;
using Jvm.NET.Interop.Jvmti;

namespace Jvm.NET.Abstractions.Jdk21;

/// <summary>
/// JDK 21 specific <see cref="IBytecodeModifier"/>. Backed by the JVMTI
/// <c>ClassFileLoadHook</c> event and the <c>RetransformClasses</c> /
/// <c>RedefineClasses</c> capabilities.
///
/// 回调路由：JVMTI ClassFileLoadHook -> OnClassFileLoadHook (trampoline) ->
///   遍历 _transformers 调用 IBytecodeTransformer.Transform -> 用 jvmti->Allocate
///   分配新内存并把指针回写给 JVMTI。
///
/// 线程安全：_transformers 用锁保护；回调中先快照再调用，避免长时间持锁。
/// </summary>
internal sealed unsafe class Jdk21BytecodeModifier : IBytecodeModifier, IDisposable
{
    private readonly IntPtr _jvmtiEnv;
    private readonly JvmtiEventHub _hub;
    private readonly List<IBytecodeTransformer> _transformers = new();

    // 委托必须作为字段保持活着；hub 也会钉住它，但字段让代码更直观。
    private readonly ClassFileLoadHookDelegate _classFileLoadHookDelegate;
    private bool _hookEnabled;
    private bool _disposed;

    public Jdk21BytecodeModifier(IntPtr jvmtiEnv, IntPtr jniEnv, JvmtiEventHub hub)
    {
        if (jvmtiEnv == IntPtr.Zero) throw new ArgumentNullException(nameof(jvmtiEnv));
        if (hub is null) throw new ArgumentNullException(nameof(hub));
        _jvmtiEnv = jvmtiEnv;
        _hub = hub;
        // jniEnv 参数保留以备未来需要（当前 ClassFileLoadHook 不直接用它做高级操作）。
        _ = jniEnv;

        _classFileLoadHookDelegate = OnClassFileLoadHook;
        _hub.SetCallback(ref _hub.Callbacks.ClassFileLoadHook, _classFileLoadHookDelegate);
    }

    /// <summary>在 hub.Apply() 之后由 Runtime 调用，开启 ClassFileLoadHook 通知。</summary>
    internal void Activate()
    {
        if (_hookEnabled) return;
        _hub.EnableEvent(NativeConstants.JVMTI_EVENT_CLASS_FILE_LOAD_HOOK);
        _hookEnabled = true;
    }

    public IDisposable RegisterTransformer(IBytecodeTransformer transformer)
    {
        ThrowIfDisposed();
        if (transformer is null) throw new ArgumentNullException(nameof(transformer));
        lock (_transformers)
            _transformers.Add(transformer);
        return new TransformerRegistration(this, transformer);
    }

    public void RetransformClasses(IEnumerable<JvmClass> classes)
    {
        ThrowIfDisposed();
        if (classes is null) throw new ArgumentNullException(nameof(classes));

        var arr = classes.ToArray();
        if (arr.Length == 0) return;

        var handles = stackalloc IntPtr[arr.Length];
        for (int i = 0; i < arr.Length; i++)
            handles[i] = arr[i].Handle;

        ref var jvmti = ref _hub.Interface;
        int rc = jvmti.RetransformClasses(_jvmtiEnv, arr.Length, handles);
        if (rc != NativeConstants.JNI_OK)
            throw new InvalidOperationException($"jvmtiEnv->RetransformClasses returned {rc}.");
    }

    public void RedefineClasses(IEnumerable<KeyValuePair<JvmClass, byte[]>> redefinitions)
    {
        ThrowIfDisposed();
        if (redefinitions is null) throw new ArgumentNullException(nameof(redefinitions));

        var arr = redefinitions.ToArray();
        if (arr.Length == 0) return;

        var defs = stackalloc JvmtiClassDefinition[arr.Length];
        var pins = new GCHandle[arr.Length];
        try
        {
            for (int i = 0; i < arr.Length; i++)
            {
                var bytes = arr[i].Value ?? throw new ArgumentException(
                    $"Class bytes for '{arr[i].Key.Name}' must not be null.", nameof(redefinitions));
                pins[i] = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                defs[i] = new JvmtiClassDefinition
                {
                    Klass = arr[i].Key.Handle,
                    ClassByteCount = bytes.Length,
                    ClassBytes = pins[i].AddrOfPinnedObject(),
                };
            }

            ref var jvmti = ref _hub.Interface;
            int rc = jvmti.RedefineClasses(_jvmtiEnv, arr.Length, defs);
            if (rc != NativeConstants.JNI_OK)
                throw new InvalidOperationException($"jvmtiEnv->RedefineClasses returned {rc}.");
        }
        finally
        {
            for (int i = 0; i < pins.Length; i++)
                if (pins[i].IsAllocated) pins[i].Free();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_hookEnabled)
        {
            try { _hub.DisableEvent(NativeConstants.JVMTI_EVENT_CLASS_FILE_LOAD_HOOK); }
            catch { /* Dispose 路径静默 */ }
        }
        lock (_transformers)
            _transformers.Clear();
    }

    // ---- ClassFileLoadHook trampoline ----

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ClassFileLoadHookDelegate(
        IntPtr jvmtiEnv,
        IntPtr jniEnv,
        IntPtr classBeingRedefined,
        IntPtr loader,
        byte* name,
        IntPtr protectionDomain,
        int classDataLen,
        byte* classData,
        int* newClassDataLen,
        byte** newClassData);

    private void OnClassFileLoadHook(
        IntPtr jvmtiEnv,
        IntPtr jniEnv,
        IntPtr classBeingRedefined,
        IntPtr loader,
        byte* name,
        IntPtr protectionDomain,
        int classDataLen,
        byte* classData,
        int* newClassDataLen,
        byte** newClassData)
    {
        // JVMTI 在此回调中已 attach 当前线程并提供了 jniEnv，无需再 AttachCurrentThread。
        // 注意：若 newClassData 为 null 或 *newClassData 为 null，表示 JVM 还没分配替换缓冲。
        if (newClassDataLen is null || newClassData is null)
            return;
        if (classDataLen <= 0 || classData is null)
            return;

        IBytecodeTransformer[] snapshot;
        lock (_transformers)
            snapshot = _transformers.ToArray();
        if (snapshot.Length == 0)
            return;

        var className = name == null
            ? string.Empty
            : (Marshal.PtrToStringUTF8((IntPtr)name) ?? string.Empty);

        var currentBytes = new byte[classDataLen];
        Marshal.Copy((IntPtr)classData, currentBytes, 0, classDataLen);

        byte[]? result = null;
        foreach (var t in snapshot)
        {
            try
            {
                var transformed = t.Transform(className, result ?? currentBytes);
                if (transformed != null)
                    result = transformed;
            }
            catch
            {
                // 单个 transformer 失败不应影响其他 transformer 或 JVM 类加载。
                // 生产环境应写入 error.log，这里静默以保持类加载路径不中断。
            }
        }

        if (result is null)
            return;

        // 用 JVMTI Allocate 分配缓冲——JVMTI 会负责释放（按文档）。
        ref var jvmti = ref _hub.Interface;
        IntPtr pMem = IntPtr.Zero;
        int rc = jvmti.Allocate(_jvmtiEnv, result.LongLength, &pMem);
        if (rc != NativeConstants.JNI_OK || pMem == IntPtr.Zero)
            return;

        Marshal.Copy(result, 0, pMem, result.Length);
        *newClassDataLen = result.Length;
        *newClassData = (byte*)pMem;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Jdk21BytecodeModifier));
    }

    private sealed class TransformerRegistration : IDisposable
    {
        private readonly Jdk21BytecodeModifier _owner;
        private readonly IBytecodeTransformer _transformer;
        private bool _unregistered;

        public TransformerRegistration(Jdk21BytecodeModifier owner, IBytecodeTransformer transformer)
        {
            _owner = owner;
            _transformer = transformer;
        }

        public void Dispose()
        {
            if (_unregistered) return;
            _unregistered = true;
            lock (_owner._transformers)
                _owner._transformers.Remove(_transformer);
        }
    }
}
