# Jvm.NET

A production-grade .NET library that embeds an in-process JVM and lets you drive it from C#:
launch jars with arguments, rewrite bytecode at runtime, and listen to Java method / VM events.

> First stable release targeting **OpenJDK 21 (LTS)**.

## Highlights

- **In-process JVM** — boots `libjvm.so` / `jvm.dll` / `libjvm.dylib` in the current process; no `java` subprocess.
- **Invoke anything** — load jars, find classes, call static / virtual methods, run `main(String[])`.
- **Bytecode rewriting** — register `IBytecodeTransformer` callbacks backed by JVMTI `ClassFileLoadHook` / `RetransformClasses` / `RedefineClasses`. Ships with a C# port of ASM core for class file parsing and emission (supports unequal-length string replacement, constant pool rebuild).
- **Event listening** — subscribe to `MethodEntry`, `MethodExit`, `Exception`, `ClassLoad`, `ClassPrepare`, `ThreadStart/End`, `VMInit/Death`. MethodEntry/MethodExit/Exception are simulated via bytecode instrumentation (方案 B) to bypass onload-only JVMTI capability restrictions of embedded JVMs.
- **Version abstraction** — each JDK version lives under `Abstractions/jdkXX/` and exposes a single `IJvmRuntime` facade, so callers don't deal with version-specific JVMTI quirks.
- **Cross-platform** — `net8.0` / `net9.0` / `net10.0` on Windows, Linux and macOS.

## Install

```bash
dotnet add package Jvm.NET
```

## Quick start

```csharp
using Jvm.NET;
using Jvm.NET.Abstractions;

var runtime = JvmInitializer.Initialize(new JvmInitializationOptions
{
    JdkBinPath = @"C:\jdk-21\bin",     // or /usr/lib/jvm/jdk-21/bin
    Version    = JdkVersion.Jdk21,
    Classpath  = ["/opt/myapp/app.jar"],
});

// Run the main(String[]) of a jar in-process
runtime.Invoker.RunMain("/opt/myapp/app.jar", "com.example.App", "--verbose", "input.txt");

// Listen to every method entry (via bytecode instrumentation)
runtime.EventListener.SubscribeMethodEntry(e =>
    Console.WriteLine($"-> {e.Class.Name}.{e.MethodName}"));

// Listen to exceptions thrown inside instrumented methods
runtime.EventListener.SubscribeException(e =>
    Console.WriteLine($"ex in {e.ThrowingClass?.Name}.{e.MethodName}: {e.Exception.Class.Name}"));

// Rewrite bytecode at load time
using var token = runtime.BytecodeModifier.RegisterTransformer(new MyTransformer());
runtime.BytecodeModifier.RetransformClasses([runtime.Invoker.LoadClass("com.example.App")]);
```

## Documentation

- [中文说明](docs/README.zh-CN.md)
- [API reference](docs/API.md)
- [中文 API 参考](docs/API.zh-CN.md)

## License

MIT © XSY_xiaoqi
