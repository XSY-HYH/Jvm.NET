namespace Jvm.NET.Abstractions;

/// <summary>
/// Top-level facade for an embedded JVM running in the current process.
/// Implementations live under <c>Abstractions/jdkXX</c> and are selected by
/// <see cref="JvmInitializationOptions.Version"/> at startup.
/// </summary>
public interface IJvmRuntime : IDisposable
{
    /// <summary>Current lifecycle state of the runtime.</summary>
    JvmRuntimeState State { get; }

    /// <summary>The JDK version this runtime was initialised against.</summary>
    JdkVersion Version { get; }

    /// <summary>Accessor for invocation APIs. Throws if the runtime is not running.</summary>
    IJvmInvoker Invoker { get; }

    /// <summary>Accessor for bytecode modification APIs. Throws if bytecode modification was disabled.</summary>
    IBytecodeModifier BytecodeModifier { get; }

    /// <summary>Accessor for event-listening APIs. Throws if event listening was disabled.</summary>
    IJvmEventListener EventListener { get; }

    /// <summary>Boots the JVM with the options supplied at construction. Idempotent.</summary>
    void Start();

    /// <summary>Destroys the JVM. After this call the instance cannot be restarted.</summary>
    void Shutdown();
}

/// <summary>Lifecycle states of an <see cref="IJvmRuntime"/>.</summary>
public enum JvmRuntimeState
{
    NotStarted,
    Starting,
    Running,
    ShuttingDown,
    Stopped,
    Faulted,
}
