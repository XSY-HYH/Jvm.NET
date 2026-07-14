using Jvm.NET.Abstractions;

namespace Jvm.NET.Abstractions.Jdk21;

/// <summary>
/// Default <see cref="IJdkImplementation"/> for JDK 21 (LTS).
/// Registers JNI version 0x00150000 and JVMTI version 0x30150000.
/// </summary>
internal sealed class Jdk21Implementation : IJdkImplementation
{
    public int Version => 21;
    public int JniVersion => 0x00150000;
    public int JvmtiVersion => 0x30150000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
