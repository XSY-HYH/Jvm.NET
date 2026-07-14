namespace Jvm.NET.Abstractions;

/// <summary>
/// Handle to a JVM class loaded through <see cref="IJvmInvoker.FindClass"/> or
/// <see cref="IJvmInvoker.LoadClass"/>. Wraps the raw <c>jclass</c> pointer and
/// exposes the fully-qualified name (e.g. <c>java.lang.String</c>).
/// </summary>
public sealed class JvmClass
{
    /// <summary>Raw <c>jclass</c> handle. Callers must NOT free it.</summary>
    public IntPtr Handle { get; }

    /// <summary>Fully-qualified class name using dots (e.g. <c>java.lang.String</c>).</summary>
    public string Name { get; }

    public JvmClass(IntPtr handle, string name)
    {
        Handle = handle;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

/// <summary>
/// Handle to a JVM object instance obtained from <see cref="IJvmInvoker.NewObject"/>
/// or returned from a method call. Wraps the raw <c>jobject</c> pointer and the
/// <see cref="JvmClass"/> of its runtime type.
/// </summary>
public sealed class JvmObject
{
    /// <summary>Raw <c>jobject</c> handle. The wrapper does NOT own it; lifetime is managed by the JVM.</summary>
    public IntPtr Handle { get; }

    /// <summary>Runtime class of the instance.</summary>
    public JvmClass Class { get; }

    public JvmObject(IntPtr handle, JvmClass clazz)
    {
        Handle = handle;
        Class = clazz ?? throw new ArgumentNullException(nameof(clazz));
    }
}
