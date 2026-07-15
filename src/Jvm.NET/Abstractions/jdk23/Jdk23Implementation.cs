using Jvm.NET.Abstractions;

namespace Jvm.NET.Abstractions.Jdk23;

/// <summary>
/// Default <see cref="IJdkImplementation"/> for JDK 23.
/// JNI 0x00150000 (JNI_VERSION_21 — JDK 23 未定义新常量，复用 21).
/// JVMTI 0x30170000 (0x30000000 + 23*0x10000).
/// </summary>
internal sealed class Jdk23Implementation : IJdkImplementation
{
    public int Version => 23;
    public int JniVersion => 0x00150000;
    public int JvmtiVersion => 0x30170000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
