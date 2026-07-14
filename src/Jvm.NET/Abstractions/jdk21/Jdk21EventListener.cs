using System.Runtime.InteropServices;
using Jvm.NET.Interop;
using Jvm.NET.Interop.Jvmti;

namespace Jvm.NET.Abstractions.Jdk21;

/// <summary>
/// JDK 21 specific <see cref="IJvmEventListener"/>. Sets / clears the JVMTI event
/// notification mode bits and routes native callbacks to managed handlers.
///
/// 回调路由：JVMTI 事件 -> On* (trampoline) -> 在锁内快照 handler 列表 ->
///   逐个调用。每个事件 kind 有独立的 handler 列表和订阅计数。
///
/// 线程安全：_handlers 用锁保护；回调中先快照再调用。
/// </summary>
internal sealed unsafe class Jdk21EventListener : IJvmEventListener, IDisposable
{
    private readonly IntPtr _jvmtiEnv;
    private readonly JvmtiEventHub _hub;
    private readonly object _gate = new();

    // 方案 B 的桥接器；为 null 表示方案 B 未启用，MethodEntry/Exit 仍抛 NotSupportedException。
    private readonly JnBridge? _jnBridge;

    // 方案 B 的插桩器；为 null 表示方案 B 未启用。有 MethodEntry/Exit 订阅者时置 Enabled=true。
    private readonly MethodEventInstrumentor? _instrumentor;

    // 各事件的 handler 列表 + 订阅状态。用数组而非 List<Action> 以便快照。
    private readonly List<Action<MethodEntryEventData>> _methodEntry = new();
    private readonly List<Action<MethodExitEventData>> _methodExit = new();
    private readonly List<Action<ClassLoadEventData>> _classLoad = new();
    private readonly List<Action<ClassPrepareEventData>> _classPrepare = new();
    private readonly List<Action<ThreadStartEventData>> _threadStart = new();
    private readonly List<Action<ThreadEndEventData>> _threadEnd = new();
    private readonly List<Action<VmInitEventData>> _vmInit = new();
    private readonly List<Action<VmDeathEventData>> _vmDeath = new();
    private readonly List<Action<ExceptionEventData>> _exception = new();

    // 委托引用作为字段保持活着（hub 也会钉住）。
    private readonly VmInitDelegate _vmInitDelegate;
    private readonly VmDeathDelegate _vmDeathDelegate;
    private readonly ThreadStartDelegate _threadStartDelegate;
    private readonly ThreadEndDelegate _threadEndDelegate;
    private readonly ClassLoadDelegate _classLoadDelegate;
    private readonly ClassPrepareDelegate _classPrepareDelegate;
    private readonly MethodEntryDelegate _methodEntryDelegate;
    private readonly MethodExitDelegate _methodExitDelegate;
    private readonly ExceptionDelegate _exceptionDelegate;

    private bool _disposed;
    // 跟踪哪些事件 kind 已启用，避免重复 Enable / 在 Dispose 时知道要 Disable 哪些。
    private readonly HashSet<int> _enabledKinds = new();

