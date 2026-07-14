# 扩展 Jvm.NET —— 注册自定义 JDK 实现

`Jvm.NET` 内置支持 JDK 21-25。本文档说明第三方包如何注册自己的 `IJdkImplementation`
来支持其它 JDK 版本（例如 **JDK 8**），或覆盖已支持版本的默认行为。

---

## 注册机制

1. 核心包在 `Jvm.NET` 程序集加载时通过 `ModuleInitializer` 自动注册
   `Jdk21Implementation`..`Jdk25Implementation`。
2. `JvmInitializer.Initialize` 调用 `JdkImplementationRegistry.Resolve(options.Version)`
   查找实现。
3. 如果第三方包在 `Initialize` 之前为同一 `Version` 注册了实现，第三方实现优先
   （注册表是 `ConcurrentDictionary<int, IJdkImplementation>`，键为主版本号）。
4. 如果 `Resolve` 返回 `null`，`Initialize` 抛出 `NotSupportedException`，列出请求的版本号。

> `Version` 现在是普通 `int`（JDK 主版本号）。这样第三方包可以注册任意版本号，
> 无需等核心包扩展枚举。

---

## 三个扩展点

| 类型 | 作用 |
| --- | --- |
| `IJdkImplementation` | 契约：提供 `Version`、`JniVersion`、`JvmtiVersion` 与 `CreateRuntime` 工厂方法。 |
| `JdkImplementationRegistry` | `JvmInitializer` 查询的静态注册表。`Initialize` 前调用 `Register`。 |
| `JdkRuntimeBase` | 公共基类，通过参数化版本常量实现 `IJvmRuntime`。如果目标 JDK 与 JDK 21+ 的 JNI/JVMTI ABI 相同，直接复用即可。 |

