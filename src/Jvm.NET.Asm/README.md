# Jvm.NET.Asm

A C# port of the [ASM](https://asm.ow2.io/) bytecode manipulation framework. Parses, modifies and generates Java class files — no JVM required.

## Install

```xml
<PackageReference Include="Jvm.NET.Asm" Version="1.0.2" />
```

## Usage

### Read a class file

```csharp
using Jvm.NET.Asm;

var bytes = File.ReadAllBytes("Foo.class");
var reader = new ClassReader(bytes);
Console.WriteLine($"class: {reader.GetClassName()}");
Console.WriteLine($"super: {reader.GetSuperName()}");
```

### Modify and write back

```csharp
using Jvm.NET.Asm;

var reader = new ClassReader(File.ReadAllBytes("Foo.class"));
var writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);

reader.Accept(new ClassVisitor(writer)
{
    // Override VisitMethod to intercept each method
    public override MethodVisitor? VisitMethod(int access, string name, string descriptor,
        string? signature, string[]? exceptions)
    {
        var mv = base.VisitMethod(access, name, descriptor, signature, exceptions);
        if (name == "compute" && mv is not null)
            return new MyMethodTransformer(mv);
        return mv;
    }
}, 0);

File.WriteAllBytes("Foo.class", writer.ToByteArray());
```

### Generate a class from scratch

```csharp
using Jvm.NET.Asm;

var writer = new ClassWriter(ClassWriter.COMPUTE_FRAMES);
writer.Visit(Opcodes.V1_8, Opcodes.ACC_PUBLIC, "HelloGen", null, "java/lang/Object", null);

var mv = writer.VisitMethod(Opcodes.ACC_PUBLIC | Opcodes.ACC_STATIC,
    "main", "([Ljava/lang/String;)V", null, null);
mv.VisitFieldInsn(Opcodes.GETSTATIC, "java/lang/System", "out", "Ljava/io/PrintStream;");
mv.VisitLdcInsn("Hello from generated class!");
mv.VisitMethodInsn(Opcodes.INVOKEVIRTUAL, "java/io/PrintStream", "println", "(Ljava/lang/String;)V");
mv.VisitInsn(Opcodes.RETURN);
mv.VisitMaxs(2, 1);
mv.VisitEnd();

writer.VisitEnd();
File.WriteAllBytes("HelloGen.class", writer.ToByteArray());
```

## License

GPL-3.0-only © XSY_xiaoqi
