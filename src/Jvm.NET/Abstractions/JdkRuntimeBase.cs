using System.Runtime.InteropServices;
using Jvm.NET.Abstractions.Jdk21;
using Jvm.NET.Interop;
using Jvm.NET.Interop.Jni;
using Jvm.NET.Interop.Jvmti;

namespace Jvm.NET.Abstractions;

/// <summary>
/// Generic <see cref="IJvmRuntime"/> implementation shared across JDK 21+ versions.
/// </summary>
/// <remarks>
/// <para>
/// JDK 21-25 share an identical JNI/JVMTI ABI; the only per-version difference is the
/// version constant passed to <c>JNI_CreateJavaVM</c> and <c>GetEnv</c>. This base class
/// parameterises those constants so that a single <see cref="IJdkImplementation"/> per
/// version is sufficient.
/// </para>
/// <para>
/// Boot sequence:
/// <list type="number">
/// <item>Load <c>jvm.dll</c> / <c>libjvm.so</c> / <c>libjvm.dylib</c> from <see cref="JvmInitializationOptions.JdkBinPath"/>.</item>
/// <item>Resolve <c>JNI_CreateJavaVM</c> and call it with a synthesised <see cref="JavaVMInitArgs"/>.</item>
/// <item>Obtain a <c>jvmtiEnv*</c> via <c>JavaVM-&gt;GetEnv(jvmtiVersion)</c>.</item>
/// <item>Add the capabilities required by bytecode modification / event listening.</item>
/// <item>Construct the invoker / bytecode modifier / event listener.</item>
/// </list>
/// </para>
/// </remarks>
public unsafe class JdkRuntimeBase : IJvmRuntime
{
    private readonly JvmInitializationOptions _options;
    private readonly INativeLibraryLoader _loader;
    private readonly int _version;
    private readonly int _jniVersion;
    private readonly int _jvmtiVersion;

    private JvmRuntimeState _state = JvmRuntimeState.NotStarted;

    // Native handles — set in Start(), cleared in Shutdown().
    private IntPtr _libraryHandle;
    private IntPtr _javaVm;       // JavaVM*
    private IntPtr _jniEnv;       // JNIEnv*
    private IntPtr _jvmtiEnv;     // jvmtiEnv*

    private Jdk21Invoker? _invoker;
    private Jdk21BytecodeModifier? _modifier;
    private Jdk21EventListener? _listener;
    private JvmtiEventHub? _eventHub;
    private JnBridge? _jnBridge;
    private MethodEventInstrumentor? _instrumentor;

