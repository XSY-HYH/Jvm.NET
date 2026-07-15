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
- [Java/.NET 互操作](#javanet-互操作)

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
| `Interop` | `JvmInteropOptions` | Java/.NET 互操作选项。默认 `NativeOnly` 模式（纯 JNI+ASM，无需 jar）。详见 [Java/.NET 互操作](#javanet-互操作)。 |

### `JvmInteropOptions`

| 属性 | 类型 | 说明 |
| --- | --- | --- |
| `Mode` | `InteropMode` | 互操作模式。默认 `NativeOnly`（纯 JNI+ASM）。选 `WithJar` 需提供 `BridgeJarPath`。 |
| `BridgeJarPath` | `string?` | Java 桥接 jar 的绝对路径。`Mode` 为 `WithJar` 时必需，会自动追加到 `java.class.path`。 |
| `AutoInitializeBridge` | `bool` | 是否在启动时自动初始化 Java 桥接类 `com.xsy.jn.Bridge`。仅 `WithJar` 模式生效。默认 `true`。 |

### `InteropMode`（枚举）

| 值 | 说明 |
| --- | --- |
| `NativeOnly` | 纯 JNI+ASM 模式，无需 Java 桥接 jar。Java→C# 回调通过 `RegisterNatives` 实现。 |
| `WithJar` | 加载 Java 桥接 jar，提供 Java 侧的 .NET 对象代理与高级回调路由。需提供 `BridgeJarPath`。 |

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
| `NewString(string str) : JvmValue` | 从 .NET `string` 创建 Java `String`（modified UTF-8）。返回的 `JvmValue` 持有 JNI 局部引用。 |
| `GetString(IntPtr javaStringHandle) : string` | 读取 Java `String`（modified UTF-8）为 .NET `string`。 |
| `NewStringArray(string[] args) : JvmValue` | 创建 Java `String[]`，用于传递给 `main` 等方法。 |
| `GetField(JvmObject, string name, string sig) : JvmValue` | 读取实例字段。`sig` 为字段类型签名（如 `I`、`Ljava/lang/String;`）。 |
| `SetField(JvmObject, string name, string sig, JvmValue value) : void` | 写入实例字段。 |
| `GetStaticField(JvmClass, string name, string sig) : JvmValue` | 读取静态字段。 |
| `SetStaticField(JvmClass, string name, string sig, JvmValue value) : void` | 写入静态字段。 |
| `IsInstanceOf(JvmObject instance, JvmClass clazz) : bool` | 检查对象是否是指定类的实例。 |
| `IsAssignableFrom(JvmClass from, JvmClass to) : bool` | 检查 `from` 是否可赋值给 `to`。 |
| `GetSuperclass(JvmClass clazz) : JvmClass?` | 获取父类。`clazz` 为 `Object` 或接口时返回 `null`。 |
| `GetObjectClass(JvmObject instance) : JvmClass` | 获取对象的运行时类。 |
| `GetArrayLength(JvmValue array) : int` | 获取数组长度。 |
| `GetObjectArrayElement(JvmValue array, int index) : JvmValue` | 读取对象数组的元素。 |
| `SetObjectArrayElement(JvmValue array, int index, JvmValue value) : void` | 设置对象数组的元素。 |
| `NewArray<T>(T[] values) : JvmValue` | 创建 Java 基本类型数组并用 `values` 填充。`T` 必须是 `bool`/`byte`/`char`/`short`/`int`/`long`/`float`/`double`。 |
| `GetArrayValues<T>(JvmValue array) : T[]` | 读取 Java 基本类型数组的所有元素。 |
| `NewObjectArray(JvmClass elementClass, JvmValue[] elements) : JvmValue` | 创建 Java 对象数组。 |
| `GetPendingException() : JvmObject?` | 获取当前 pending 异常（不清除）。无 pending 异常时返回 `null`。 |
| `GetExceptionMessage(JvmObject exception) : string` | 调用 `Throwable.getMessage()` 获取异常消息。 |
| `GetExceptionStackTrace(JvmObject exception) : string` | 调用 `Throwable.getStackTrace()` 获取堆栈跟踪字符串。 |
| `RegisterCallback(JvmClass clazz, string methodName, string sig, Delegate callback) : void` | 在 `clazz` 上注册 native 方法，使 Java 代码可以回调到 .NET 委托。委托被强引用保持，直到 `UnregisterCallbacks` 被调用或 Invoker 释放。 |
| `UnregisterCallbacks(JvmClass clazz) : void` | 注销 `clazz` 上所有通过 `RegisterCallback` 注册的 native 方法。 |

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

读取方法：`AsBoolean`、`AsByte`、`AsChar`、`AsShort`、`AsInt`、`AsLong`、`AsFloat`、`AsDouble`。`ObjectHandle : IntPtr` 属性返回对象引用句柄。`Type : JvmValueType` 属性返回当前持有的值类型。

### `JvmClass` / `JvmObject`（实现 `IDisposable`）

对原始 `jclass` / `jobject` 句柄的封装。两者都实现 `IDisposable`。

**句柄所有权**：
- 通过 `IJvmInvoker.FindClass` / `LoadClass` / `NewObject` 创建的实例**拥有** JNI 全局引用，`Dispose` 时自动调用 `DeleteGlobalRef`。
- 通过公共构造函数 `new JvmClass(handle, name)` / `new JvmObject(handle, clazz)` 创建的实例**不拥有**句柄，`Dispose` 是空操作。适用于包装 `InvokeVirtual` / `InvokeStatic` 返回的局部引用（局部引用的生命周期由调用方管理）。
- `OwnsHandle : bool` 属性指示是否拥有句柄。

**字段**：
- `JvmClass` —— `Handle : IntPtr`、`Name : string`（内部名，斜杠分隔）、`OwnsHandle : bool`。
- `JvmObject` —— `Handle : IntPtr`、`Class : JvmClass`、`OwnsHandle : bool`。

> 当 `JvmObject.Class.Handle` 为 `IntPtr.Zero`（占位类）时，`IJvmInvoker.InvokeVirtual` 会通过 `GetObjectClass` 获取对象的实际运行时类来查找方法 ID。这允许用 `new JvmObject(handle, new JvmClass(IntPtr.Zero, "java/util/Set"))` 包装返回值后调用其方法。

## Java/.NET 互操作

`Jvm.NET` 提供 Java 与 .NET 之间的双向互操作。默认的 `NativeOnly` 模式纯基于 JNI+ASM，无需额外 Java jar。

### `TypeMapper`（静态）

CLR 类型与 Java 类型之间的自动转换。

| 方法 | 说明 |
| --- | --- |
| `FromClr(object? value, IJvmInvoker) : JvmValue` | 将 CLR 值转换为 `JvmValue`。支持 `string`、所有基本类型、`JvmObject`、`JavaObject` 子类。`null` 转为 `JvmValue.Null`。 |
| `ToClr<T>(JvmValue value, IJvmInvoker) : T?` | 将 `JvmValue` 转换为 CLR 类型。`T` 为 `string` 时调用 `GetString`；为值类型时调用对应的 `As*` 方法；为 `JavaObject` 子类时调用 `Wrap`。 |
| `ToString(JvmValue value, IJvmInvoker) : string` | 调用 Java 对象的 `toString()` 方法。 |
| `ToStringArray(JvmValue arrayValue, IJvmInvoker) : string[]` | 读取 Java `String[]` 为 .NET `string[]`。 |
| `Box(JvmValue value, IJvmInvoker) : JvmValue` | 将基本类型装箱为 Java 包装类对象（`int` → `Integer.valueOf(int)` 等）。用于把 CLR 值类型传给 Java `Object` 参数。 |
| `Unbox(JvmValue value, IJvmInvoker) : JvmValue` | 将 Java 包装类对象拆箱为基本类型（`Integer` → `intValue()` 等）。通过 `getClass().getName()` 识别包装类。 |

### `JavaList<T>`（实现 `IList<T>`, `IDisposable`）

Java `java.util.List` 的 .NET 包装，实现 `IList<T>` 语义。

| 成员 | 说明 |
| --- | --- |
| `JavaList(IJvmInvoker invoker, JvmObject list)` | 包装已有的 Java List 对象。 |
| `NewArrayList(IJvmInvoker invoker) : JavaList<T>` | 创建新的 `java.util.ArrayList`。 |
| `UnderlyingObject : JvmObject` | 被包装的 Java 对象。 |
| `Count`, `IsReadOnly`, `this[int]`, `Add`, `Clear`, `Contains`, `CopyTo`, `IndexOf`, `Insert`, `Remove`, `RemoveAt` | `IList<T>` 标准成员。 |

> 当 `T` 是 CLR 值类型时，`Add` / 索引器会自动调用 `TypeMapper.Box` 装箱，`ConvertFromJava` 会调用 `TypeMapper.Unbox` 拆箱。

### `JavaMap<TKey, TValue>`（实现 `IDictionary<TKey, TValue>`, `IDisposable`）

Java `java.util.Map` 的 .NET 包装，实现 `IDictionary<TKey, TValue>` 语义。

| 成员 | 说明 |
| --- | --- |
| `JavaMap(IJvmInvoker invoker, JvmObject map)` | 包装已有的 Java Map 对象。 |
| `NewHashMap(IJvmInvoker invoker) : JavaMap<TKey, TValue>` | 创建新的 `java.util.HashMap`。 |
| `UnderlyingObject : JvmObject` | 被包装的 Java 对象。 |
| `Count`, `IsReadOnly`, `this[TKey]`, `Keys`, `Values`, `Add`, `Clear`, `Contains`, `ContainsKey`, `CopyTo`, `GetEnumerator`, `Remove`, `TryGetValue` | `IDictionary<TKey, TValue>` 标准成员。 |

> 与 `JavaList<T>` 相同，当 `TKey` / `TValue` 是 CLR 值类型时会自动装箱/拆箱。`GetEnumerator` 通过 `entrySet().iterator()` 遍历，每个 `Map.Entry` 调用 `getKey()` / `getValue()`。

### `JavaObject`（抽象基类，实现 `IDisposable`）

Java 对象的 .NET 包装基类。用户继承此类手写 Java 类的 C# 封装，或通过 Source Generator 自动生成。

**使用方式**：
1. 继承 `JavaObject`，用 `[JavaClass]` 标记 Java 类名。
2. 通过 `Create<T>` 工厂方法创建新实例，或 `Wrap<T>` 包装已有对象。
3. 通过 `Invoke` / `GetField` / `SetField` 等 protected 方法操作对象。

| 成员 | 说明 |
| --- | --- |
| `Handle : JvmObject` | 包装的 Java 对象句柄。未初始化时抛出异常。 |
| `Invoker : IJvmInvoker` | 关联的 Invoker。未初始化时抛出异常。 |
| `JavaClassName : string`（protected virtual） | Java 类全名（点分隔）。默认从 `[JavaClass]` 特性读取，可重写。 |
| `Create<T>(IJvmInvoker, string ctorSig, params JvmValue[] args) : T`（static） | 创建 `T` 的新实例：加载 Java 类、调用构造函数、返回包装对象。 |
| `Wrap<T>(IJvmInvoker, JvmObject obj) : T`（static） | 包装一个已有的 Java 对象。 |
| `Invoke(string name, string sig, params JvmValue[] args) : JvmValue`（protected） | 调用实例方法。 |
| `Invoke<T>(string name, string sig, params JvmValue[] args) : T?`（protected） | 调用实例方法并通过 `TypeMapper.ToClr<T>` 转换返回值。 |
| `InvokeStatic(string name, string sig, params JvmValue[] args) : JvmValue`（protected） | 调用静态方法。 |
| `InvokeStatic<T>(...) : T?`（protected） | 调用静态方法并转换返回值。 |
| `GetField(string name, string sig) : JvmValue`（protected） | 读取实例字段。 |
| `GetField<T>(string name, string sig) : T?`（protected） | 读取实例字段并转换。 |
| `SetField(string name, string sig, JvmValue value) : void`（protected） | 写入实例字段。 |
| `GetStaticField(string name, string sig) : JvmValue`（protected） | 读取静态字段。 |
| `SetStaticField(string name, string sig, JvmValue value) : void`（protected） | 写入静态字段。 |
| `IsInstanceOf(string javaClassName) : bool` | 检查此对象是否是指定 Java 类的实例。 |
| `Dispose() : void` | 释放包装的 `JvmObject`。 |

### 特性类

| 特性 | 目标 | 说明 |
| --- | --- | --- |
| `[JavaClass(string name)]` | Class | 标记 C# 类对应的 Java 类名（如 `java.util.ArrayList`）。`JavaObject` 会自动读取。 |
| `[JavaMethod(string name, string signature)]` | Method | 标记 C# 方法对应的 Java 方法。`IsStatic` 属性指示是否为静态方法。Source Generator 会读取此特性生成调用代码。 |
| `[JavaField(string name, string signature)]` | Property | 标记 C# 属性对应的 Java 字段。`IsStatic` 属性指示是否为静态字段。Source Generator 会读取此特性生成访问代码。 |

### Source Generator（`Jvm.NET.SourceGenerator` 包）

引用 `Jvm.NET.SourceGenerator` 后，用 `[JavaClass]` / `[JavaMethod]` / `[JavaField]` 标记的 `partial` 类会自动生成强类型封装代码。用户只需声明 partial 方法签名，SG 会生成调用 JNI 的实现。

```csharp
using Jvm.NET.Abstractions;

[JavaClass("java.util.ArrayList")]
public sealed partial class JavaArrayList : JavaObject
{
    [JavaMethod("add", "(Ljava/lang/Object;)Z")]
    public partial bool Add(object? item);

    [JavaMethod("size", "()I")]
    public partial int Count();
}
```

### Java 桥接 jar（`WithJar` 模式）

可选的 Java 桥接 jar（源码位于 `src/Jvm.NET.JavaBridge/`）提供 Java 侧的 .NET 对象代理与高级回调路由。

**核心类**：
- `com.xsy.jn.Bridge`：Java 桥接入口，含 native 方法（`onBridgeInitialized`、`registerDotNetObject`、`invokeDotNetMethod`、`getDotNetField`、`setDotNetField`、`isInstanceOf`）。静态初始化器调用 `onBridgeInitialized()` 通知 .NET 侧。
- `com.xsy.jn.DotNetObject`：.NET 对象的 Java 代理，通过 Bridge native 方法路由到 .NET。`finalize()` 时自动调用 `unregisterDotNetObject`。

**构建**：运行 `src/Jvm.NET.JavaBridge/build.ps1`（Windows）或 `build.sh`（Linux/macOS）生成 `dist/jn-bridge.jar`。需要 `javac` 和 `jar`（在 `JAVA_HOME` 或 `PATH` 中）。
