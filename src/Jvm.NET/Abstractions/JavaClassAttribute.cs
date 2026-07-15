namespace Jvm.NET.Abstractions;

/// <summary>
/// 标记一个 C# 类对应的 Java 类名。Source Generator 会读取此特性生成强类型封装。
/// 手写封装时也可使用此特性，<see cref="JavaObject"/> 会自动读取。
/// </summary>
/// <example>
/// <code>
/// [JavaClass("java.util.ArrayList")]
/// public sealed class ArrayList : JavaObject { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class JavaClassAttribute : Attribute
{
    /// <summary>Java 类全名（点分隔，如 <c>java.util.ArrayList</c>）。</summary>
    public string Name { get; }

    public JavaClassAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}

/// <summary>
/// 标记一个 C# 方法对应的 Java 方法。Source Generator 会读取此特性生成调用代码。
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class JavaMethodAttribute : Attribute
{
    /// <summary>Java 方法名。</summary>
    public string Name { get; }

    /// <summary>JNI 方法签名（如 <c>(I)V</c>）。</summary>
    public string Signature { get; }

    /// <summary>是否为静态方法。默认 false。</summary>
    public bool IsStatic { get; set; }

    public JavaMethodAttribute(string name, string signature)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Signature = signature ?? throw new ArgumentNullException(nameof(signature));
    }
}

/// <summary>
/// 标记一个 C# 字段/属性对应的 Java 字段。Source Generator 会读取此特性生成访问代码。
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class JavaFieldAttribute : Attribute
{
    /// <summary>Java 字段名。</summary>
    public string Name { get; }

    /// <summary>JNI 字段类型签名（如 <c>I</c>、<c>Ljava/lang/String;</c>）。</summary>
    public string Signature { get; }

    /// <summary>是否为静态字段。默认 false。</summary>
    public bool IsStatic { get; set; }

    public JavaFieldAttribute(string name, string signature)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Signature = signature ?? throw new ArgumentNullException(nameof(signature));
    }
}
