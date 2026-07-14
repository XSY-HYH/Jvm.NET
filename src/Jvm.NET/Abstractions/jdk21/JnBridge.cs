using System.Runtime.InteropServices;
using Jvm.NET.Interop;
using Jvm.NET.Interop.Jni;

namespace Jvm.NET.Abstractions.Jdk21;

/// <summary>
/// JNI native method descriptor,对应 JVMS 的 JNINativeMethod 结构。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JNINativeMethod
{
    public IntPtr Name;      // char* name
    public IntPtr Signature; // char* signature
    public IntPtr FnPtr;     // void* fnPtr
}

/// <summary>
/// 方案 B 的核心：在 JVM 侧注入一个桥接类 <c>com.xsy.jn.JnBridge</c>，
/// 该类包含两个 static native 方法 <c>onMethodEntry</c> / <c>onMethodExit</c>。
/// 字节码插桩时，在目标方法入口/出口插入 <c>invokestatic JnBridge.onMethod*</c>，
/// JVM 执行到此处会回调到 .NET 侧的 handler 列表，从而模拟 JVMTI MethodEntry/Exit 事件。
///
/// 生命周期：由 Jdk21Runtime 在 Start() 中创建并 Initialize()，在 Shutdown() 中 Dispose()。
/// </summary>
internal sealed unsafe class JnBridge : IDisposable
{
    /// <summary>JnBridge 在 JVM 中的内部类名（斜杠分隔）。</summary>
    public const string BridgeClassInternalName = "com/xsy/jn/JnBridge";

    private const string MethodCallbackDescriptor = "(Ljava/lang/String;Ljava/lang/String;)V";
    private const string ExceptionCallbackDescriptor = "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Throwable;)V";

    private readonly JniEnvHandle _env;
    private IntPtr _bridgeClass;  // global ref to JnBridge.class

    // native 回调委托必须作为字段保持活着，否则 GC 会回收导致 native 回调崩溃。
    private readonly OnMethodCallbackDelegate _onMethodEntryDelegate;
    private readonly OnMethodCallbackDelegate _onMethodExitDelegate;
    private readonly OnExceptionCallbackDelegate _onMethodExceptionDelegate;

    // handler 列表，由 EventListener 订阅/取消订阅。
    private readonly List<Action<MethodEntryEventData>> _methodEntryHandlers = new();
    private readonly List<Action<MethodExitEventData>> _methodExitHandlers = new();
    private readonly List<Action<ExceptionEventData>> _methodExceptionHandlers = new();
    private readonly object _gate = new();

    private bool _disposed;

