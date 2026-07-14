using Jvm.NET.Abstractions;

namespace Jvm.NET.Abstractions.Jdk25;

/// <summary>
/// Default <see cref="IJdkImplementation"/> for JDK 25 (LTS).
/// Registers JNI version 0x00190000 and JVMTI version 0x30190000.
/// </summary>
internal sealed class Jdk25Implementation : IJdkImplementation
{
    public int Version => 25;
    public int JniVersion => 0x00190000;
    public int JvmtiVersion => 0x30190000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
