namespace Jvm.NET.Abstractions;

/// <summary>
/// Pluggable abstraction for a JDK-version-specific runtime implementation.
/// </summary>
/// <remarks>
/// <para>
/// The core <c>Jvm.NET</c> package ships default implementations for JDK 21-25.
/// Third-party packages can provide their own <see cref="IJdkImplementation"/> (e.g. for
/// a custom JDK distribution or a future JDK version) and register it via
/// <see cref="JdkImplementationRegistry.Register"/> before calling
/// <see cref="JvmInitializer.Initialize"/>.
/// </para>
/// <para>
/// Version number layout (since JDK 9):
/// <list type="bullet">
/// <item><c>JniVersion</c>  = <c>major &lt;&lt; 16</c>                (e.g. JDK 21 → 0x00150000)</item>
/// <item><c>JvmtiVersion</c> = <c>0x30000000 | (major &lt;&lt; 16)</c>   (e.g. JDK 21 → 0x30150000)</item>
/// </list>
/// </para>
/// </remarks>
public interface IJdkImplementation
{
    /// <summary>The JDK major version this implementation targets (e.g. 21, 22, 8).</summary>
    int Version { get; }

    /// <summary>JNI version constant for <see cref="Version"/> (e.g. 0x00150000 for JDK 21).</summary>
    int JniVersion { get; }

    /// <summary>JVMTI version constant for <see cref="Version"/> (e.g. 0x30150000 for JDK 21).</summary>
    int JvmtiVersion { get; }

    /// <summary>
    /// Creates a new <see cref="IJvmRuntime"/> instance for this JDK version.
    /// The caller is responsible for calling <see cref="IJvmRuntime.Start"/>.
    /// </summary>
    IJvmRuntime CreateRuntime(JvmInitializationOptions options);
}
