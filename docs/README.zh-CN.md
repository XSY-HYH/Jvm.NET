# Jvm.NET 中文说明

Jvm.NET 是一个 .NET 类库，可在当前进程内嵌入 JVM 并从 C# 侧直接驱动它：
按指定 jar 路径+参数启动 jar、运行时修改字节码、监听 Java 方法执行事件与原生 VM 事件。

> 首个正式版，目标为 **OpenJDK 21 (LTS)**。

## 主要特性

- **进程内 JVM** —— 直接加载 `libjvm.so` / `jvm.dll` / `libjvm.dylib`，不启动 `java` 子进程。
- **方法调用** —— 加载 jar、查找类、调用静态 / 实例方法、运行 `main(String[])`。
- **字节码改写** —— 通过 JVMTI 的 `ClassFileLoadHook` / `RetransformClasses` / `RedefineClasses` 注册 `IBytecodeTransformer`。内置 C# 移植的 ASM 核心库，支持 class 文件解析与生成（含不等长字符串替换、常量池重建）。
- **事件监听** —— 订阅 `MethodEntry`、`MethodExit`、`Exception`、`ClassLoad`、`ClassPrepare`、`ThreadStart/End`、`VMInit/Death`。其中 MethodEntry/MethodExit/Exception 通过字节码插桩模拟（方案 B），绕过嵌入式 JVM 的 onload-only capability 限制。
- **版本抽象层** —— 每个 JDK 版本独立放在 `Abstractions/jdkXX/` 下，对外只暴露统一的 `IJvmRuntime`，屏蔽各版本 JVMTI 差异。
- **跨平台** —— 支持 `net8.0` / `net9.0` / `net10.0`，Windows / Linux / macOS。

## 安装

```bash
dotnet add package Jvm.NET
```

## 快速上手

```csharp
using Jvm.NET;
using Jvm.NET.Abstractions;

var runtime = JvmInitializer.Initialize(new JvmInitializationOptions
{
    JdkBinPath = @"C:\jdk-21\bin",     // 或 /usr/lib/jvm/jdk-21/bin
    Version    = JdkVersion.Jdk21,
    Classpath  = ["/opt/myapp/app.jar"],
});

// 在当前进程内运行 jar 的 main(String[])
runtime.Invoker.RunMain("/opt/myapp/app.jar", "com.example.App", "--verbose", "input.txt");

// 监听每一次方法进入（通过字节码插桩）
runtime.EventListener.SubscribeMethodEntry(e =>
    Console.WriteLine($"-> {e.Class.Name}.{e.MethodName}"));

// 监听插桩方法内抛出的异常
runtime.EventListener.SubscribeException(e =>
    Console.WriteLine($"ex in {e.ThrowingClass?.Name}.{e.MethodName}: {e.Exception.Class.Name}"));

// 在类加载时改写字节码
using var token = runtime.BytecodeModifier.RegisterTransformer(new MyTransformer());
runtime.BytecodeModifier.RetransformClasses([runtime.Invoker.LoadClass("com.example.App")]);
```

## 文档

- [英文说明](../README.md)
- [中文 API 参考](API.zh-CN.md)
- [英文 API 参考](API.md)

## 许可证

MIT © XSY_xiaoqi
