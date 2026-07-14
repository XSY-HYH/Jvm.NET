using Jvm.NET.Abstractions;

namespace Jvm.NET.Abstractions.Jdk22;

/// <summary>
/// Default <see cref="IJdkImplementation"/> for JDK 22.
/// Registers JNI version 0x00160000 and JVMTI version 0x30160000.
/// </summary>
internal sealed class Jdk22Implementation : IJdkImplementation
{
    public int Version => 22;
    public int JniVersion => 0x00160000;
    public int JvmtiVersion => 0x30160000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
