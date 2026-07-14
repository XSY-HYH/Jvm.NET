using System.Runtime.CompilerServices;
using Jvm.NET.Abstractions.Jdk21;
using Jvm.NET.Abstractions.Jdk22;
using Jvm.NET.Abstractions.Jdk23;
using Jvm.NET.Abstractions.Jdk24;
using Jvm.NET.Abstractions.Jdk25;

namespace Jvm.NET.Abstractions;

/// <summary>
/// Registers the built-in <see cref="IJdkImplementation"/> objects for JDK 21-25
/// when this assembly loads. Third-party packages can register their own
/// implementations (overriding any version) by calling
/// <see cref="JdkImplementationRegistry.Register"/> before
/// <see cref="JvmInitializer.Initialize"/>.
/// </summary>
internal static class JdkImplementationRegistration
{
    // CA2255: ModuleInitializer 在类库中使用是有意为之——保证第三方包在
    // 引用本程序集后无需手动调用 Register 即可使用默认 JDK 21-25 实现。
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void RegisterDefaultImplementations()
    {
        JdkImplementationRegistry.Register(new Jdk21Implementation());
        JdkImplementationRegistry.Register(new Jdk22Implementation());
        JdkImplementationRegistry.Register(new Jdk23Implementation());
        JdkImplementationRegistry.Register(new Jdk24Implementation());
        JdkImplementationRegistry.Register(new Jdk25Implementation());
    }
}
