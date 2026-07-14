using Jvm.NET.Abstractions;

namespace Jvm.NET.Abstractions.Jdk23;

/// <summary>
/// Default <see cref="IJdkImplementation"/> for JDK 23.
/// Registers JNI version 0x00170000 and JVMTI version 0x30170000.
/// </summary>
internal sealed class Jdk23Implementation : IJdkImplementation
{
    public int Version => 23;
    public int JniVersion => 0x00170000;
    public int JvmtiVersion => 0x30170000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