完整签名参考 [API.zh-CN.md](API.zh-CN.md#jdk-版本抽象)。

---

## 快速开始：覆盖 JDK 21

最简单的例子——用一个自定义实现替换内置的 JDK 21 实现（例如增加日志或厂商特定选项）。

```csharp
using Jvm.NET.Abstractions;

internal sealed class MyJdk21Implementation : IJdkImplementation
{
    public int Version => 21;
    public int JniVersion => 0x00150000;
    public int JvmtiVersion => 0x30150000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
    {
        // 在这里加入你自己的日志 / 选项处理逻辑。
        return new JdkRuntimeBase(options, this);
    }
}

public static class MyPackageInitializer
{
    public static void Register()
    {
        // 因为 Version 都是 21，这会覆盖内置的 Jdk21Implementation。
        // 必须在 JvmInitializer.Initialize 之前调用。
        JdkImplementationRegistry.Register(new MyJdk21Implementation());
    }
}
```

---

## 完整示例：添加 JDK 8 支持

核心包不提供 JDK 8。第三方包可以补上。与 JDK 21+ 相比有两个重要差异：

1. **版本号布局在 JDK 9 改变。** JDK 9 之前使用旧的 `0x0001000X` 布局，不是现代的
   `major << 16` 布局。
2. **JVMTI 版本常量**也遵循旧布局。务必对照目标 JDK 自带的 `jvmti.h` 核对——
   下面的值来自 OpenJDK 8u。

### 第 1 步 —— 创建实现类

```csharp
using Jvm.NET.Abstractions;

namespace YourPackage.Jdk8;

internal sealed class Jdk8Implementation : IJdkImplementation
{
    // JDK 主版本号。注册表接受任意正整数。
    public int Version => 8;

    // jni.h 中的 JNI_VERSION_1_8（旧布局，不是 0x00080000）。
    public int JniVersion => 0x00010008;

    // jvmti.h 中的 JVMTI_VERSION_1_8（OpenJDK 8u 中为 0x80010008）。
    // 注意接口类型位是 0x80000000，与 JDK 21 的 0x30000000 不同。
    // 务必对照你目标 JDK 的 jvmti.h 核对。
    public int JvmtiVersion => 0x80010008;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
```

### 第 2 步 —— 在 `Initialize` 之前注册

两种常见模式：

**A. 库侧辅助方法** —— 让消费者显式调用：

```csharp
public static class JvmNetJdk8
{
    public static void Use()
        => JdkImplementationRegistry.Register(new Jdk8Implementation());
}
```

```csharp
// 在应用启动时：
JvmNetJdk8.Use();

var runtime = JvmInitializer.Initialize(new JvmInitializationOptions
{
    JdkBinPath = @"C:\Program Files\Java\jdk-8\bin",
    Version    = 8,
});
```

**B. 模块初始化器** —— 你的包加载时自动注册。使用静态构造函数或 `[ModuleInitializer]`，
这样消费者无需调用任何方法：

```csharp
using System.Runtime.CompilerServices;

internal static class Jdk8Registration
{
#pragma warning disable CA2255 // 类库中使用 ModuleInitializer 是有意的。
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register()
        => JdkImplementationRegistry.Register(new Jdk8Implementation());
}
```

### 第 3 步 —— 运行

```csharp
var runtime = JvmInitializer.Initialize(new JvmInitializationOptions
{
    JdkBinPath = @"C:\Program Files\Java\jdk-8\bin",
    Version    = 8,
});

runtime.Invoker.RunMain("myapp.jar", "com.example.Main", "arg1", "arg2");
```

---

## 版本号参考表

| JDK | `Version` | `JniVersion`  | `JvmtiVersion` | 说明 |
| --- | --- | --- | --- | --- |
| 8   | `8`  | `0x00010008` | `0x80010008` | 旧布局。请对照 OpenJDK 8 的 `jvmti.h` 核对。 |
| 9   | `9`  | `0x00090000` | — | 现代布局开始。 |
| 11  | `11` | `0x000B0000` | `0x300B0000` | LTS。 |
| 17  | `17` | `0x00110000` | `0x30110000` | LTS。 |
| 21  | `21` | `0x00150000` | `0x30150000` | LTS。内置。 |
| 22  | `22` | `0x00160000` | `0x30160000` | 内置。 |
| 23  | `23` | `0x00170000` | `0x30170000` | 内置。 |
| 24  | `24` | `0x00180000` | `0x30180000` | 内置。 |
| 25  | `25` | `0x00190000` | `0x30190000` | LTS。内置。 |

> **现代布局（JDK 9+）：** `JniVersion = major << 16`，
> `JvmtiVersion = 0x30000000 | (major << 16)`。
>
> **旧布局（JDK ≤ 8）：** `JniVersion = 0x0001000X`，其中 `X` 是次版本号
> （例如 8 → `0x00010008`）。JVMTI 版本常量在每个版本的 `jvmti.h` 中单独发布，
> **不**遵循统一公式——务必对照目标 JDK 的头文件核对。

---

## 何时直接实现 `IJvmRuntime`

`JdkRuntimeBase` 假设目标 JDK 暴露与 JDK 21 相同的 JNI/JVMTI 函数表。从 JDK 8 起
的每个 OpenJDK 都满足这一假设——函数表只增不减——所以 `JdkRuntimeBase` 对 JDK 8
也适用。

只有以下情况才需要直接实现 `IJvmRuntime`：

- 使用非 OpenJDK ABI（例如裁剪过的嵌入式 JVM）。
- 需要不同的生命周期（例如通过 `JNI_GetCreatedJavaVMs` 附加到已运行的 JVM，
  而不是用 `JNI_CreateJavaVM` 新建）。
- 想用自己的实现替换 invoker / 字节码修改器 / 事件监听器，而不是用内置的
  `Jdk21Invoker` / `Jdk21BytecodeModifier` / `Jdk21EventListener`。

---

## 注意事项

- **`onload-only` capability**（如 `can_generate_method_entry_events`）仍需要
  `-agentpath:` native agent。无论 JDK 版本如何，`Jvm.NET` 的字节码插桩兜底方案
  （方案 B）都会被使用。
- **class 文件版本**：内置的 ASM 移植版以现代 class 文件版本为目标。JDK 8 的
  class 文件（版本 52）可以正确读取，但如果通过 `Jvm.NET.Asm` 生成 class 文件，
  请确保把 `ClassWriter` 设置为正确的版本。
- **注册顺序**：`Register` 会替换同一 `Version` 的已有实现。如果多个第三方包都
  注册了 JDK 8，最后注册的胜出。加载顺序由 .NET 程序集加载顺序决定——如果需要
  确定性，请在应用启动时显式调用 `Register`。
