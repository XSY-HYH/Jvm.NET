# Jvm.NET.SourceGenerator

Source Generator for [Jvm.NET](https://github.com/XSY-HYH/Jvm.NET). Scans `partial` classes marked with `[JavaClass]` and auto-generates strongly-typed C# wrappers that call into Java via JNI — no boilerplate.

## Install

```xml
<ItemGroup>
  <PackageReference Include="Jvm.NET" Version="2.1.0" />
  <PackageReference Include="Jvm.NET.SourceGenerator" Version="2.1.0"
                    PrivateAssets="all" OutputItemType="Analyzer" />
</ItemGroup>
```

## Usage

Declare a `partial` class that extends `JavaObject` and mark it with `[JavaClass]`. Use `[JavaMethod]` / `[JavaField]` on `partial` members — the generator emits the JNI call implementations automatically.

```csharp
using Jvm.NET.Abstractions;

[JavaClass("java.util.ArrayList")]
public sealed partial class JavaArrayList : JavaObject
{
    [JavaMethod("add", "(Ljava/lang/Object;)Z")]
    public partial bool Add(object? item);

    [JavaMethod("size", "()I")]
    public partial int Count();

    [JavaMethod("get", "(I)Ljava/lang/Object;")]
    public partial object? Get(int index);

    [JavaField("modCount", "I")]
    public partial int ModificationCount { get; }
}
```

Then use it like a regular .NET object:

```csharp
var list = JavaArrayList.Create<JavaArrayList>(runtime.Invoker, "()V");
list.Add("hello");
list.Add("world");
Console.WriteLine($"count={list.Count}, first={list.Get(0)}");
```

## Attributes

| Attribute | Target | Description |
| --- | --- |
| `[JavaClass(name)]` | Class | Maps the C# class to a Java class (dot-separated name). |
| `[JavaMethod(name, signature)]` | Method | Maps a `partial` method to a Java method. Set `IsStatic = true` for static methods. |
| `[JavaField(name, signature)]` | Property | Maps a `partial` property to a Java field. Set `IsStatic = true` for static fields. |

## License

LGPL-3.0-only © XSY_xiaoqi
