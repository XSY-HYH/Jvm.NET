using Jvm.NET.Abstractions;
using Jvm.NET.Abstractions.Jdk21;
using Jvm.NET.Interop;

namespace Jvm.NET;

/// <summary>
/// The single public entry point for Jvm.NET. Bootstraps an in-process JVM
/// for the requested <see cref="JvmInitializationOptions.Version"/> and returns
/// the version-agnostic <see cref="IJvmRuntime"/> facade.
/// </summary>
public static class JvmInitializer
{
    private static readonly object SyncLock = new();
    private static IJvmRuntime? _current;
    private static bool _bootstrapped;

    /// <summary>
    /// Initialises (or returns the already-running) embedded JVM.
    /// </summary>
    /// <remarks>
    /// The JVM is process-wide: a second call with a different
    /// <see cref="JvmInitializationOptions.Version"/> throws. Callers MUST
    /// dispose the returned runtime before re-initialising with a different version.
    /// </remarks>
    public static IJvmRuntime Initialize(JvmInitializationOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        ValidateOptions(options);

        lock (SyncLock)
        {
            if (_bootstrapped && _current is not null && _current.State != JvmRuntimeState.Stopped)
            {
                if (_current.Version != options.Version)
                    throw new InvalidOperationException(
                        $"A JVM of version '{_current.Version}' is already running in this process; " +
                        $"cannot re-initialise as '{options.Version}'.");
                return _current;
            }

            var loader = NativeLibraryLoader.Instance;
            IJvmRuntime runtime = options.Version switch
            {
                JdkVersion.Jdk21 => new Jdk21Runtime(options, loader),
                _ => throw new NotSupportedException($"JDK version '{options.Version}' is not supported yet."),
            };

            runtime.Start();
            _current = runtime;
            _bootstrapped = true;
            return runtime;
        }
    }

    /// <summary>The currently running JVM, or <c>null</c> if none has been started.</summary>
    public static IJvmRuntime? Current
    {
        get
        {
            lock (SyncLock)
            {
                return _current is { State: JvmRuntimeState.Running } ? _current : null;
            }
        }
    }

    private static void ValidateOptions(JvmInitializationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.JdkBinPath))
            throw new ArgumentException("JvmInitializationOptions.JdkBinPath must be set.", nameof(options));

        if (!Enum.IsDefined(options.Version))
            throw new ArgumentOutOfRangeException(nameof(options), options.Version, "Unsupported JDK version.");
    }
}
