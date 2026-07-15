using Jvm.NET.Abstractions;

namespace Jvm.NET.Abstractions.Jdk25;

/// <summary>
/// Default <see cref="IJdkImplementation"/> for JDK 25 (LTS).
/// JNI 0x00180000 (JNI_VERSION_24 — JDK 25 未定义新常量，复用 24).
/// JVMTI 0x30190000 (0x30000000 + 25*0x10000).
/// </summary>
internal sealed class Jdk25Implementation : IJdkImplementation
{
    public int Version => 25;
    public int JniVersion => 0x00180000;
    public int JvmtiVersion => 0x30190000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
