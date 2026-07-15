# Jvm.NET 实现原理 / Implementation Notes

> 本文档解释 Jvm.NET 各核心功能的实现原理，包括 JVM 嵌入、JNI 调用、字节码修改和事件监听。
>
> This document explains how each core feature of Jvm.NET is implemented: JVM embedding, JNI invocation, bytecode modification, and event listening.

---

## 目录 / Table of Contents

1. [JVM 嵌入 / JVM Embedding](#1-jvm-嵌入--jvm-embedding)
2. [JNI 调用 / JNI Invocation](#2-jni-调用--jni-invocation)
3. [字节码修改 / Bytecode Modification](#3-字节码修改--bytecode-modification)
4. [事件监听 / Event Listening](#4-事件监听--event-listening)
5. [Java/.NET 互操作 / Java/.NET Interop](#5-javanet-互操作--javanet-interop)

---

## 1. JVM 嵌入 / JVM Embedding

### 中文

Jvm.NET 通过 JNI（Java Native Interface）在当前进程内嵌入一个完整的 JVM。核心流程如下：

1. **加载 native 库**：通过 `NativeLibrary.TryLoad` 在运行时加载 `jvm.dll`（Windows）/ `libjvm.so`（Linux）/ `libjvm.dylib`（macOS）。不使用 `DllImport`，因为库路径要到运行时才知道（来自用户提供的 `JdkBinPath`）。加载时还会把 JDK 的 `bin` 目录加入 `PATH`，因为 `jvm.dll` 依赖同目录的 `java.dll`、`jimage.dll` 等伴生库。
   - **fallback 路径**：JDK 标准布局中 `jvm.dll` 在 `bin\server\` 子目录（不在 `bin\` 根目录），Linux/macOS 的 `libjvm.so` 在 `lib/server/`。加载器会依次尝试 `bin\`、`bin\server\`、`lib\server\`。

2. **解析入口函数**：从加载的库中通过 `GetExport` 解析 `JNI_CreateJavaVM` 函数指针，转为 C# 委托。

3. **构造启动参数**：把用户提供的 classpath、VM 参数打包成 `JavaVMInitArgs` 结构（内部是 `JavaVMOption[]` 数组）。所有字符串通过 `Marshal.StringToHGlobalAnsi` 分配到非托管内存，因为 JNI 需要稳定的 native 指针。

4. **创建 JVM**：调用 `JNI_CreateJavaVM(out JavaVM*, out JNIEnv*, &args)`。返回 `JavaVM*`（JVM 实例句柄）和 `JNIEnv*`（当前线程的 JNI 环境）。

5. **获取 JVMTI 环境**：通过 `JavaVM->GetEnv(JVMTI_VERSION_21)` 获取 `jvmtiEnv*`。JVMTI 版本号 `0x30150000` 包含 `0x30000000` 接口类型位，用于区分 JNI 和 JVMTI 请求。

### English

Jvm.NET embeds a full JVM in the current process via JNI (Java Native Interface). The core flow is:

1. **Load the native library**: `NativeLibrary.TryLoad` loads `jvm.dll` (Windows) / `libjvm.so` (Linux) / `libjvm.dylib` (macOS) at runtime. `DllImport` is not used because the library path is only known at runtime (from the user-supplied `JdkBinPath`). The JDK `bin` directory is also prepended to `PATH` because `jvm.dll` depends on sibling libraries like `java.dll`, `jimage.dll`.
   - **Fallback paths**: In the standard JDK layout `jvm.dll` lives in `bin\server\` (not `bin\` root); on Linux/macOS `libjvm.so` lives in `lib/server/`. The loader tries `bin\`, `bin\server\`, `lib\server\` in order.

2. **Resolve the entry point**: `GetExport` resolves the `JNI_CreateJavaVM` function pointer from the loaded library and converts it to a C# delegate.

3. **Build startup arguments**: The user-supplied classpath and VM arguments are packed into a `JavaVMInitArgs` struct (containing a `JavaVMOption[]` array). All strings are allocated in unmanaged memory via `Marshal.StringToHGlobalAnsi` because JNI requires stable native pointers.

4. **Create the JVM**: `JNI_CreateJavaVM(out JavaVM*, out JNIEnv*, &args)` is called, returning a `JavaVM*` (JVM instance handle) and `JNIEnv*` (current thread's JNI environment).

5. **Acquire the JVMTI environment**: `JavaVM->GetEnv(JVMTI_VERSION_21)` returns a `jvmtiEnv*`. The JVMTI version `0x30150000` includes the `0x30000000` interface-type bit that distinguishes a JVMTI request from a JNI request.

---

## 2. JNI 调用 / JNI Invocation

### 中文

JNI 调用层由 `Jdk21Invoker` 实现，封装了类加载、对象创建、方法调用等操作。

**函数表布局**：`JNIEnv*` 指向 `JNINativeInterface_` 结构体，这是一个函数指针表。Jvm.NET 用 `LayoutKind.Explicit` + `FieldOffset` 精确映射每个槽位，然后通过 `Marshal.GetDelegateForFunctionPointer` 把函数指针转为 C# 委托。

**关键操作**：
- `FindClass(name)`：通过 JVM 启动类加载器查找已加载的类，返回 `jclass`。类名用 `/` 分隔（如 `java/lang/String`）。
- `LoadClass(name)`：通过应用类加载器（`ClassLoader.loadClass`）强制加载类。类名用 `.` 分隔。
- `GetStaticMethodID` / `GetMethodID`：查找方法 ID，需要 JNI 签名（如 `(II)I` 表示两个 int 参数返回 int）。
- `CallStaticIntMethodA` / `CallStaticObjectMethodA`：调用静态方法，参数通过 `jvalue` 联合体传递。
- `NewStringUTF` / `GetStringUTFChars`：在 .NET string 和 Java String 之间转换（modified UTF-8 编码）。

**引用管理**：JNI 区分 local ref（当前 JNI 栈帧有效）和 global ref（跨栈帧有效）。`FindClass` 返回的 `jclass` 会被提升为 global ref，避免在后续调用中失效。

### English

The JNI invocation layer is implemented by `Jdk21Invoker`, wrapping class loading, object creation, and method invocation.

**Function table layout**: `JNIEnv*` points to a `JNINativeInterface_` struct, which is a table of function pointers. Jvm.NET maps each slot precisely with `LayoutKind.Explicit` + `FieldOffset`, then converts function pointers to C# delegates via `Marshal.GetDelegateForFunctionPointer`.

**Key operations**:
- `FindClass(name)`: Looks up an already-loaded class through the JVM's startup class loader, returns a `jclass`. The name uses `/` separators (e.g. `java/lang/String`).
- `LoadClass(name)`: Forces class loading through the application class loader (`ClassLoader.loadClass`). The name uses `.` separators.
- `GetStaticMethodID` / `GetMethodID`: Looks up a method ID, requiring a JNI signature (e.g. `(II)I` means two int arguments returning int).
- `CallStaticIntMethodA` / `CallStaticObjectMethodA`: Invokes a static method; arguments are passed via `jvalue` unions.
- `NewStringUTF` / `GetStringUTFChars`: Converts between .NET strings and Java Strings (modified UTF-8 encoding).

**Reference management**: JNI distinguishes local refs (valid for the current JNI stack frame) and global refs (valid across frames). The `jclass` returned by `FindClass` is promoted to a global ref so it stays valid in subsequent calls.

---

## 3. 字节码修改 / Bytecode Modification

### 中文

字节码修改通过 JVMTI 的 `ClassFileLoadHook` 事件实现。当 JVM 加载 class 文件时，会在解析之前触发 `ClassFileLoadHook`，允许 agent 修改字节码。

**工作流程**：
1. **注册 capability**：启动时添加 `can_generate_all_class_hook_events`、`can_redefine_classes`、`can_retransform_classes` 等 capability。
2. **注册回调**：通过 `SetEventCallbacks` 注册 `ClassFileLoadHook` 回调函数。回调签名为 `(jvmtiEnv*, JNIEnv*, class, loader, name, protection_domain, class_data_len, class_data, new_class_data_len*, new_class_data**)`。
3. **启用事件**：通过 `SetEventNotificationMode(JVMTI_ENABLE, JVMTI_EVENT_CLASS_FILE_LOAD_HOOK, NULL)` 启用事件通知。
4. **变换字节码**：当事件触发时，遍历已注册的 `IBytecodeTransformer` 列表。如果 transformer 返回新的字节数组，用 `jvmti->Allocate` 分配 native 内存，把新字节码复制进去，然后回写 `*new_class_data` 和 `*new_class_data_len`。JVM 会使用新的字节码来定义类。
5. **RedefineClasses / RetransformClasses**：对已加载的类，可以调用 `RedefineClasses` 重新定义字节码，或 `RetransformClasses` 重新触发 `ClassFileLoadHook`。`RedefineClasses` 时用 `GCHandle.Alloc(bytes, Pinned)` 钉住 byte[]，保持指针在调用期间有效。

### 为什么字节码替换需要等长？/ Why must bytecode replacement be equal-length?

**中文解释**：

class 文件的常量池中，字符串以 `CONSTANT_Utf8_info` 结构存储：
```
CONSTANT_Utf8_info {
    u1 tag;        // 值 = 1
    u2 length;     // 字节数（big-endian）
    u1 bytes[length]; // 实际 UTF-8 字节
}
```

`length` 字段记录了 `bytes` 的确切字节数。JVM 解析常量池时，会读取 `length` 字节作为字符串内容，然后从第 `length+1` 字节开始解析下一个常量池条目。

**等长替换**（推荐）：只替换 `bytes` 内容，不改 `length`。因为字节数没变，class 文件结构完整，JVM 能正常解析。这是 `StringReplaceTransformer` 的做法（如把 `"Hello from Java!"` 替换为 `"Pwned from Java!"`，两者都是 16 字节）。

**不等长替换**（会破坏 class 文件）：
- **只改内容不改 length**（replacement 更短）：剩余字节保留原样，字符串内容变成 `"HACKEDfrom Java!"`。class 文件结构完整，JVM 能正常解析，但字符串内容不是预期的 `"HACKED"`。
- **改 length 不改总长度**（replacement 更短，把 length 改成 replacement 长度）：JVM 读取 shorter 字符串后，从剩余字节开始解析下一个常量池条目。剩余字节（如 `'f'`，ASCII 102）不是有效的常量池 tag，导致 `ClassFormatError: Unknown constant tag 102`。这是 `UnequalReplaceTransformer` 的做法。
- **插入/删除字节**（改变总长度）：会破坏 class 文件后续所有结构的偏移，导致 `ClassFormatError`。

**结论**：如果不想解析 class 文件常量池结构、不想修改 `length` 字段，等长替换是最简单、最安全的方式。

### English

Bytecode modification is implemented via the JVMTI `ClassFileLoadHook` event. When the JVM loads a class file, it fires `ClassFileLoadHook` before parsing, allowing the agent to modify the bytes.

**Workflow**:
1. **Register capabilities**: At startup, add `can_generate_all_class_hook_events`, `can_redefine_classes`, `can_retransform_classes`, etc.
2. **Register callback**: `SetEventCallbacks` registers the `ClassFileLoadHook` callback. Its signature is `(jvmtiEnv*, JNIEnv*, class, loader, name, protection_domain, class_data_len, class_data, new_class_data_len*, new_class_data**)`.
3. **Enable event**: `SetEventNotificationMode(JVMTI_ENABLE, JVMTI_EVENT_CLASS_FILE_LOAD_HOOK, NULL)` turns on event notification.
4. **Transform bytes**: When the event fires, iterate the registered `IBytecodeTransformer` list. If a transformer returns a new byte array, allocate native memory via `jvmti->Allocate`, copy the new bytes in, and write back `*new_class_data` and `*new_class_data_len`. The JVM uses the new bytes to define the class.
5. **RedefineClasses / RetransformClasses**: For already-loaded classes, call `RedefineClasses` to redefine bytecode, or `RetransformClasses` to re-trigger `ClassFileLoadHook`. During `RedefineClasses`, `GCHandle.Alloc(bytes, Pinned)` pins the byte[] to keep the pointer valid for the call duration.

### Why must bytecode replacement be equal-length?

**English explanation**:

In the class file's constant pool, strings are stored as `CONSTANT_Utf8_info`:
```
CONSTANT_Utf8_info {
    u1 tag;        // value = 1
    u2 length;     // byte count (big-endian)
    u1 bytes[length]; // actual UTF-8 bytes
}
```

The `length` field records the exact byte count of `bytes`. When the JVM parses the constant pool, it reads `length` bytes as the string content, then starts parsing the next constant pool entry from byte `length+1`.

**Equal-length replacement** (recommended): Only replace `bytes` content, leave `length` unchanged. Since the byte count is unchanged, the class file structure stays intact and the JVM parses it normally. This is what `StringReplaceTransformer` does (e.g. replacing `"Hello from Java!"` with `"Pwned from Java!"`, both 16 bytes).

**Unequal-length replacement** (breaks the class file):
- **Change content only, not length** (replacement shorter): Residual bytes remain, so the string becomes `"HACKEDfrom Java!"`. The class file structure is intact and the JVM parses it, but the string content is not the intended `"HACKED"`.
- **Change length, not total size** (replacement shorter, set length to replacement length): The JVM reads the shorter string, then starts parsing the next constant pool entry from the residual bytes. The residual byte (e.g. `'f'`, ASCII 102) is not a valid constant pool tag, causing `ClassFormatError: Unknown constant tag 102`. This is what `UnequalReplaceTransformer` does.
- **Insert/delete bytes** (change total size): Breaks the offsets of all subsequent structures in the class file, causing `ClassFormatError`.

**Conclusion**: If you don't want to parse the class file constant pool structure or modify the `length` field, equal-length replacement is the simplest and safest approach.

---

## 4. 事件监听 / Event Listening

### 中文

事件监听通过 JVMTI 的事件回调机制实现。JvmtiEventHub 作为共享协调者，BytecodeModifier 和 EventListener 都通过它注册回调，最后由 Runtime 统一调用一次 `SetEventCallbacks`（因为 `SetEventCallbacks` 是全局的，会整体替换回调表）。

**事件回调表布局**：`jvmtiEventCallbacks` 结构体是一个函数指针数组。字段顺序由 OpenJDK 的 XSLT 生成器决定（`jvmtiLib.xsl` 的 `eventStruct` 模板）。第一个事件是 `VMInit`（num=50），所以用 `FieldOffset = (num-50)*8` 映射。

**订阅机制**：每个事件类型有独立的 `List<Action<T>>`。当列表从空变非空时，调用 `SetEventNotificationMode(JVMTI_ENABLE, eventKind, NULL)` 启用事件通知；从非空变空时禁用。返回的 `IDisposable` 用于取消订阅。

**回调中的元数据读取**：在 ClassLoad/ClassPrepare 回调中，通过 `GetClassSignature` 读取类签名（如 `Ljava/lang/String;`）。在 ThreadStart/ThreadEnd 回调中，通过 `GetThreadInfo` 读取线程名。这些 JVMTI 分配的内存需要通过 `Deallocate` 释放。

### Capability 限制 / Capability limitation

**中文**：

JVMTI 的 capability 分为两类：
- **always-on**：在 JVMTI 的任何 phase（OnLoad / Live）都可以添加。如 `can_redefine_classes`、`can_generate_all_class_hook_events`。
- **onload-only**：只能在 `JVMTI_PHASE_ONLOAD` 期添加。如 `can_generate_method_entry_events`、`can_generate_method_exit_events`、`can_generate_exception_events`。

**嵌入式 JVM 的限制**：`JNI_CreateJavaVM` 返回后，JVM 已处于 `JVMTI_PHASE_LIVE`，无法再添加 onload-only capability。如果在 live phase 调用 `AddCapabilities` 请求 onload-only capability，JVMTI 会返回 `JVMTI_ERROR_NOT_AVAILABLE`（98）。

**影响**：
| 事件 | 需要 capability | 是否可用 |
|------|-----------------|----------|
| VMInit / VMDeath | 无 | 可用 |
| ThreadStart / ThreadEnd | 无 | 可用 |
| ClassLoad / ClassPrepare | 无 | 可用 |
| ClassFileLoadHook | `can_generate_all_class_hook_events` (always-on) | 可用 |
| MethodEntry | `can_generate_method_entry_events` (onload-only) | **通过方案 B 模拟** |
| MethodExit | `can_generate_method_exit_events` (onload-only) | **通过方案 B 模拟** |
| Exception | `can_generate_exception_events` (onload-only) | **通过方案 B 模拟** |

**已实现方案（方案 B — 字节码插桩模拟）**：通过 ClassFileLoadHook 在目标方法字节码中插入 `invokestatic` 指令，调用注入到 JVM 的桥接类 `com.xsy.jn.JnBridge` 的 static native 方法，JVM 执行时通过 JNI 回调 .NET 侧 handler。绕过了 onload-only capability 限制，保持纯 C# 实现。
- **MethodEntry**：方法入口插入 `invokestatic JnBridge.onMethodEntry(className, methodName)`
- **MethodExit**：每个 return 指令前插入 `invokestatic JnBridge.onMethodExit(className, methodName)`
- **Exception**：方法体外包裹 try-catch(all)，catch handler 调用 `JnBridge.onException(className, methodName, exception)` 后 `athrow` 重新抛出
- **限制**：构造函数 `<init>` 不插桩 Exception（try-catch 跨越 `super.<init>()` 会导致 stackmap frame 类型不匹配，uninitializedThis vs 类名）
- **前提**：需同时开启 `EnableBytecodeModification` 和 `EnableEventListening`

### English

Event listening is implemented via JVMTI's event callback mechanism. JvmtiEventHub acts as a shared coordinator: both BytecodeModifier and EventListener register callbacks through it, and the Runtime calls `SetEventCallbacks` once (because `SetEventCallbacks` is global and replaces the entire callback table).

**Event callback table layout**: The `jvmtiEventCallbacks` struct is an array of function pointers. Field order is dictated by OpenJDK's XSLT generator (`eventStruct` template in `jvmtiLib.xsl`). The first event is `VMInit` (num=50), so fields are mapped with `FieldOffset = (num-50)*8`.

**Subscription mechanism**: Each event type has an independent `List<Action<T>>`. When the list goes from empty to non-empty, `SetEventNotificationMode(JVMTI_ENABLE, eventKind, NULL)` is called to enable event notification; when it goes from non-empty to empty, the event is disabled. The returned `IDisposable` is used to unsubscribe.

**Metadata reading in callbacks**: In ClassLoad/ClassPrepare callbacks, `GetClassSignature` reads the class signature (e.g. `Ljava/lang/String;`). In ThreadStart/ThreadEnd callbacks, `GetThreadInfo` reads the thread name. Memory allocated by JVMTI must be freed via `Deallocate`.

### Capability limitation

**English**:

JVMTI capabilities fall into two categories:
- **always-on**: Can be added in any JVMTI phase (OnLoad / Live). E.g. `can_redefine_classes`, `can_generate_all_class_hook_events`.
- **onload-only**: Can only be added during `JVMTI_PHASE_ONLOAD`. E.g. `can_generate_method_entry_events`, `can_generate_method_exit_events`, `can_generate_exception_events`.

**Embedded JVM limitation**: After `JNI_CreateJavaVM` returns, the JVM is already in `JVMTI_PHASE_LIVE` and cannot add onload-only capabilities. Calling `AddCapabilities` with an onload-only capability in the live phase returns `JVMTI_ERROR_NOT_AVAILABLE` (98).

**Impact**:
| Event | Required capability | Available? |
|-------|---------------------|------------|
| VMInit / VMDeath | none | Yes |
| ThreadStart / ThreadEnd | none | Yes |
| ClassLoad / ClassPrepare | none | Yes |
| ClassFileLoadHook | `can_generate_all_class_hook_events` (always-on) | Yes |
| MethodEntry | `can_generate_method_entry_events` (onload-only) | **Simulated via 方案 B** |
| MethodExit | `can_generate_method_exit_events` (onload-only) | **Simulated via 方案 B** |
| Exception | `can_generate_exception_events` (onload-only) | **Simulated via 方案 B** |

**Implemented solution (方案 B — bytecode instrumentation)**: Target methods are rewritten via `ClassFileLoadHook` to insert `invokestatic` calls to a bridge class `com.xsy.jn.JnBridge` (injected into the JVM with static native methods). When the JVM executes these calls, it calls back into .NET handlers via JNI. This bypasses the onload-only capability restriction while staying pure C#.
- **MethodEntry**: inserts `invokestatic JnBridge.onMethodEntry(className, methodName)` at method entry.
- **MethodExit**: inserts `invokestatic JnBridge.onMethodExit(className, methodName)` before each return instruction.
- **Exception**: wraps the method body in a try-catch(all) block; the catch handler calls `JnBridge.onException(className, methodName, exception)` then `athrow`.
- **Limitation**: constructors (`<init>`) are not instrumented for Exception (try-catch across `super.<init>()` causes stackmap frame mismatch: uninitializedThis vs class name).
- **Requirement**: both `EnableBytecodeModification` and `EnableEventListening` must be `true`.

---

## 5. Java/.NET 互操作 / Java/.NET Interop

### 中文

Jvm.NET 的互操作分为两个方向：**C#→Java**（通过 JNI 直接调用）和 **Java→C#**（通过 `RegisterNatives` 回调）。默认的 `NativeOnly` 模式纯基于 JNI+ASM，无需额外 Java jar。

**分层架构**（L0-L6）：

| 层 | 内容 |
| --- | --- |
| L0 | JNI 底层扩展：字段访问（GetField/SetField/GetStaticField/SetStaticField）、数组操作（NewArray/GetArrayValues/GetArrayLength/GetObjectArrayElement）、异常获取（GetPendingException/GetExceptionMessage/GetExceptionStackTrace）、类型检查（IsInstanceOf/IsAssignableFrom/GetSuperclass/GetObjectClass）。 |
| L1 | 通用回调 API：`JvmCallbackRegistry` + `IJvmInvoker.RegisterCallback`。通过 `RegisterNatives` 把 .NET 委托注册为 Java 类的 native 方法，Java 代码调用时通过 JNI 函数表回调到 .NET。委托被 `GCHandle.Alloc` 强引用保持，避免 GC 回收。 |
| L2 | 对象生命周期：`JvmClass` / `JvmObject` 实现 `IDisposable`。通过 `FindClass` / `LoadClass` / `NewObject` 创建的实例拥有 JNI 全局引用，`Dispose` 时调用 `DeleteGlobalRef`；通过公共构造函数创建的实例不拥有句柄。`OwnsHandle` 属性指示所有权。 |
| L3 | 类型映射层：`TypeMapper.FromClr` / `ToClr<T>` 处理 CLR↔Java 自动转换。`JavaList<T>` 实现 `IList<T>`，`JavaMap<TKey, TValue>` 实现 `IDictionary<TKey, TValue>`，通过调用 Java 集合方法（add/size/get/put/entrySet/iterator）实现。 |
| L4 | `JavaObject` 抽象基类 + `[JavaClass]` / `[JavaMethod]` / `[JavaField]` 特性。`Create<T>` 工厂方法加载 Java 类、调用构造函数、返回包装对象；`Wrap<T>` 包装已有对象。protected 方法（Invoke/GetField/SetField 等）封装 JNI 调用。 |
| L5 | Source Generator（`Jvm.NET.SourceGenerator` 包）：扫描标记了 `[JavaClass]` 的 `partial` 类，为每个 `[JavaMethod]` / `[JavaField]` 标记的 partial 方法/属性生成 JNI 调用实现。 |
| L6 | 可选 Java 桥接 jar（`WithJar` 模式）：`com.xsy.jn.Bridge` 提供 Java 侧的 .NET 对象代理（`DotNetObject`），通过 native 方法路由到 .NET。`NativeOnly` 模式不需要此 jar。 |

**值类型装箱/拆箱**：Java 集合（List/Map）的元素类型是 `Object`，CLR 值类型（int/long/double 等）传给 Java Object 参数时必须装箱为对应的 java.lang.Integer/Long/Double 等包装类，否则 JVM 会把基本类型的位模式当作对象指针解引用导致崩溃。`TypeMapper.Box` 调用 `Integer.valueOf(int)` 等装箱；`TypeMapper.Unbox` 通过 `getClass().getName()` 识别包装类后调用 `intValue()` 等拆箱。`JavaList<T>` / `JavaMap<K,V>` 在 `typeof(T).IsValueType` 时自动调用 Box/Unbox。

**占位类兜底**：当 `InvokeVirtual` 收到的 `JvmObject.Class.Handle` 为 `IntPtr.Zero`（占位类，用于包装返回值）时，通过 `GetObjectClass` 获取对象的实际运行时类来查找方法 ID。这允许用 `new JvmObject(handle, new JvmClass(IntPtr.Zero, "java/util/Set"))` 包装 `entrySet()` 的返回值后调用 `iterator()`。

### English

Jvm.NET's interop has two directions: **C#→Java** (direct JNI calls) and **Java→C#** (callbacks via `RegisterNatives`). The default `NativeOnly` mode is pure JNI+ASM and requires no additional Java jar.

**Layered architecture** (L0-L6):

| Layer | Content |
| --- | --- |
| L0 | Low-level JNI extensions: field access (GetField/SetField/GetStaticField/SetStaticField), array ops (NewArray/GetArrayValues/GetArrayLength/GetObjectArrayElement), exception access (GetPendingException/GetExceptionMessage/GetExceptionStackTrace), type checks (IsInstanceOf/IsAssignableFrom/GetSuperclass/GetObjectClass). |
| L1 | Generic callback API: `JvmCallbackRegistry` + `IJvmInvoker.RegisterCallback`. Registers .NET delegates as native methods on Java classes via `RegisterNatives`; when Java code calls them, the JNI function table routes back to .NET. Delegates are held by `GCHandle.Alloc` to prevent GC. |
| L2 | Object lifetime: `JvmClass` / `JvmObject` implement `IDisposable`. Instances created via `FindClass` / `LoadClass` / `NewObject` own a JNI global reference, released by `DeleteGlobalRef` on `Dispose`; instances created via the public constructors do not own the handle. `OwnsHandle` indicates ownership. |
| L3 | Type mapping: `TypeMapper.FromClr` / `ToClr<T>` handle CLR↔Java conversion. `JavaList<T>` implements `IList<T>`, `JavaMap<TKey, TValue>` implements `IDictionary<TKey, TValue>`, by calling Java collection methods (add/size/get/put/entrySet/iterator). |
| L4 | `JavaObject` abstract base + `[JavaClass]` / `[JavaMethod]` / `[JavaField]` attributes. The `Create<T>` factory loads the Java class, invokes the constructor, returns the wrapper; `Wrap<T>` wraps an existing object. Protected methods (Invoke/GetField/SetField etc.) wrap JNI calls. |
| L5 | Source Generator (`Jvm.NET.SourceGenerator` package): scans `partial` classes marked with `[JavaClass]` and generates JNI call implementations for each `[JavaMethod]` / `[JavaField]`-marked partial method/property. |
| L6 | Optional Java bridge jar (`WithJar` mode): `com.xsy.jn.Bridge` provides a Java-side .NET object proxy (`DotNetObject`) that routes calls back to .NET via native methods. Not required in `NativeOnly` mode. |

**Value-type boxing/unboxing**: Java collection elements are typed `Object`; CLR value types (int/long/double etc.) must be boxed to java.lang.Integer/Long/Double etc. before being passed to Java Object parameters, otherwise the JVM dereferences the primitive bit pattern as an object pointer and crashes. `TypeMapper.Box` calls `Integer.valueOf(int)` etc. to box; `TypeMapper.Unbox` identifies the wrapper class via `getClass().getName()` and calls `intValue()` etc. to unbox. `JavaList<T>` / `JavaMap<K,V>` auto-invoke Box/Unbox when `typeof(T).IsValueType`.

**Placeholder-class fallback**: When `InvokeVirtual` receives a `JvmObject` whose `Class.Handle` is `IntPtr.Zero` (a placeholder class used to wrap return values), it falls back to `GetObjectClass` to obtain the object's runtime class for method-ID lookup. This lets you wrap the return of `entrySet()` with `new JvmObject(handle, new JvmClass(IntPtr.Zero, "java/util/Set"))` and still call `iterator()` on it.
