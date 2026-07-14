using System.Runtime.InteropServices;

namespace Jvm.NET.Interop;

/// <summary>
/// Resolves the native JVM shared library filename for the current platform:
/// <list type="bullet">
/// <item>Windows: <c>jvm.dll</c></item>
/// <item>Linux:   <c>libjvm.so</c></item>
/// <item>macOS:   <c>libjvm.dylib</c></item>
/// </list>
/// </summary>
internal static class NativeLibraryNames
{
    public const string Windows = "jvm.dll";
    public const string Linux = "libjvm.so";
    public const string MacOS = "libjvm.dylib";

    public static string ForCurrentPlatform =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Windows :
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Linux :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? MacOS :
        throw new PlatformNotSupportedException($"Jvm.NET does not yet support {RuntimeInformation.OSDescription}");
}
