using System.Reflection;

namespace Jvm.NET.Abstractions;

/// <summary>
/// Java 对象的 .NET 包装基类。用户继承此类手写 Java 类的 C# 封装，
/// 或通过 Source Generator 自动生成。
/// </summary>
/// <remarks>
/// 使用方式：
/// <list type="number">
/// <item>继承 <see cref="JavaObject"/>，用 <see cref="JavaClassAttribute"/> 标记 Java 类名。</item>
/// <item>通过 <see cref="Create"/> 工厂方法创建新实例，或 <see cref="Wrap"/> 包装已有对象。</item>
/// <item>通过 <see cref="Invoke"/> / <see cref="GetField"/> / <see cref="SetField"/> 等方法操作对象。</item>
/// </list>
/// </remarks>
public abstract class JavaObject : IDisposable
{
    private IJvmInvoker? _invoker;
    private JvmObject? _handle;
    private JvmClass? _cachedClass;

    /// <summary>
    /// Java 类全名（点分隔）。默认从 <see cref="JavaClassAttribute"/> 读取。
    /// 子类可以重写此属性提供硬编码值。
    /// </summary>
    protected virtual string JavaClassName
    {
        get
        {
            var attr = GetType().GetCustomAttribute<JavaClassAttribute>();
            if (attr is null)
                throw new NotImplementedException(
                    $"{GetType().Name} 必须重写 JavaClassName 或使用 [JavaClass] 特性标记 Java 类名。");
            return attr.Name;
        }
    }

    /// <summary>包装的 Java 对象句柄。未初始化时抛出异常。</summary>
    public JvmObject Handle => _handle ?? throw CreateNotInitialized();

    /// <summary>关联的 <see cref="IJvmInvoker"/>。未初始化时抛出异常。</summary>
    public IJvmInvoker Invoker => _invoker ?? throw CreateNotInitialized();

    /// <summary>缓存的 Java Class 对象（延迟加载）。</summary>
    protected JvmClass JavaClass => _cachedClass ??= Invoker.LoadClass(JavaClassName);

    private Exception CreateNotInitialized()
        => new InvalidOperationException("对象未初始化，请通过 Create 或 Wrap 方法初始化。");

    /// <summary>绑定已有的 Java 对象和 Invoker。</summary>
    protected void Attach(JvmObject obj, IJvmInvoker invoker)
    {
        _handle = obj ?? throw new ArgumentNullException(nameof(obj));
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
    }

    // ---- 工厂方法 ----

    /// <summary>
    /// 创建 T 的新实例：加载 Java 类、调用构造函数、返回包装对象。
    /// </summary>
    /// <typeparam name="T">C# 包装类型。</typeparam>
    /// <param name="invoker">JVM Invoker。</param>
    /// <param name="constructorSignature">构造函数的 JNI 签名（如 <c>()V</c>）。</param>
    /// <param name="args">构造参数。</param>
    public static T Create<T>(IJvmInvoker invoker, string constructorSignature, params JvmValue[] args)
        where T : JavaObject, new()
    {
        var obj = new T();
        var clazz = invoker.LoadClass(obj.JavaClassName);
        var jobj = invoker.NewObject(clazz, constructorSignature, args);
        obj.Attach(jobj, invoker);
        return obj;
    }

    /// <summary>
    /// 包装一个已有的 Java 对象。
    /// </summary>
    public static T Wrap<T>(IJvmInvoker invoker, JvmObject obj) where T : JavaObject, new()
    {
        var result = new T();
        result.Attach(obj, invoker);
        return result;
    }

    // ---- 实例方法调用 ----

    /// <summary>调用实例方法。</summary>
    protected JvmValue Invoke(string methodName, string signature, params JvmValue[] args)
        => Invoker.InvokeVirtual(Handle, methodName, signature, args);

    /// <summary>调用实例方法并返回指定类型。</summary>
    protected T? Invoke<T>(string methodName, string signature, params JvmValue[] args)
    {
        var v = Invoke(methodName, signature, args);
        return TypeMapper.ToClr<T>(v, Invoker);
    }

    // ---- 静态方法调用 ----

    /// <summary>调用静态方法。</summary>
    protected JvmValue InvokeStatic(string methodName, string signature, params JvmValue[] args)
        => Invoker.InvokeStatic(JavaClass, methodName, signature, args);

    /// <summary>调用静态方法并返回指定类型。</summary>
    protected T? InvokeStatic<T>(string methodName, string signature, params JvmValue[] args)
    {
        var v = InvokeStatic(methodName, signature, args);
        return TypeMapper.ToClr<T>(v, Invoker);
    }

    // ---- 字段访问 ----

    /// <summary>读取实例字段。</summary>
    protected JvmValue GetField(string name, string signature)
        => Invoker.GetField(Handle, name, signature);

    /// <summary>读取实例字段并返回指定类型。</summary>
    protected T? GetField<T>(string name, string signature)
        => TypeMapper.ToClr<T>(GetField(name, signature), Invoker);

    /// <summary>写入实例字段。</summary>
    protected void SetField(string name, string signature, JvmValue value)
        => Invoker.SetField(Handle, name, signature, value);

    /// <summary>读取静态字段。</summary>
    protected JvmValue GetStaticField(string name, string signature)
        => Invoker.GetStaticField(JavaClass, name, signature);

    /// <summary>写入静态字段。</summary>
    protected void SetStaticField(string name, string signature, JvmValue value)
        => Invoker.SetStaticField(JavaClass, name, signature, value);

    // ---- 类型检查 ----

    /// <summary>检查此对象是否是指定 Java 类的实例。</summary>
    public bool IsInstanceOf(string javaClassName)
    {
        var clazz = Invoker.LoadClass(javaClassName);
        return Invoker.IsInstanceOf(Handle, clazz);
    }

    // ---- 生命周期 ----

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
        _cachedClass = null;
        GC.SuppressFinalize(this);
    }
}