    public Jdk21EventListener(IntPtr jvmtiEnv, IntPtr jniEnv, JvmtiEventHub hub,
        JnBridge? jnBridge = null, MethodEventInstrumentor? instrumentor = null)
    {
        if (jvmtiEnv == IntPtr.Zero) throw new ArgumentNullException(nameof(jvmtiEnv));
        if (hub is null) throw new ArgumentNullException(nameof(hub));
        _jvmtiEnv = jvmtiEnv;
        _hub = hub;
        _jnBridge = jnBridge;
        _instrumentor = instrumentor;
        // jniEnv 参数保留：事件回调中 JVMTI 会重新提供 jniEnv，构造期的不需要存。
        _ = jniEnv;

        // 创建所有 trampoline 委托并注册到 hub。
        _vmInitDelegate = OnVmInit;
        _vmDeathDelegate = OnVmDeath;
        _threadStartDelegate = OnThreadStart;
        _threadEndDelegate = OnThreadEnd;
        _classLoadDelegate = OnClassLoad;
        _classPrepareDelegate = OnClassPrepare;
        _methodEntryDelegate = OnMethodEntry;
        _methodExitDelegate = OnMethodExit;
        _exceptionDelegate = OnException;

        _hub.SetCallback(ref _hub.Callbacks.VMInit, _vmInitDelegate);
        _hub.SetCallback(ref _hub.Callbacks.VMDeath, _vmDeathDelegate);
        _hub.SetCallback(ref _hub.Callbacks.ThreadStart, _threadStartDelegate);
        _hub.SetCallback(ref _hub.Callbacks.ThreadEnd, _threadEndDelegate);
        _hub.SetCallback(ref _hub.Callbacks.ClassLoad, _classLoadDelegate);
        _hub.SetCallback(ref _hub.Callbacks.ClassPrepare, _classPrepareDelegate);
        _hub.SetCallback(ref _hub.Callbacks.MethodEntry, _methodEntryDelegate);
        _hub.SetCallback(ref _hub.Callbacks.MethodExit, _methodExitDelegate);
        _hub.SetCallback(ref _hub.Callbacks.Exception, _exceptionDelegate);
    }

    public IDisposable SubscribeMethodEntry(Action<MethodEntryEventData> handler)
    {
        // 方案 B：通过 JnBridge 字节码插桩模拟事件，绕过 onload-only capability 限制。
        if (_jnBridge is null || _instrumentor is null)
            throw new NotSupportedException(
                "MethodEntry 事件需要启用方案 B（EnableBytecodeModification + EnableEventListening），" +
                "通过 ClassFileLoadHook 字节码插桩模拟。" +
                "原 JVMTI MethodEntry 事件需要 can_generate_method_entry_events capability，" +
                "该 capability 属于 onload-only，嵌入式 JVM 在 live phase 无法添加。");
        ThrowIfDisposed();
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        bool wasEmpty;
        lock (_gate)
        {
            wasEmpty = _methodEntry.Count == 0 && _methodExit.Count == 0 && _exception.Count == 0;
            _methodEntry.Add(handler);
        }

        _jnBridge.AddMethodEntryHandler(handler);
        if (wasEmpty)
            _instrumentor.Enabled = true;

        return new MethodEventSubscription(this, handler);
    }

    public IDisposable SubscribeMethodExit(Action<MethodExitEventData> handler)
    {
        if (_jnBridge is null || _instrumentor is null)
            throw new NotSupportedException(
                "MethodExit 事件需要启用方案 B（EnableBytecodeModification + EnableEventListening），" +
                "通过 ClassFileLoadHook 字节码插桩模拟。" +
                "原 JVMTI MethodExit 事件需要 can_generate_method_exit_events capability，" +
                "该 capability 属于 onload-only，嵌入式 JVM 在 live phase 无法添加。");
        ThrowIfDisposed();
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        bool wasEmpty;
        lock (_gate)
        {
            wasEmpty = _methodEntry.Count == 0 && _methodExit.Count == 0 && _exception.Count == 0;
            _methodExit.Add(handler);
        }

        _jnBridge.AddMethodExitHandler(handler);
        if (wasEmpty)
            _instrumentor.Enabled = true;

        return new MethodEventSubscription(this, handler);
    }

    public IDisposable SubscribeClassLoad(Action<ClassLoadEventData> handler)
        => Subscribe(_classLoad, handler, NativeConstants.JVMTI_EVENT_CLASS_LOAD);

    public IDisposable SubscribeClassPrepare(Action<ClassPrepareEventData> handler)
        => Subscribe(_classPrepare, handler, NativeConstants.JVMTI_EVENT_CLASS_PREPARE);

