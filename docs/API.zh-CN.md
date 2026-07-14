# Jvm.NET API 中文参考

本文档列出 `Jvm.NET` 的公开 API。除特别说明外，类型都位于 `Jvm.NET.Abstractions` 命名空间。

## 目录

- [初始化](#初始化)
- [JDK 版本抽象](#jdk-版本抽象)
- [运行时门面](#运行时门面)
- [方法调用](#方法调用)
- [字节码修改](#字节码修改)
- [事件监听](#事件监听)
- [值类型与句柄类型](#值类型与句柄类型)

## 初始化

### `Jvm.NET.JvmInitializer`（静态）

| 成员 | 说明 |
| --- | --- |
| `Initialize(JvmInitializationOptions options) : IJvmRuntime` | 在当前进程内启动 JVM，返回运行时门面。同一 JDK 版本下幂等。 |
| `Current : IJvmRuntime?` | 返回正在运行的运行时；若未启动则返回 `null`。 |

### `JvmInitializationOptions`

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `JdkBinPath` | `string` | **必填**。JDK `bin` 目录的绝对路径。 |
| `Version` | `int` | **必填**。JDK 主版本号（如 `21`、`22`、`8`）。核心包内置 21-25 的实现；其它版本可通过 `JdkImplementationRegistry` 注册——详见 [EXTENDING.zh-CN.md](EXTENDING.zh-CN.md)。 |
| `VmArguments` | `IReadOnlyList<string>` | 透传给 `JNI_CreateJavaVM` 的额外 `-X` / `-D` 参数。 |
| `Classpath` | `IReadOnlyList<string>` | 启动时追加到 `java.class.path` 的条目。 |
| `EnableBytecodeModification` | `bool` | 是否加载 JVMTI 代理以支持 `ClassFileLoadHook`。默认 `true`。 |
| `EnableEventListening` | `bool` | 是否启用 JVMTI 事件回调。默认 `true`。 |
| `RequireJvmti` | `bool` | 获取不到 JVMTI 时是否抛出异常。 |

## JDK 版本抽象

`Jvm.NET` 内置 JDK 21-25 的默认实现，并提供可插拔的注册表，允许第三方包提供自己的实现
（例如在 `Jvm.NET` 发布新版本前支持新 JDK 发行版，或支持厂商定制 JVM）。

### `Version`（int）

JDK 主版本号，普通 `int` 类型。核心包内置支持 JDK 21、22、23、24、25。任意正整数
都可作为版本号使用，只要在 `Initialize` 之前通过 `JdkImplementationRegistry` 注册
了匹配的 `IJdkImplementation`——教程见 [EXTENDING.zh-CN.md](EXTENDING.zh-CN.md)
（含 JDK 8 完整示例）。

### `IJdkImplementation`

每个 JDK 版本适配器都需实现此契约。核心包内部实现在 `Jvm.NET.Abstractions.Jdk21`..`Jdk25`
命名空间下；第三方包应暴露自己的 `IJdkImplementation`。

| 成员 | 说明 |
| --- | --- |
| `Version : int` | 该实现所针对的 JDK 主版本号（如 21、8）。 |
| `JniVersion : int` | 传给 `JNI_CreateJavaVM` / `GetEnv` 的 JNI 版本常量（如 JDK 21 为 `0x00150000`）。 |
| `JvmtiVersion : int` | 传给 `JavaVM->GetEnv` 的 JVMTI 版本常量（如 JDK 21 为 `0x30150000`）。 |
| `CreateRuntime(JvmInitializationOptions) : IJvmRuntime` | 构造该版本的运行时。 |

### `JdkImplementationRegistry`（静态）

`JvmInitializer.Initialize` 会查询的全局注册表。核心包在程序集加载时通过
`ModuleInitializer` 自动注册 JDK 21-25；第三方包可以在 `Initialize` 之前调用
`Register` 来覆盖任意版本。

| 成员 | 说明 |
| --- | --- |
| `Register(IJdkImplementation) : void` | 注册（或替换）`implementation.Version` 对应的实现。 |
| `Resolve(int version) : IJdkImplementation?` | 返回已注册的实现，未注册则返回 `null`。 |

### `JdkRuntimeBase`

内置 JDK 21-25 实现所使用的公共基类。JDK 21-25 的 JNI/JVMTI ABI 完全一致，唯一的
版本差异是传给 `JNI_CreateJavaVM` 和 `GetEnv` 的版本常量，因此一个参数化的
`JdkRuntimeBase` 就够用。第三方 `IJdkImplementation` 作者可继承它，也可直接实现
`IJvmRuntime`。

| 构造函数 | 说明 |
| --- | --- |
| `JdkRuntimeBase(JvmInitializationOptions, int version, int jniVersion, int jvmtiVersion)` | 通过显式版本常量创建运行时。 |
| `JdkRuntimeBase(JvmInitializationOptions, IJdkImplementation)` | 从已注册的实现创建运行时。 |

> 构造后实例即表现为 `IJvmRuntime`（见下文）。

## 运行时门面

### `IJvmRuntime`

| 成员 | 说明 |
| --- | --- |
| `State : JvmRuntimeState` | `NotStarted` / `Starting` / `Running` / `ShuttingDown` / `Stopped` / `Faulted`。 |
| `Version : int` | 该运行时对应的 JDK 主版本号（如 21、8）。 |
| `Invoker : IJvmInvoker` | 方法调用入口；未运行时抛出异常。 |
| `BytecodeModifier : IBytecodeModifier` | 字节码修改入口；启动时未开启则抛出异常。 |
| `EventListener : IJvmEventListener` | 事件监听入口；启动时未开启则抛出异常。 |
| `Start() : void` | 启动 JVM，幂等。 |
| `Shutdown() : void` | 销毁 JVM，销毁后无法再启动。 |
| `Dispose() : void` | 必要时调用 `Shutdown`。 |

## 方法调用

### `IJvmInvoker`

| 成员 | 说明 |
| --- | --- |
| `LoadJar(string jarPath) : void` | 将 jar 或目录追加到运行时 classpath。 |
| `FindClass(string fqn) : JvmClass?` | 返回已加载的类；找不到返回 `null`。 |
| `LoadClass(string fqn) : JvmClass` | 强制 JVM 加载类；找不到抛出异常。 |
| `NewObject(JvmClass, string ctorSig, params JvmValue[] args) : JvmObject` | 分配 + 调用构造函数。 |
| `InvokeStatic(JvmClass, string name, string sig, params JvmValue[] args) : JvmValue` | 调用静态方法。 |
| `InvokeVirtual(JvmObject, string name, string sig, params JvmValue[] args) : JvmValue` | 调用实例方法。 |
| `RunMain(string jarPath, string mainClass, params string[] args) : void` | 加载 jar 并调用 `public static void main(String[])`。 |
| `GetString(IntPtr javaStringHandle) : string` | 读取 Java `String`（modified UTF-8）为 .NET `string`。 |
| `NewStringArray(string[] args) : JvmValue` | 创建 Java `String[]`，用于传递给 `main` 等方法。 |

> 方法 / 构造函数签名使用 JNI 形式，例如 `(Ljava/lang/String;I)V`。

## 字节码修改

### `IBytecodeModifier`

| 成员 | 说明 |
| --- | --- |
| `RegisterTransformer(IBytecodeTransformer) : IDisposable` | 注册转换器；返回值 `Dispose` 即移除。 |
| `RetransformClasses(IEnumerable<JvmClass>) : void` | 触发 JVMTI `RetransformClasses`。 |
| `RedefineClasses(IEnumerable<KeyValuePair<JvmClass, byte[]>>) : void` | 通过 `RedefineClasses` 直接替换字节码。 |

### `IBytecodeTransformer`

| 成员 | 说明 |
| --- | --- |
| `Name : string` | 用于诊断的名称。 |
| `Transform(string className, byte[] original) : byte[]?` | 返回改写后的字节码，或返回 `null` 表示保留原字节码。 |

## 事件监听

### `IJvmEventListener`

每个方法返回 `IDisposable`，`Dispose` 后取消订阅（当订阅者数量降为 0 时会自动关闭对应的 JVMTI 事件位）：

- `SubscribeMethodEntry(Action<MethodEntryEventData>)`
- `SubscribeMethodExit(Action<MethodExitEventData>)`
- `SubscribeClassLoad(Action<ClassLoadEventData>)`
- `SubscribeClassPrepare(Action<ClassPrepareEventData>)`
- `SubscribeThreadStart(Action<ThreadStartEventData>)`
- `SubscribeThreadEnd(Action<ThreadEndEventData>)`
- `SubscribeVmInit(Action<VmInitEventData>)`
- `SubscribeVmDeath(Action<VmDeathEventData>)`
- `SubscribeException(Action<ExceptionEventData>)`

> **关于 MethodEntry / MethodExit / Exception：**
> 这三个事件通过字节码插桩模拟（方案 B）。通过 `JNI_CreateJavaVM` 启动的嵌入式 JVM 立即进入 `live` 阶段，所需的 JVMTI capability（`can_generate_method_entry_events`、`can_generate_method_exit_events`、`can_generate_exception_events`）属于 `onload-only`，无法在 live 阶段添加。因此 `Jvm.NET` 在 JVM 中注入桥接类 `com.xsy.jn.JnBridge`，并在类加载时通过 `ClassFileLoadHook` 改写目标方法字节码：
> - **MethodEntry**：在方法入口插入 `invokestatic JnBridge.onMethodEntry(className, methodName)`。
> - **MethodExit**：在每个 return 指令前插入 `invokestatic JnBridge.onMethodExit(className, methodName)`。
> - **Exception**：在方法体外包裹 `try-catch(all)` 块；catch handler 调用 `JnBridge.onException(className, methodName, exception)` 后重新抛出异常。
>
> 需要同时开启 `EnableBytecodeModification` 和 `EnableEventListening`。构造函数 `<init>` 会插桩 MethodEntry/MethodExit，但**不会**插桩 Exception（try-catch 跨越 `super.<init>()` 会导致 stackmap frame 类型不匹配）。

### 事件数据类型

| 类型 | 关键字段 |
| --- | --- |
| `MethodEntryEventData` | `Class : JvmClass`、`MethodName : string`、`MethodSignature : string` |
| `MethodExitEventData` | `Class : JvmClass`、`MethodName`、`MethodSignature`、`ReturnValue : JvmValue`、`WasException : bool` |
| `ExceptionEventData` | `Exception : JvmObject`、`ThrowingClass : JvmClass?`、`MethodName`、`MethodSignature` |
| `ClassLoadEventData` | `Class : JvmClass` |
| `ClassPrepareEventData` | `Class : JvmClass` |
| `ThreadStartEventData` | `Thread : JvmObject` |
| `ThreadEndEventData` | `Thread : JvmObject` |
| `VmInitEventData` | `Thread : JvmObject` |
| `VmDeathEventData` | _（无）_ |

## 值类型与句柄类型

### `JvmValue`（只读 struct）

涵盖所有 JVM 基础类型与对象引用的可辨识联合。工厂方法：`FromBoolean`、`FromByte`、`FromChar`、`FromShort`、`FromInt`、`FromLong`、`FromFloat`、`FromDouble`、`FromObject(IntPtr)`、`Null`。

### `JvmClass` / `JvmObject`

对原始 `jclass` / `jobject` 句柄的轻量封装。生命周期由 JVM 管理 —— 封装类**不会**调用 `DeleteLocalRef`。

- `JvmClass` —— `Handle : IntPtr`、`Name : string`（内部名，斜杠分隔）。
- `JvmObject` —— `Handle : IntPtr`、`Class : JvmClass`。
