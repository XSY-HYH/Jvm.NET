using Jvm.NET.Abstractions;

namespace Jvm.NET.Abstractions.Jdk24;

/// <summary>
/// Default <see cref="IJdkImplementation"/> for JDK 24.
/// Registers JNI version 0x00180000 and JVMTI version 0x30180000.
/// </summary>
internal sealed class Jdk24Implementation : IJdkImplementation
{
    public int Version => 24;
    public int JniVersion => 0x00180000;
    public int JvmtiVersion => 0x30180000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