    public IDisposable SubscribeThreadStart(Action<ThreadStartEventData> handler)
        => Subscribe(_threadStart, handler, NativeConstants.JVMTI_EVENT_THREAD_START);

    public IDisposable SubscribeThreadEnd(Action<ThreadEndEventData> handler)
        => Subscribe(_threadEnd, handler, NativeConstants.JVMTI_EVENT_THREAD_END);

    public IDisposable SubscribeVmInit(Action<VmInitEventData> handler)
        => Subscribe(_vmInit, handler, NativeConstants.JVMTI_EVENT_VM_INIT);

    public IDisposable SubscribeVmDeath(Action<VmDeathEventData> handler)
        => Subscribe(_vmDeath, handler, NativeConstants.JVMTI_EVENT_VM_DEATH);

    public IDisposable SubscribeException(Action<ExceptionEventData> handler)
    {
        // 方案 B：通过 JnBridge 字节码插桩 try-catch 模拟 Exception 事件，
        // 绕过 can_generate_exception_events 的 onload-only 限制。
        if (_jnBridge is null || _instrumentor is null)
            throw new NotSupportedException(
                "Exception 事件需要启用方案 B（EnableBytecodeModification + EnableEventListening），" +
                "通过 ClassFileLoadHook 字节码插桩在方法体外包裹 try-catch 模拟。" +
                "原 JVMTI Exception 事件需要 can_generate_exception_events capability，" +
                "该 capability 属于 onload-only，嵌入式 JVM 在 live phase 无法添加。");
        ThrowIfDisposed();
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        bool wasEmpty;
        lock (_gate)
        {
            wasEmpty = _methodEntry.Count == 0 && _methodExit.Count == 0 && _exception.Count == 0;
            _exception.Add(handler);
        }

        _jnBridge.AddMethodExceptionHandler(handler);
        if (wasEmpty)
            _instrumentor.Enabled = true;

        return new MethodEventSubscription(this, handler);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 关闭所有已启用的事件通知。
        foreach (var kind in _enabledKinds)
        {
            try { _hub.DisableEvent(kind); }
            catch { /* Dispose 路径静默 */ }
        }
        _enabledKinds.Clear();

        lock (_gate)
        {
            _methodEntry.Clear();
            _methodExit.Clear();
            _classLoad.Clear();
            _classPrepare.Clear();
            _threadStart.Clear();
            _threadEnd.Clear();
            _vmInit.Clear();
            _vmDeath.Clear();
            _exception.Clear();
        }
    }

    // ---- 订阅辅助 ----

    private IDisposable Subscribe<T>(List<Action<T>> list, Action<T> handler, int eventKind)
    {
        ThrowIfDisposed();
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        bool needEnable;
        lock (_gate)
        {
            needEnable = list.Count == 0;
            list.Add(handler);
        }

        if (needEnable && _enabledKinds.Add(eventKind))
        {
            // 注意：hub.Apply() 必须已经由 Runtime 调用过，否则 EnableEvent 不会报错但回调不会触发。
            _hub.EnableEvent(eventKind);
        }

        return new Subscription<T>(this, list, handler, eventKind);
    }

