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
    /// Target JDK version. Used to select the matching abstraction layer under
    /// <c>Abstractions/jdkXX</c>.
    /// </summary>
    public required JdkVersion Version { get; set; }

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
}