    /// <summary>
    /// Creates a runtime for the given JDK version.
    /// </summary>
    /// <param name="options">Initialization options.</param>
    /// <param name="version">The JDK major version (e.g. 21, 22, 8).</param>
    /// <param name="jniVersion">JNI version constant (e.g. 0x00150000 for JDK 21).</param>
    /// <param name="jvmtiVersion">JVMTI version constant (e.g. 0x30150000 for JDK 21).</param>
    public JdkRuntimeBase(JvmInitializationOptions options, int version, int jniVersion, int jvmtiVersion)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loader = NativeLibraryLoader.Instance;
        _version = version;
        _jniVersion = jniVersion;
        _jvmtiVersion = jvmtiVersion;
    }

    /// <summary>
    /// Creates a runtime from a registered <see cref="IJdkImplementation"/>.
    /// </summary>
    public JdkRuntimeBase(JvmInitializationOptions options, IJdkImplementation implementation)
        : this(options, implementation.Version, implementation.JniVersion, implementation.JvmtiVersion)
    {
    }

    public JvmRuntimeState State => _state;
    public int Version => _version;

    public IJvmInvoker Invoker
    {
        get
        {
            EnsureRunning();
            return _invoker!;
        }
    }

    public IBytecodeModifier BytecodeModifier
    {
        get
        {
            EnsureRunning();
            if (!_options.EnableBytecodeModification)
                throw new InvalidOperationException("Bytecode modification was disabled at startup.");
            return _modifier!;
        }
    }

    public IJvmEventListener EventListener
    {
        get
        {
            EnsureRunning();
            if (!_options.EnableEventListening)
                throw new InvalidOperationException("Event listening was disabled at startup.");
            return _listener!;
        }
    }

    public void Start()
    {
        if (_state is JvmRuntimeState.Running or JvmRuntimeState.Starting)
            return;
        if (_state == JvmRuntimeState.Stopped)
            throw new InvalidOperationException("Cannot restart a stopped JVM runtime.");

        _state = JvmRuntimeState.Starting;
        try
        {
            LoadJvmLibrary();
            CreateJavaVm();
            AcquireJvmtiEnv();
            AddRequiredCapabilities();

            // 创建共享的 JVMTI 事件回调协调者。BytecodeModifier 和 EventListener
            // 都通过它注册回调，最后由这里统一 Apply() 一次（SetEventCallbacks 是全局的）。
            if (_jvmtiEnv != IntPtr.Zero)
                _eventHub = new JvmtiEventHub(_jvmtiEnv);

            _invoker = new Jdk21Invoker(_jniEnv);
            if (_options.EnableBytecodeModification && _eventHub is not null)
                _modifier = new Jdk21BytecodeModifier(_jvmtiEnv, _jniEnv, _eventHub);

            // 方案 B：在 EventListener 之前创建 JnBridge 和 MethodEventInstrumentor，
            // 使它们能传入 EventListener。JnBridge.Initialize 会 DefineClass 加载
            // JnBridge 类到 JVM，此时 ClassFileLoadHook 尚未激活，JnBridge 类不会被插桩。
            if (_options.EnableBytecodeModification && _options.EnableEventListening && _modifier is not null)
            {
                _jnBridge = new JnBridge(new JniEnvHandle(_jniEnv));
                _jnBridge.Initialize();
                _instrumentor = new MethodEventInstrumentor();
            }

            if (_options.EnableEventListening && _eventHub is not null)
                _listener = new Jdk21EventListener(_jvmtiEnv, _jniEnv, _eventHub, _jnBridge, _instrumentor);

            // 所有回调槽位已注册，提交给 JVMTI。
            _eventHub?.Apply();
            // Apply 之后才能启用事件通知（BytecodeModifier 的 ClassFileLoadHook）。
            _modifier?.Activate();

            // 方案 B：ClassFileLoadHook 激活后注册插桩器，使后续类加载被插桩。
            // JnBridge 自身已被 MethodEventInstrumentor.ShouldSkip 过滤，不会自举循环。
            if (_instrumentor is not null && _modifier is not null)
            {
                _modifier.RegisterTransformer(_instrumentor);
            }

            _state = JvmRuntimeState.Running;
        }
        catch
        {
            _state = JvmRuntimeState.Faulted;
            // Best-effort cleanup of partial state.
            try { DestroyJavaVm(); } catch { /* swallow */ }
            try { FreeLibraryHandle(); } catch { /* swallow */ }
            throw;
        }
    }

    public void Shutdown()
    {
        if (_state == JvmRuntimeState.Stopped)
            return;
        _state = JvmRuntimeState.ShuttingDown;
        try
        {
            // Dispose managed wrappers first so they release their global refs
            // and disable event notifications.
            _listener?.Dispose();
            _modifier?.Dispose();
            _invoker?.Dispose();
            _jnBridge?.Dispose();
            _listener = null;
            _modifier = null;
            _invoker = null;
            _jnBridge = null;
            _instrumentor = null;

            // 清空 JVMTI 回调表（必须在 modifier/listener Dispose 之后）。
            _eventHub?.Reset();
            _eventHub = null;

            DestroyJavaVm();
            FreeLibraryHandle();
        }
        finally
        {
            _javaVm = IntPtr.Zero;
            _jniEnv = IntPtr.Zero;
            _jvmtiEnv = IntPtr.Zero;
            _state = JvmRuntimeState.Stopped;
        }
    }

    public void Dispose()
    {
        if (_state != JvmRuntimeState.Stopped)
            Shutdown();
    }

    // ---- boot steps ----

    private void LoadJvmLibrary()
    {
        _libraryHandle = _loader.Load(_options.JdkBinPath);
        if (_libraryHandle == IntPtr.Zero)
            throw new DllNotFoundException("Native JVM library could not be loaded.");
    }

    private void CreateJavaVm()
    {
        var createVm = NativeMethods.GetCreateJavaVM(_loader, _libraryHandle);

        // Build the JavaVMOption array in unmanaged memory so JNI sees a stable pointer.
        var optionStrings = BuildOptionStrings();
        var optionHandles = new JavaVMOption[optionStrings.Count];
        var optionPtrs = Marshal.AllocHGlobal(optionStrings.Count * Marshal.SizeOf<JavaVMOption>());
        try
        {
            for (int i = 0; i < optionStrings.Count; i++)
            {
                optionHandles[i] = new JavaVMOption(optionStrings[i]);
                Marshal.StructureToPtr(optionHandles[i], optionPtrs + (i * Marshal.SizeOf<JavaVMOption>()), fDeleteOld: false);
            }

            var args = new JavaVMInitArgs
            {
                Version = _jniVersion,
                nOptions = optionStrings.Count,
                Options = optionPtrs,
                IgnoreUnrecognized = JniTypes.JNI_TRUE,
            };

            int rc = createVm(out _javaVm, out _jniEnv, (IntPtr)(&args));
            if (rc != NativeConstants.JNI_OK)
                throw new InvalidOperationException($"JNI_CreateJavaVM returned {rc} (expected JNI_OK={NativeConstants.JNI_OK}).");
        }
        finally
        {
            for (int i = 0; i < optionHandles.Length; i++)
                optionHandles[i].Free();
            Marshal.FreeHGlobal(optionPtrs);
        }
    }

    private List<string> BuildOptionStrings()
    {
        var result = new List<string>(_options.VmArguments.Count + 1);

        if (_options.Classpath.Count > 0)
        {
            var cp = string.Join(Path.PathSeparator, _options.Classpath);
            result.Add($"-Djava.class.path={cp}");
        }

        foreach (var arg in _options.VmArguments)
        {
            if (!string.IsNullOrWhiteSpace(arg))
                result.Add(arg);
        }

        return result;
    }

    private void AcquireJvmtiEnv()
    {
        // We always try to acquire jvmtiEnv because the caller may turn on
        // bytecode modification / event listening later by re-issuing Start on
        // a sub-component. If GetEnv fails and the caller didn't explicitly
        // require JVMTI, we treat it as a soft failure.
        ref var invoke = ref JNIInvokeInterface.FromJavaVm(_javaVm);
        IntPtr jvmtiEnvPtr = IntPtr.Zero;
        int rc = invoke.GetEnv(_javaVm, &jvmtiEnvPtr, _jvmtiVersion);
        _jvmtiEnv = jvmtiEnvPtr;

        if (rc != NativeConstants.JNI_OK)
        {
            if (_options.RequireJvmti || _options.EnableBytecodeModification || _options.EnableEventListening)
                throw new InvalidOperationException($"JavaVM->GetEnv(0x{_jvmtiVersion:X8}) returned {rc}; JVMTI is required by the current options.");
            _jvmtiEnv = IntPtr.Zero;
        }
    }

    private void AddRequiredCapabilities()
    {
        if (_jvmtiEnv == IntPtr.Zero)
            return;

        ref var jvmti = ref JvmtiInterface_1_.FromJvmtiEnv(_jvmtiEnv);

        // 注意：嵌入式 JVM 经过 JNI_CreateJavaVM 返回后已处于 JVMTI_PHASE_LIVE，
        // 此时只能添加 always-on 的 capability。onload-only 的 capability（如
        // can_generate_method_entry_events / can_generate_method_exit_events /
        // can_generate_exception_events）需要在 JVMTI_PHASE_ONLOAD 期添加，
        // 添加不了。MethodEntry/MethodExit/Exception 事件因此暂不支持，
        // 需要 native agent（-agentpath:）在 OnLoad phase 注册才能启用。
        // ClassLoad/ClassPrepare/VMInit/VMDeath/ThreadStart/ThreadEnd 事件不需要
        // 任何 capability，可在 live phase 直接启用。
        var caps = new JvmtiCapabilities();
        if (_options.EnableBytecodeModification)
        {
            caps.SetCapability(JvmtiCapabilities.CanRedefineClasses);
            caps.SetCapability(JvmtiCapabilities.CanRetransformClasses);
            caps.SetCapability(JvmtiCapabilities.CanRetransformAnyClass);
            caps.SetCapability(JvmtiCapabilities.CanGenerateAllClassHookEvents);
        }
        // EnableEventListening 不需要添加任何 capability：
        // - ClassLoad/ClassPrepare/VMInit/VMDeath/ThreadStart/ThreadEnd 无 capability 要求
        // - MethodEntry/MethodExit/Exception 需要 onload-only capability，live phase 加不了

        int rc = jvmti.AddCapabilities(_jvmtiEnv, &caps);
        if (rc != NativeConstants.JNI_OK)
            throw new InvalidOperationException($"jvmtiEnv->AddCapabilities returned {rc}.");
    }

    private void DestroyJavaVm()
    {
        if (_javaVm == IntPtr.Zero)
            return;
        ref var invoke = ref JNIInvokeInterface.FromJavaVm(_javaVm);
        invoke.DestroyJavaVM(_javaVm);
    }

    private void FreeLibraryHandle()
    {
        if (_libraryHandle != IntPtr.Zero)
        {
            _loader.Free(_libraryHandle);
            _libraryHandle = IntPtr.Zero;
        }
    }

    private void EnsureRunning()
    {
        if (_state != JvmRuntimeState.Running)
            throw new InvalidOperationException($"Runtime is in state '{_state}', expected 'Running'.");
    }
}
