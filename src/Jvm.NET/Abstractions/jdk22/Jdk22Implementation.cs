using Jvm.NET.Abstractions;

namespace Jvm.NET.Abstractions.Jdk22;

/// <summary>
/// Default <see cref="IJdkImplementation"/> for JDK 22.
/// JNI 0x00150000 (JNI_VERSION_21 — JDK 22 未定义新常量，复用 21).
/// JVMTI 0x30160000 (0x30000000 + 22*0x10000).
/// </summary>
internal sealed class Jdk22Implementation : IJdkImplementation
{
    public int Version => 22;
    public int JniVersion => 0x00150000;
    public int JvmtiVersion => 0x30160000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
