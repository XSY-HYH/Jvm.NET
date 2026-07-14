namespace Jvm.NET.Abstractions;

/// <summary>
/// Hooks into the JVM's <c>ClassFileLoadHook</c> / <c>RetransformClasses</c> path
/// so callers can rewrite bytecode before (or after) a class is linked.
/// Requires JVMTI to be enabled at startup (<see cref="JvmInitializationOptions.EnableBytecodeModification"/>).
/// </summary>
public interface IBytecodeModifier
{
    /// <summary>
    /// Registers a transformer that will be invoked for every class load / retransform
    /// from this point on. The returned <see cref="IDisposable"/> removes the transformer
    /// when disposed (use a <c>using</c> block).
    /// </summary>
    IDisposable RegisterTransformer(IBytecodeTransformer transformer);

    /// <summary>
    /// Triggers JVMTI <c>RetransformClasses</c> for the given classes, which causes
    /// all registered transformers (including those added by other agents) to be
    /// re-invoked with the current bytecode.
    /// </summary>
    void RetransformClasses(IEnumerable<JvmClass> classes);

    /// <summary>
    /// Triggers JVMTI <c>RedefineClasses</c> with caller-supplied bytecode. This bypasses
    /// transformers and replaces the class body directly. Use with care.
    /// </summary>
    void RedefineClasses(IEnumerable<KeyValuePair<JvmClass, byte[]>> redefinitions);
}

/// <summary>
/// Callback invoked by <see cref="IBytecodeModifier"/> for every class being loaded
/// or retransformed. Returning <c>null</c> leaves the bytecode untouched.
/// </summary>
public interface IBytecodeTransformer
{
    /// <summary>Human-readable name used in diagnostics / error logs.</summary>
    string Name { get; }

    /// <summary>
    /// Inspect / rewrite the class bytes. Called on a JVMTI-managed thread.
    /// </summary>
    /// <param name="className">Fully-qualified name of the class being loaded, dots-separated.</param>
    /// <param name="originalBytes">Bytes as the JVM currently sees them (post-previous-transformer).</param>
    /// <returns>New bytecode to install, or <c>null</c> to keep <paramref name="originalBytes"/>.</returns>
    byte[]? Transform(string className, byte[] originalBytes);
}