    public JnBridge(JniEnvHandle env)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _onMethodEntryDelegate = OnMethodEntryNative;
        _onMethodExitDelegate = OnMethodExitNative;
        _onMethodExceptionDelegate = OnMethodExceptionNative;
    }

    /// <summary>加载到 JVM 之后的 JnBridge.class 全局引用（供 RegisterNatives 使用）。</summary>
    public IntPtr BridgeClass => _bridgeClass;

    /// <summary>
    /// 生成 JnBridge.class 字节码，用 DefineClass 加载到 JVM，并注册 native 方法。
    /// 必须在 JVM 启动后、且 ClassFileLoadHook 已激活之后调用。
    /// </summary>
    public void Initialize()
    {
        ThrowIfDisposed();
        var bytes = GenerateBridgeClassBytes();
        _bridgeClass = DefineBridgeClass(bytes);
        RegisterNativeMethods(_bridgeClass);
    }

    // ---- handler 管理 ----

    public void AddMethodEntryHandler(Action<MethodEntryEventData> handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        lock (_gate)
            _methodEntryHandlers.Add(handler);
    }

    public void RemoveMethodEntryHandler(Action<MethodEntryEventData> handler)
    {
        if (handler is null) return;
        lock (_gate)
            _methodEntryHandlers.Remove(handler);
    }

    public void AddMethodExitHandler(Action<MethodExitEventData> handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        lock (_gate)
            _methodExitHandlers.Add(handler);
    }

    public void RemoveMethodExitHandler(Action<MethodExitEventData> handler)
    {
        if (handler is null) return;
        lock (_gate)
            _methodExitHandlers.Remove(handler);
    }

    public void AddMethodExceptionHandler(Action<ExceptionEventData> handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        lock (_gate)
            _methodExceptionHandlers.Add(handler);
    }

    public void RemoveMethodExceptionHandler(Action<ExceptionEventData> handler)
    {
        if (handler is null) return;
        lock (_gate)
            _methodExceptionHandlers.Remove(handler);
    }

    public bool HasMethodEntrySubscribers
    {
        get { lock (_gate) return _methodEntryHandlers.Count > 0; }
    }

    public bool HasMethodExitSubscribers
    {
        get { lock (_gate) return _methodExitHandlers.Count > 0; }
    }

    // ---- 字节码生成 ----

    /// <summary>
    /// 用 ASM 生成 JnBridge.class 字节码：
    /// <code>
    /// package com.xsy.jn;
    /// public final class JnBridge {
    ///     private JnBridge() {}
    ///     public static native void onMethodEntry(String className, String methodName);
    ///     public static native void onMethodExit(String className, String methodName);
    /// }
    /// </code>
    /// </summary>
    private static byte[] GenerateBridgeClassBytes()
    {
        var cw = new Asm.ClassWriter(0);
        cw.Visit(
            Asm.Opcodes.V21,
            Asm.Opcodes.ACC_PUBLIC | Asm.Opcodes.ACC_FINAL | Asm.Opcodes.ACC_SUPER,
            BridgeClassInternalName,
            null,
            "java/lang/Object",
            null);

        // private constructor to prevent instantiation
        var ctor = cw.VisitMethod(
            Asm.Opcodes.ACC_PRIVATE,
            "<init>", "()V", null, null);
        if (ctor != null)
        {
            ctor.VisitCode();
            ctor.VisitVarInsn(Asm.Opcodes.ALOAD, 0);
            ctor.VisitMethodInsn(
                Asm.Opcodes.INVOKESPECIAL,
                "java/lang/Object", "<init>", "()V", false);
            ctor.VisitInsn(Asm.Opcodes.RETURN);
            ctor.VisitMaxs(1, 1);
            ctor.VisitEnd();
        }

        // static native onMethodEntry(String, String)
        var entry = cw.VisitMethod(
            Asm.Opcodes.ACC_PUBLIC | Asm.Opcodes.ACC_STATIC | Asm.Opcodes.ACC_NATIVE,
            "onMethodEntry", MethodCallbackDescriptor, null, null);
        entry?.VisitEnd();

        // static native onMethodExit(String, String)
        var exit = cw.VisitMethod(
            Asm.Opcodes.ACC_PUBLIC | Asm.Opcodes.ACC_STATIC | Asm.Opcodes.ACC_NATIVE,
            "onMethodExit", MethodCallbackDescriptor, null, null);
        exit?.VisitEnd();

        // static native onException(String, String, Throwable)
        var ex = cw.VisitMethod(
            Asm.Opcodes.ACC_PUBLIC | Asm.Opcodes.ACC_STATIC | Asm.Opcodes.ACC_NATIVE,
            "onException", ExceptionCallbackDescriptor, null, null);
        ex?.VisitEnd();

        cw.VisitEnd();
        return cw.ToByteArray();
    }

    // ---- DefineClass + RegisterNatives ----

    private IntPtr DefineBridgeClass(byte[] bytes)
    {
        fixed (byte* pBytes = bytes)
        {
            var localRef = _env.DefineClass(BridgeClassInternalName, IntPtr.Zero, pBytes, bytes.Length);
            if (localRef == IntPtr.Zero)
                throw new InvalidOperationException("JNIEnv->DefineClass returned null for JnBridge.");
            var global = _env.NewGlobalRef(localRef);
            _env.DeleteLocalRef(localRef);
            return global;
        }
    }

    private void RegisterNativeMethods(IntPtr bridgeClass)
    {
        var methods = new JNINativeMethod[3];
        methods[0].Name = (IntPtr)Marshal.StringToHGlobalAnsi("onMethodEntry");
        methods[0].Signature = (IntPtr)Marshal.StringToHGlobalAnsi(MethodCallbackDescriptor);
        methods[0].FnPtr = Marshal.GetFunctionPointerForDelegate(_onMethodEntryDelegate);
        methods[1].Name = (IntPtr)Marshal.StringToHGlobalAnsi("onMethodExit");
        methods[1].Signature = (IntPtr)Marshal.StringToHGlobalAnsi(MethodCallbackDescriptor);
        methods[1].FnPtr = Marshal.GetFunctionPointerForDelegate(_onMethodExitDelegate);
        methods[2].Name = (IntPtr)Marshal.StringToHGlobalAnsi("onException");
        methods[2].Signature = (IntPtr)Marshal.StringToHGlobalAnsi(ExceptionCallbackDescriptor);
        methods[2].FnPtr = Marshal.GetFunctionPointerForDelegate(_onMethodExceptionDelegate);

        try
        {
            _env.RegisterNatives(bridgeClass, methods);
        }
        finally
        {
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != IntPtr.Zero)
                    Marshal.FreeHGlobal(methods[i].Name);
                if (methods[i].Signature != IntPtr.Zero)
                    Marshal.FreeHGlobal(methods[i].Signature);
            }
        }
    }

    // ---- native 回调 ----

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnMethodCallbackDelegate(
        IntPtr jniEnv, IntPtr jclass, IntPtr jclassName, IntPtr jmethodName);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OnExceptionCallbackDelegate(
        IntPtr jniEnv, IntPtr jclass, IntPtr jclassName, IntPtr jmethodName, IntPtr jexception);

    private void OnMethodEntryNative(IntPtr jniEnv, IntPtr jclass, IntPtr jclassName, IntPtr jmethodName)
    {
        Action<MethodEntryEventData>[] snapshot;
        lock (_gate)
            snapshot = _methodEntryHandlers.ToArray();
        if (snapshot.Length == 0) return;

        var env = new JniEnvHandle(jniEnv);
        var className = SafeGetString(env, jclassName);
        var methodName = SafeGetString(env, jmethodName);

        var data = new MethodEntryEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_METHOD_ENTRY,
            TimestampUtc = DateTimeOffset.UtcNow,
            Class = new JvmClass(IntPtr.Zero, className),
            MethodName = methodName,
            MethodSignature = string.Empty,
        };

        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默，避免回调异常影响 JVM */ }
        }
    }

    private void OnMethodExitNative(IntPtr jniEnv, IntPtr jclass, IntPtr jclassName, IntPtr jmethodName)
    {
        Action<MethodExitEventData>[] snapshot;
        lock (_gate)
            snapshot = _methodExitHandlers.ToArray();
        if (snapshot.Length == 0) return;

        var env = new JniEnvHandle(jniEnv);
        var className = SafeGetString(env, jclassName);
        var methodName = SafeGetString(env, jmethodName);

        var data = new MethodExitEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_METHOD_EXIT,
            TimestampUtc = DateTimeOffset.UtcNow,
            Class = new JvmClass(IntPtr.Zero, className),
            MethodName = methodName,
            MethodSignature = string.Empty,
            ReturnValue = default,
            WasException = false,
        };

        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    private void OnMethodExceptionNative(IntPtr jniEnv, IntPtr jclass, IntPtr jclassName, IntPtr jmethodName, IntPtr jexception)
    {
        Action<ExceptionEventData>[] snapshot;
        lock (_gate)
            snapshot = _methodExceptionHandlers.ToArray();
        if (snapshot.Length == 0) return;

        var env = new JniEnvHandle(jniEnv);
        var className = SafeGetString(env, jclassName);
        var methodName = SafeGetString(env, jmethodName);
        var exceptionClassName = SafeGetExceptionClassName(env, jexception);

        var exceptionClass = new JvmClass(jexception, exceptionClassName);
        var data = new ExceptionEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_EXCEPTION,
            TimestampUtc = DateTimeOffset.UtcNow,
            Exception = new JvmObject(jexception, exceptionClass),
            ThrowingClass = new JvmClass(IntPtr.Zero, className),
            MethodName = methodName,
            MethodSignature = string.Empty,
        };

        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    /// <summary>
    /// 通过 JNI 反射调用 exception.getClass().getName() 获取异常类名。
    /// 失败时回退到 "java/lang/Throwable"。
    /// </summary>
    private static string SafeGetExceptionClassName(JniEnvHandle env, IntPtr exceptionObj)
    {
        if (exceptionObj == IntPtr.Zero) return "java/lang/Throwable";
        try
        {
            var clazz = env.GetObjectClass(exceptionObj);
            if (clazz == IntPtr.Zero) return "java/lang/Throwable";
            try
            {
                var classClass = env.FindClass("java/lang/Class");
                var getNameMethod = env.GetMethodID(classClass, "getName", "()Ljava/lang/String;");
                var nameJStr = env.CallObjectMethodA(clazz, getNameMethod, IntPtr.Zero);
                var name = env.GetStringUTFChars(nameJStr);
                // Class.getName() 返回 "java.lang.RuntimeException" 格式，转成内部名
                return name.Replace('.', '/');
            }
            finally
            {
                env.DeleteLocalRef(clazz);
            }
        }
        catch
        {
            return "java/lang/Throwable";
        }
    }

    private static string SafeGetString(JniEnvHandle env, IntPtr jstr)
    {
        if (jstr == IntPtr.Zero) return string.Empty;
        try { return env.GetStringUTFChars(jstr); }
        catch { return string.Empty; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_bridgeClass != IntPtr.Zero)
        {
            try { _env.UnregisterNatives(_bridgeClass); } catch { /* Dispose 路径静默 */ }
            _env.DeleteGlobalRef(_bridgeClass);
            _bridgeClass = IntPtr.Zero;
        }

        lock (_gate)
        {
            _methodEntryHandlers.Clear();
            _methodExitHandlers.Clear();
            _methodExceptionHandlers.Clear();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JnBridge));
    }
}
