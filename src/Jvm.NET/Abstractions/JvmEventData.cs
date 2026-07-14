namespace Jvm.NET.Abstractions;

/// <summary>
/// Base class for all JVM event payloads delivered through <see cref="IJvmEventListener"/>.
/// Carries the JVMTI event kind so subscribers can route dynamically.
/// </summary>
public abstract class JvmEventData
{
    /// <summary>JVMTI event kind enum value (see <c>jvmtiEvent</c> in <c>jvmti.h</c>).</summary>
    public int EventKind { get; init; }

    /// <summary>Wall-clock timestamp (UTC) when the event was captured by the native side.</summary>
    public DateTimeOffset TimestampUtc { get; init; }
}

public sealed class MethodEntryEventData : JvmEventData
{
    public required JvmClass Class { get; init; }
    public required string MethodName { get; init; }
    public required string MethodSignature { get; init; }
}

public sealed class MethodExitEventData : JvmEventData
{
    public required JvmClass Class { get; init; }
    public required string MethodName { get; init; }
    public required string MethodSignature { get; init; }
    public required JvmValue ReturnValue { get; init; }
    public bool WasException { get; init; }
}

public sealed class ClassLoadEventData : JvmEventData
{
    public required JvmClass Class { get; init; }
}

public sealed class ClassPrepareEventData : JvmEventData
{
    public required JvmClass Class { get; init; }
}

public sealed class ThreadStartEventData : JvmEventData
{
    public required IntPtr ThreadHandle { get; init; }
    public required string ThreadName { get; init; }
}

public sealed class ThreadEndEventData : JvmEventData
{
    public required IntPtr ThreadHandle { get; init; }
    public required string ThreadName { get; init; }
}

public sealed class VmInitEventData : JvmEventData { }
public sealed class VmDeathEventData : JvmEventData { }

public sealed class ExceptionEventData : JvmEventData
{
    public required JvmObject Exception { get; init; }
    public JvmClass? ThrowingClass { get; init; }
    public string? MethodName { get; init; }
    public string? MethodSignature { get; init; }
}
