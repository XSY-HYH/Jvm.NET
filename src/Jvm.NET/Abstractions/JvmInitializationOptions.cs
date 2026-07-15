namespace Jvm.NET.Abstractions;

/// <summary>
/// Configuration required to bootstrap an embedded JVM in the current process.
/// Callers MUST initialize Jvm.NET with these options before any other operation.
/// </summary>
public sealed class JvmInitializationOptions
{
    /// <summary>
    /// Absolute path to the JDK <c>bin</c> directory (e.g. <c>C:\jdk-21\bin</c> or <c>/usr/lib/jvm/jdk-21/bin</c>).
    /// The native loader will resolve <c>jvm.dll</c> / <c>libjvm.so</c> / <c>libjvm.dylib</c> from here.
    /// </summary>
    public required string JdkBinPath { get; set; }

    /// <summary>
    /// Target JDK major version (e.g. 21, 22, 8). Used to select the matching
    /// <see cref="IJdkImplementation"/> registered in <see cref="JdkImplementationRegistry"/>.
    /// </summary>
    public required int Version { get; set; }

    /// <summary>
    /// Extra <c>-X</c> / <c>-D</c> JVM arguments forwarded to <c>JNI_CreateJavaVM</c>.
    /// </summary>
    public IReadOnlyList<string> VmArguments { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Classpath entries (jar files or directories) appended to the JVM's
    /// <c>java.class.path</c> at startup.
    /// </summary>
    public IReadOnlyList<string> Classpath { get; set; } = Array.Empty<string>();

    /// <summary>
    /// When <c>true</c>, the JVMTI agent is loaded so that <see cref="IBytecodeModifier"/>
    /// can register <c>ClassFileLoadHook</c> transformers. Defaults to <c>true</c>.
    /// </summary>
    public bool EnableBytecodeModification { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, JVMTI event callbacks (MethodEntry / MethodExit / VM events)
    /// are wired up so that <see cref="IJvmEventListener"/> can deliver events.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool EnableEventListening { get; set; } = true;

    /// <summary>
    /// When <c>true</c> the library asserts that JVMTI was successfully obtained during
    /// startup and throws <see cref="InvalidOperationException"/> otherwise. Useful in tests.
    /// </summary>
    public bool RequireJvmti { get; set; } = false;

    /// <summary>
    /// Java/.NET 互操作选项。控制桥接 jar 加载与回调路由策略。
    /// 默认使用纯 JNI+ASM 无 jar 模式（<see cref="InteropMode.NativeOnly"/>）。
    /// </summary>
    public JvmInteropOptions Interop { get; set; } = new();
}

/// <summary>
/// Java/.NET 双向互操作的配置选项。
/// </summary>
public sealed class JvmInteropOptions
{
    /// <summary>
    /// 互操作模式。默认 <see cref="InteropMode.NativeOnly"/>（纯 JNI+ASM，无需 jar）。
    /// 选择 <see cref="InteropMode.WithJar"/> 时需提供 <see cref="BridgeJarPath"/>。
    /// </summary>
    public InteropMode Mode { get; set; } = InteropMode.NativeOnly;

    /// <summary>
    /// Java 桥接 jar 的绝对路径。<see cref="Mode"/> 为 <see cref="InteropMode.WithJar"/> 时必需，
    /// 会自动追加到 JVM 的 <c>java.class.path</c>。
    /// </summary>
    public string? BridgeJarPath { get; set; }

    /// <summary>
    /// 是否在启动时自动初始化 Java 桥接类（<c>com.xsy.jn.Bridge</c>）。
    /// 仅在 <see cref="Mode"/> 为 <see cref="InteropMode.WithJar"/> 时生效。默认 <c>true</c>。
    /// </summary>
    public bool AutoInitializeBridge { get; set; } = true;
}

/// <summary>
/// 互操作模式。
/// </summary>
public enum InteropMode
{
    /// <summary>
    /// 纯 JNI+ASM 模式，无需 Java 桥接 jar。
    /// Java→C# 回调通过 <c>RegisterNatives</c> 实现，C#→Java 调用通过 JNI 实现。
    /// </summary>
    NativeOnly,

    /// <summary>
    /// 加载 Java 桥接 jar，提供 Java 侧的 .NET 对象代理与高级回调路由。
    /// 需提供 <see cref="JvmInteropOptions.BridgeJarPath"/>。
    /// </summary>
    WithJar,
}
