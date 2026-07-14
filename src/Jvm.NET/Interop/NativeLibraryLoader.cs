using System.Runtime.InteropServices;

namespace Jvm.NET.Interop;

/// <summary>
/// Abstraction over <see cref="NativeLibrary"/> so that loading logic can be
/// replaced in unit tests (e.g. by a fake that returns pre-baked function pointers).
/// </summary>
internal interface INativeLibraryLoader
{
    /// <summary>Loads the JVM shared library from <paramref name="jdkBinPath"/> and returns a stable handle.</summary>
    IntPtr Load(string jdkBinPath);

    /// <summary>Resolves a function pointer by name from the previously loaded handle.</summary>
    IntPtr GetExport(IntPtr handle, string name);

    /// <summary>Frees the handle returned by <see cref="Load"/>. Safe to call multiple times.</summary>
    void Free(IntPtr handle);
}

/// <summary>
/// Default <see cref="INativeLibraryLoader"/> backed by <see cref="NativeLibrary"/>.
/// Uses <see cref="NativeLibrary.Load(string, Assembly, DllImportSearchPath?)"/> so that
/// the search path is constrained to the caller-supplied JDK bin directory.
/// </summary>
internal sealed class NativeLibraryLoader : INativeLibraryLoader
{
    public static readonly NativeLibraryLoader Instance = new();

    public IntPtr Load(string jdkBinPath)
    {
        if (string.IsNullOrWhiteSpace(jdkBinPath))
            throw new ArgumentException("JDK bin path must be provided.", nameof(jdkBinPath));

        if (!Directory.Exists(jdkBinPath))
            throw new DirectoryNotFoundException($"JDK bin directory not found: {jdkBinPath}");

        // The native loader expects the directory on the platform's search path.
        // We add the JDK bin dir to PATH for the duration of the process so that
        // jvm.dll's transitive dependencies (e.g. java.dll, jimage.dll) resolve too.
        PrependToPath(jdkBinPath);

        var libraryName = Path.GetFileNameWithoutExtension(NativeLibraryNames.ForCurrentPlatform);
        if (NativeLibrary.TryLoad(libraryName, typeof(NativeLibraryLoader).Assembly, DllImportSearchPath.UserDirectories | DllImportSearchPath.ApplicationDirectory, out var handle))
            return handle;

        // Fall back to loading by absolute path (covers non-standard layouts such as
        // Linux server/jre/lib/server/libjvm.so).
        var candidate = Path.Combine(jdkBinPath, NativeLibraryNames.ForCurrentPlatform);
        if (NativeLibrary.TryLoad(candidate, out handle))
            return handle;

        // JDK 标准布局：Windows 上 jvm.dll 在 bin\server\，Linux/macOS 上 libjvm.so 在 lib/server/。
        // 由于我们只拿到 bin 路径，先尝试 bin\server\；找不到再尝试同级 lib\server\。
        var serverCandidate = Path.Combine(jdkBinPath, "server", NativeLibraryNames.ForCurrentPlatform);
        if (NativeLibrary.TryLoad(serverCandidate, out handle))
            return handle;

        var libServerCandidate = Path.Combine(Path.GetDirectoryName(jdkBinPath) ?? string.Empty, "lib", "server", NativeLibraryNames.ForCurrentPlatform);
        if (NativeLibrary.TryLoad(libServerCandidate, out handle))
            return handle;

        throw new DllNotFoundException(
            $"Failed to load native JVM library '{NativeLibraryNames.ForCurrentPlatform}' " +
            $"from '{jdkBinPath}'. Verify that the path points at a valid JDK bin directory.");
    }

    public IntPtr GetExport(IntPtr handle, string name)
    {
        if (handle == IntPtr.Zero)
            throw new ArgumentNullException(nameof(handle));
        if (NativeLibrary.TryGetExport(handle, name, out var addr))
            return addr;
        throw new EntryPointNotFoundException($"Export '{name}' not found in native JVM library.");
    }

    public void Free(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
            NativeLibrary.Free(handle);
    }

    private static void PrependToPath(string jdkBinPath)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (path.Contains(jdkBinPath, StringComparison.OrdinalIgnoreCase))
            return;
        Environment.SetEnvironmentVariable("PATH", jdkBinPath + Path.PathSeparator + path);
    }
}
