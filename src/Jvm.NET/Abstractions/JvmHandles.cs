namespace Jvm.NET.Abstractions;

/// <summary>
/// Handle to a JVM class loaded through <see cref="IJvmInvoker.FindClass"/> or
/// <see cref="IJvmInvoker.LoadClass"/>. Wraps the raw <c>jclass</c> pointer and
/// exposes the fully-qualified name (e.g. <c>java.lang.String</c>).
/// </summary>
/// <remarks>
/// 当通过 <see cref="IJvmInvoker.FindClass"/> / <see cref="IJvmInvoker.LoadClass"/> 创建时，
/// 句柄是 JNI 全局引用，<see cref="Dispose"/> 会释放它。如果通过公共构造函数创建，
/// 则不拥有句柄，<see cref="Dispose"/> 是空操作。
/// </remarks>
public sealed class JvmClass : IDisposable
{
    /// <summary>Raw <c>jclass</c> handle. 在 <see cref="Dispose"/> 之前有效。</summary>
    public IntPtr Handle { get; private set; }

    /// <summary>Fully-qualified class name using dots (e.g. <c>java.lang.String</c>).</summary>
    public string Name { get; }

    /// <summary>是否拥有句柄（Dispose 时会释放全局引用）。</summary>
    public bool OwnsHandle => _release != null;

    private Action<IntPtr>? _release;

    /// <summary>创建不拥有句柄的包装（向后兼容）。</summary>
    public JvmClass(IntPtr handle, string name)
    {
        Handle = handle;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>创建拥有句柄的包装，Dispose 时调用 <paramref name="release"/> 释放。</summary>
    internal JvmClass(IntPtr handle, string name, Action<IntPtr> release)
    {
        Handle = handle;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _release = release;
    }

    public void Dispose()
    {
        var release = _release;
        if (release != null && Handle != IntPtr.Zero)
        {
            release(Handle);
            _release = null;
            Handle = IntPtr.Zero;
        }
    }
}

/// <summary>
/// Handle to a JVM object instance obtained from <see cref="IJvmInvoker.NewObject"/>
/// or returned from a method call. Wraps the raw <c>jobject</c> pointer and the
/// <see cref="JvmClass"/> of its runtime type.
/// </summary>
/// <remarks>
/// 当通过 <see cref="IJvmInvoker.NewObject"/> 创建时，句柄是 JNI 全局引用，
/// <see cref="Dispose"/> 会释放它。如果通过公共构造函数创建，或从
/// <see cref="IJvmInvoker.InvokeVirtual"/> / <see cref="IJvmInvoker.InvokeStatic"/>
/// 返回值包装（这些返回的是局部引用），则不拥有句柄。
/// </remarks>
public sealed class JvmObject : IDisposable
{
    /// <summary>Raw <c>jobject</c> handle. 在 <see cref="Dispose"/> 之前有效。</summary>
    public IntPtr Handle { get; private set; }

    /// <summary>Runtime class of the instance.</summary>
    public JvmClass Class { get; }

    /// <summary>是否拥有句柄（Dispose 时会释放全局引用）。</summary>
    public bool OwnsHandle => _release != null;

    private Action<IntPtr>? _release;

    /// <summary>创建不拥有句柄的包装（向后兼容）。</summary>
    public JvmObject(IntPtr handle, JvmClass clazz)
    {
        Handle = handle;
        Class = clazz ?? throw new ArgumentNullException(nameof(clazz));
    }

    /// <summary>创建拥有句柄的包装，Dispose 时调用 <paramref name="release"/> 释放。</summary>
    internal JvmObject(IntPtr handle, JvmClass clazz, Action<IntPtr> release)
    {
        Handle = handle;
        Class = clazz ?? throw new ArgumentNullException(nameof(clazz));
        _release = release;
    }

    public void Dispose()
    {
        var release = _release;
        if (release != null && Handle != IntPtr.Zero)
        {
            release(Handle);
            _release = null;
            Handle = IntPtr.Zero;
        }
    }
}
