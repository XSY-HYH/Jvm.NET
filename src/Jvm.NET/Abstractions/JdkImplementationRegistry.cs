using System.Collections.Concurrent;

namespace Jvm.NET.Abstractions;

/// <summary>
/// Global registry for <see cref="IJdkImplementation"/> instances.
/// </summary>
/// <remarks>
/// The core package auto-registers JDK 21-25 defaults via a <c>ModuleInitializer</c>.
/// Third-party packages can override any version by calling <see cref="Register"/>
/// before <see cref="JvmInitializer.Initialize"/>.
/// </remarks>
public static class JdkImplementationRegistry
{
    private static readonly ConcurrentDictionary<int, IJdkImplementation> s_registry = new();

    /// <summary>
    /// Registers or replaces the implementation for <paramref name="implementation"/>'s
    /// <see cref="IJdkImplementation.Version"/>.
    /// </summary>
    public static void Register(IJdkImplementation implementation)
    {
        if (implementation is null) throw new ArgumentNullException(nameof(implementation));
        s_registry[implementation.Version] = implementation;
    }

    /// <summary>
    /// Resolves the implementation for <paramref name="version"/>, or <c>null</c> if none registered.
    /// </summary>
    public static IJdkImplementation? Resolve(int version)
    {
        return s_registry.TryGetValue(version, out var impl) ? impl : null;
    }
}