    private void Unsubscribe<T>(List<Action<T>> list, Action<T> handler, int eventKind)
    {
        bool shouldDisable;
        lock (_gate)
        {
            list.Remove(handler);
            shouldDisable = list.Count == 0;
        }

        if (shouldDisable && _enabledKinds.Remove(eventKind))
        {
            try { _hub.DisableEvent(eventKind); }
            catch { /* 静默 */ }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Jdk21EventListener));
    }

    // ---- JVMTI 回调 trampoline ----
    // 签名严格匹配 jvmti.h 中的 jvmtiEventCallbacks 字段类型。

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VmInitDelegate(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VmDeathDelegate(IntPtr jvmtiEnv, IntPtr jniEnv);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ThreadStartDelegate(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ThreadEndDelegate(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ClassLoadDelegate(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ClassPrepareDelegate(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr klass);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MethodEntryDelegate(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr methodId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MethodExitDelegate(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr methodId, byte poppedByException, long returnValue);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ExceptionDelegate(
        IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr methodId, long location,
        IntPtr exceptionObj, IntPtr catchMethodId, long catchLocation);

    // ---- 回调实现 ----

    private void OnVmInit(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread)
    {
        var snapshot = SnapshotHandlers(_vmInit);
        if (snapshot.Length == 0) return;
        var data = new VmInitEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_VM_INIT,
            TimestampUtc = DateTimeOffset.UtcNow,
        };
        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    private void OnVmDeath(IntPtr jvmtiEnv, IntPtr jniEnv)
    {
        var snapshot = SnapshotHandlers(_vmDeath);
        if (snapshot.Length == 0) return;
        var data = new VmDeathEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_VM_DEATH,
            TimestampUtc = DateTimeOffset.UtcNow,
        };
        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    private void OnThreadStart(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread)
    {
        var snapshot = SnapshotHandlers(_threadStart);
        if (snapshot.Length == 0) return;

        var (name, handle) = ReadThreadInfo(jvmtiEnv, thread);
        var data = new ThreadStartEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_THREAD_START,
            TimestampUtc = DateTimeOffset.UtcNow,
            ThreadHandle = thread,
            ThreadName = name,
        };
        _ = handle;
        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    private void OnThreadEnd(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread)
    {
        var snapshot = SnapshotHandlers(_threadEnd);
        if (snapshot.Length == 0) return;

        var (name, _) = ReadThreadInfo(jvmtiEnv, thread);
        var data = new ThreadEndEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_THREAD_END,
            TimestampUtc = DateTimeOffset.UtcNow,
            ThreadHandle = thread,
            ThreadName = name,
        };
        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    private void OnClassLoad(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr klass)
    {
        var snapshot = SnapshotHandlers(_classLoad);
        if (snapshot.Length == 0) return;

        var className = ReadClassSignature(jvmtiEnv, klass);
        var data = new ClassLoadEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_CLASS_LOAD,
            TimestampUtc = DateTimeOffset.UtcNow,
            Class = new JvmClass(klass, className),
        };
        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    private void OnClassPrepare(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr klass)
    {
        var snapshot = SnapshotHandlers(_classPrepare);
        if (snapshot.Length == 0) return;

        var className = ReadClassSignature(jvmtiEnv, klass);
        var data = new ClassPrepareEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_CLASS_PREPARE,
            TimestampUtc = DateTimeOffset.UtcNow,
            Class = new JvmClass(klass, className),
        };
        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    private void OnMethodEntry(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr methodId)
    {
        var snapshot = SnapshotHandlers(_methodEntry);
        if (snapshot.Length == 0) return;

        var (klass, name, sig) = ReadMethodInfo(jvmtiEnv, methodId);
        var data = new MethodEntryEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_METHOD_ENTRY,
            TimestampUtc = DateTimeOffset.UtcNow,
            Class = new JvmClass(klass, name),
            MethodName = name,
            MethodSignature = sig,
        };
        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    private void OnMethodExit(IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr methodId, byte poppedByException, long returnValue)
    {
        var snapshot = SnapshotHandlers(_methodExit);
        if (snapshot.Length == 0) return;

        var (klass, name, sig) = ReadMethodInfo(jvmtiEnv, methodId);
        var data = new MethodExitEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_METHOD_EXIT,
            TimestampUtc = DateTimeOffset.UtcNow,
            Class = new JvmClass(klass, name),
            MethodName = name,
            MethodSignature = sig,
            ReturnValue = JvmValue.FromLong(returnValue),
            WasException = poppedByException != 0,
        };
        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    private void OnException(
        IntPtr jvmtiEnv, IntPtr jniEnv, IntPtr thread, IntPtr methodId, long location,
        IntPtr exceptionObj, IntPtr catchMethodId, long catchLocation)
    {
        var snapshot = SnapshotHandlers(_exception);
        if (snapshot.Length == 0) return;

        // 异常对象的 class 通过 GetObjectClass 获取需要 jniEnv，但这里我们只有 jvmtiEnv。
        // JVMTI 没有 GetObjectClass 等价物；为简化，用 exceptionObj 直接作为 JvmObject.Handle，
        // Class 用 jmethodID 推断的 throwing class（通过 ReadMethodInfo）。
        JvmClass? throwingClass = null;
        string? methodName = null;
        string? methodSig = null;
        if (methodId != IntPtr.Zero)
        {
            var (klass, name, sig) = ReadMethodInfo(jvmtiEnv, methodId);
            throwingClass = new JvmClass(klass, name);
            methodName = name;
            methodSig = sig;
        }

        // 异常对象的 JvmClass 无法在纯 jvmtiEnv 下构造，用占位 class。
        var exceptionClass = new JvmClass(exceptionObj, "java/lang/Throwable");
        var data = new ExceptionEventData
        {
            EventKind = NativeConstants.JVMTI_EVENT_EXCEPTION,
            TimestampUtc = DateTimeOffset.UtcNow,
            Exception = new JvmObject(exceptionObj, exceptionClass),
            ThrowingClass = throwingClass,
            MethodName = methodName,
            MethodSignature = methodSig,
        };
        foreach (var h in snapshot)
        {
            try { h(data); } catch { /* 静默 */ }
        }
    }

    // ---- JVMTI 元数据读取辅助 ----

    private Action<T>[] SnapshotHandlers<T>(List<Action<T>> list)
    {
        lock (_gate)
            return list.ToArray();
    }

    private (string name, IntPtr threadGroup) ReadThreadInfo(IntPtr jvmtiEnv, IntPtr thread)
    {
        ref var jvmti = ref _hub.Interface;
        JvmtiThreadInfo info;
        int rc = jvmti.GetThreadInfo(jvmtiEnv, thread, &info);
        if (rc != NativeConstants.JNI_OK)
            return ("<unknown>", IntPtr.Zero);

        try
        {
            var name = info.GetName() ?? "<unknown>";
            return (name, info.ThreadGroup);
        }
        finally
        {
            if (info.Name != IntPtr.Zero)
                jvmti.Deallocate(jvmtiEnv, info.Name);
        }
    }

    private string ReadClassSignature(IntPtr jvmtiEnv, IntPtr klass)
    {
        ref var jvmti = ref _hub.Interface;
        IntPtr pSig = IntPtr.Zero;
        IntPtr pGen = IntPtr.Zero;
        int rc = jvmti.GetClassSignature(jvmtiEnv, klass, &pSig, &pGen);
        if (rc != NativeConstants.JNI_OK || pSig == IntPtr.Zero)
            return "<unknown>";

        try
        {
            // 签名形如 "Ljava/lang/String;"，去掉首尾的 L 和 ; 得到类名。
            var sig = Marshal.PtrToStringUTF8(pSig) ?? "<unknown>";
            if (sig.Length >= 2 && sig[0] == 'L' && sig[^1] == ';')
                return sig.Substring(1, sig.Length - 2).Replace('/', '.');
            return sig;
        }
        finally
        {
            jvmti.Deallocate(jvmtiEnv, pSig);
            if (pGen != IntPtr.Zero)
                jvmti.Deallocate(jvmtiEnv, pGen);
        }
    }

    private (IntPtr klass, string name, string signature) ReadMethodInfo(IntPtr jvmtiEnv, IntPtr methodId)
    {
        ref var jvmti = ref _hub.Interface;
        IntPtr pName = IntPtr.Zero;
        IntPtr pSig = IntPtr.Zero;
        IntPtr pGen = IntPtr.Zero;
        IntPtr declaringClass = IntPtr.Zero;

        int rc = jvmti.GetMethodName(jvmtiEnv, methodId, &pName, &pSig, &pGen);
        if (rc != NativeConstants.JNI_OK)
            return (IntPtr.Zero, "<unknown>", "<unknown>");

        try
        {
            var name = pName == IntPtr.Zero ? "<unknown>" : (Marshal.PtrToStringUTF8(pName) ?? "<unknown>");
            var sig = pSig == IntPtr.Zero ? "<unknown>" : (Marshal.PtrToStringUTF8(pSig) ?? "<unknown>");

            rc = jvmti.GetMethodDeclaringClass(jvmtiEnv, methodId, &declaringClass);
            if (rc != NativeConstants.JNI_OK)
                declaringClass = IntPtr.Zero;

            return (declaringClass, name, sig);
        }
        finally
        {
            if (pName != IntPtr.Zero) jvmti.Deallocate(jvmtiEnv, pName);
            if (pSig != IntPtr.Zero) jvmti.Deallocate(jvmtiEnv, pSig);
            if (pGen != IntPtr.Zero) jvmti.Deallocate(jvmtiEnv, pGen);
        }
    }

    // ---- 订阅 token ----

    private sealed class Subscription<T> : IDisposable
    {
        private readonly Jdk21EventListener _owner;
        private readonly List<Action<T>> _list;
        private readonly Action<T> _handler;
        private readonly int _eventKind;
        private bool _unsubscribed;

        public Subscription(Jdk21EventListener owner, List<Action<T>> list, Action<T> handler, int eventKind)
        {
            _owner = owner;
            _list = list;
            _handler = handler;
            _eventKind = eventKind;
        }

        public void Dispose()
        {
            if (_unsubscribed) return;
            _unsubscribed = true;
            _owner.Unsubscribe(_list, _handler, _eventKind);
        }
    }

    /// <summary>
    /// 方案 B 的订阅 token：Dispose 时从 JnBridge 和本地列表移除 handler，
    /// 当 MethodEntry/Exit 都无订阅者时禁用 instrumentor。
    /// </summary>
    private sealed class MethodEventSubscription : IDisposable
    {
        private readonly Jdk21EventListener _owner;
        private readonly Action<MethodEntryEventData>? _entryHandler;
        private readonly Action<MethodExitEventData>? _exitHandler;
        private readonly Action<ExceptionEventData>? _exceptionHandler;
        private bool _unsubscribed;

        public MethodEventSubscription(Jdk21EventListener owner, Action<MethodEntryEventData> handler)
        {
            _owner = owner;
            _entryHandler = handler;
        }

        public MethodEventSubscription(Jdk21EventListener owner, Action<MethodExitEventData> handler)
        {
            _owner = owner;
            _exitHandler = handler;
        }

        public MethodEventSubscription(Jdk21EventListener owner, Action<ExceptionEventData> handler)
        {
            _owner = owner;
            _exceptionHandler = handler;
        }

        public void Dispose()
        {
            if (_unsubscribed) return;
            _unsubscribed = true;

            bool shouldDisable;
            lock (_owner._gate)
            {
                if (_entryHandler is not null)
                {
                    _owner._methodEntry.Remove(_entryHandler);
                    _owner._jnBridge?.RemoveMethodEntryHandler(_entryHandler);
                }
                if (_exitHandler is not null)
                {
                    _owner._methodExit.Remove(_exitHandler);
                    _owner._jnBridge?.RemoveMethodExitHandler(_exitHandler);
                }
                if (_exceptionHandler is not null)
                {
                    _owner._exception.Remove(_exceptionHandler);
                    _owner._jnBridge?.RemoveMethodExceptionHandler(_exceptionHandler);
                }
                shouldDisable = _owner._methodEntry.Count == 0
                    && _owner._methodExit.Count == 0
                    && _owner._exception.Count == 0;
            }

            if (_owner._instrumentor is not null && shouldDisable)
                _owner._instrumentor.Enabled = false;
        }
    }
}
