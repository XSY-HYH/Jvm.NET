# Jvm.NET API Reference

This document lists the public API surface of `Jvm.NET`. Types live in the `Jvm.NET.Abstractions` namespace unless noted otherwise.

## Table of contents

- [Initialization](#initialization)
- [JDK version abstraction](#jdk-version-abstraction)
- [Runtime facade](#runtime-facade)
- [Invoker](#invoker)
- [Bytecode modifier](#bytecode-modifier)
- [Event listener](#event-listener)
- [Value / handle types](#value--handle-types)
- [Java/.NET interop](#javanet-interop)

## Initialization

### `Jvm.NET.JvmInitializer` (static)

| Member | Description |
| --- | --- |
| `Initialize(JvmInitializationOptions options) : IJvmRuntime` | Boots the JVM in the current process and returns the runtime facade. Idempotent for the same JDK version. |
| `Current : IJvmRuntime?` | Returns the running runtime, or `null` if none. |

### `JvmInitializationOptions`

| Property | Type | Notes |
| --- | --- | --- |
| `JdkBinPath` | `string` | **Required.** Absolute path to the JDK `bin` directory. |
| `Version` | `int` | **Required.** JDK major version (e.g. `21`, `22`, `8`). The core package ships built-in implementations for 21-25; other versions can be added via `JdkImplementationRegistry` — see [EXTENDING.md](EXTENDING.md). |
| `VmArguments` | `IReadOnlyList<string>` | Extra `-X` / `-D` arguments forwarded to `JNI_CreateJavaVM`. |
| `Classpath` | `IReadOnlyList<string>` | Entries appended to `java.class.path` at startup. |
| `EnableBytecodeModification` | `bool` | Loads the JVMTI agent for `ClassFileLoadHook`. Default `true`. |
| `EnableEventListening` | `bool` | Enables JVMTI event callbacks. Default `true`. |
| `RequireJvmti` | `bool` | Throws if JVMTI cannot be obtained. |
| `Interop` | `JvmInteropOptions` | Java/.NET interop options. Defaults to `NativeOnly` mode (pure JNI+ASM, no jar required). See [Java/.NET interop](#javanet-interop). |

### `JvmInteropOptions`

| Property | Type | Notes |
| --- | --- | --- |
| `Mode` | `InteropMode` | Interop mode. Defaults to `NativeOnly` (pure JNI+ASM). Set to `WithJar` to require `BridgeJarPath`. |
| `BridgeJarPath` | `string?` | Absolute path to the Java bridge jar. Required when `Mode` is `WithJar`; automatically appended to `java.class.path`. |
| `AutoInitializeBridge` | `bool` | Whether to auto-initialise the Java bridge class `com.xsy.jn.Bridge` at startup. Only effective in `WithJar` mode. Default `true`. |

### `InteropMode` (enum)

| Value | Notes |
| --- | --- |
| `NativeOnly` | Pure JNI+ASM mode, no Java bridge jar required. Java→C# callbacks via `RegisterNatives`. |
| `WithJar` | Loads the Java bridge jar, providing a Java-side .NET object proxy and advanced callback routing. Requires `BridgeJarPath`. |

## JDK version abstraction

`Jvm.NET` ships default implementations for JDK 21-25 and exposes a pluggable
registry so that third-party packages can provide their own implementations
(e.g. for a new JDK release before `Jvm.NET` ships an update, or for a
vendor-specific JVM).

### `Version` (int)

JDK major version as a plain `int`. The core package ships built-in
implementations for JDK 21, 22, 23, 24, 25. Any other positive integer can
be used as long as a matching `IJdkImplementation` is registered via
`JdkImplementationRegistry` before `Initialize` — see
[EXTENDING.md](EXTENDING.md) for a tutorial (including a JDK 8 example).

### `IJdkImplementation`

Contract implemented by every JDK version adapter. The core package provides
internal implementations under `Jvm.NET.Abstractions.Jdk21`..`Jdk25`
namespaces; third-party packages should expose their own `IJdkImplementation`.

| Member | Description |
| --- | --- |
| `Version : int` | The JDK major version this implementation targets (e.g. 21, 8). |
| `JniVersion : int` | JNI version constant passed to `JNI_CreateJavaVM` / `GetEnv` (e.g. `0x00150000` for JDK 21). |
| `JvmtiVersion : int` | JVMTI version constant passed to `JavaVM->GetEnv` (e.g. `0x30150000` for JDK 21). |
| `CreateRuntime(JvmInitializationOptions) : IJvmRuntime` | Constructs a runtime for this version. |

### `JdkImplementationRegistry` (static)

Global registry consulted by `JvmInitializer.Initialize`. The core package
auto-registers JDK 21-25 via a `ModuleInitializer` when the assembly loads;
third-party packages can override any version by calling `Register` before
`Initialize`.

| Member | Description |
| --- | --- |
| `Register(IJdkImplementation) : void` | Registers (or replaces) the implementation for `implementation.Version`. |
| `Resolve(int version) : IJdkImplementation?` | Returns the registered implementation, or `null`. |

### `JdkRuntimeBase`

Public base class used by the built-in JDK 21-25 implementations. JDK 21-25
share an identical JNI/JVMTI ABI; the only per-version difference is the
version constant passed to `JNI_CreateJavaVM` and `GetEnv`, which is why a
single parameterised `JdkRuntimeBase` suffices. Third-party `IJdkImplementation`
authors may derive from it or implement `IJvmRuntime` directly.

| Constructor | Description |
| --- | --- |
| `JdkRuntimeBase(JvmInitializationOptions, int version, int jniVersion, int jvmtiVersion)` | Creates a runtime with explicit version constants. |
| `JdkRuntimeBase(JvmInitializationOptions, IJdkImplementation)` | Creates a runtime from a registered implementation. |

> The instance then behaves as an `IJvmRuntime` (see below).

## Runtime facade

### `IJvmRuntime`

| Member | Description |
| --- | --- |
| `State : JvmRuntimeState` | `NotStarted` / `Starting` / `Running` / `ShuttingDown` / `Stopped` / `Faulted`. |
| `Version : int` | The JDK major version this runtime was initialised against (e.g. 21, 8). |
| `Invoker : IJvmInvoker` | Accessor for invocation APIs. Throws if not running. |
| `BytecodeModifier : IBytecodeModifier` | Accessor for bytecode APIs. Throws if disabled at startup. |
| `EventListener : IJvmEventListener` | Accessor for event APIs. Throws if disabled at startup. |
| `Start() : void` | Boots the JVM. Idempotent. |
| `Shutdown() : void` | Destroys the JVM. Cannot be restarted. |
| `Dispose() : void` | Calls `Shutdown` if needed. |

## Invoker

### `IJvmInvoker`

| Member | Description |
| --- | --- |
| `LoadJar(string jarPath) : void` | Appends a jar / directory to the runtime classpath. |
| `FindClass(string fqn) : JvmClass?` | Returns an already-loaded class, or `null`. |
| `LoadClass(string fqn) : JvmClass` | Forces the JVM to load a class; throws if not found. |
| `NewObject(JvmClass, string ctorSig, params JvmValue[] args) : JvmObject` | Allocates + constructs. |
| `InvokeStatic(JvmClass, string name, string sig, params JvmValue[] args) : JvmValue` | Calls a static method. |
| `InvokeVirtual(JvmObject, string name, string sig, params JvmValue[] args) : JvmValue` | Calls an instance method. |
| `RunMain(string jarPath, string mainClass, params string[] args) : void` | Loads the jar and invokes `public static void main(String[])`. |
| `NewString(string str) : JvmValue` | Creates a Java `String` from a .NET `string` (modified UTF-8). The returned `JvmValue` holds a JNI local reference. |
| `GetString(IntPtr javaStringHandle) : string` | Reads a Java `String` (modified UTF-8) into a .NET `string`. |
| `NewStringArray(string[] args) : JvmValue` | Creates a Java `String[]` for passing to methods like `main`. |
| `GetField(JvmObject, string name, string sig) : JvmValue` | Reads an instance field. `sig` is the field type signature (e.g. `I`, `Ljava/lang/String;`). |
| `SetField(JvmObject, string name, string sig, JvmValue value) : void` | Writes an instance field. |
| `GetStaticField(JvmClass, string name, string sig) : JvmValue` | Reads a static field. |
| `SetStaticField(JvmClass, string name, string sig, JvmValue value) : void` | Writes a static field. |
| `IsInstanceOf(JvmObject instance, JvmClass clazz) : bool` | Checks whether the object is an instance of the given class. |
| `IsAssignableFrom(JvmClass from, JvmClass to) : bool` | Checks whether `from` is assignable to `to`. |
| `GetSuperclass(JvmClass clazz) : JvmClass?` | Returns the superclass. Returns `null` for `Object` or interfaces. |
| `GetObjectClass(JvmObject instance) : JvmClass` | Returns the runtime class of the object. |
| `GetArrayLength(JvmValue array) : int` | Returns the array length. |
| `GetObjectArrayElement(JvmValue array, int index) : JvmValue` | Reads an element of an object array. |
| `SetObjectArrayElement(JvmValue array, int index, JvmValue value) : void` | Writes an element of an object array. |
| `NewArray<T>(T[] values) : JvmValue` | Creates a Java primitive array filled with `values`. `T` must be `bool`/`byte`/`char`/`short`/`int`/`long`/`float`/`double`. |
| `GetArrayValues<T>(JvmValue array) : T[]` | Reads all elements of a Java primitive array. |
| `NewObjectArray(JvmClass elementClass, JvmValue[] elements) : JvmValue` | Creates a Java object array. |
| `GetPendingException() : JvmObject?` | Returns the current pending exception (without clearing). Returns `null` when no exception is pending. |
| `GetExceptionMessage(JvmObject exception) : string` | Calls `Throwable.getMessage()` and returns the message. |
| `GetExceptionStackTrace(JvmObject exception) : string` | Calls `Throwable.getStackTrace()` and returns a stack-trace string. |
| `RegisterCallback(JvmClass clazz, string methodName, string sig, Delegate callback) : void` | Registers a native method on `clazz` so Java code can call back into the .NET delegate. The delegate is held by a strong reference until `UnregisterCallbacks` is called or the Invoker is disposed. |
| `UnregisterCallbacks(JvmClass clazz) : void` | Unregisters all native methods on `clazz` previously registered via `RegisterCallback`. |

> Method / constructor signatures use JNI form, e.g. `(Ljava/lang/String;I)V`.

## Bytecode modifier

### `IBytecodeModifier`

| Member | Description |
| --- | --- |
| `RegisterTransformer(IBytecodeTransformer) : IDisposable` | Adds a transformer; dispose to remove. |
| `RetransformClasses(IEnumerable<JvmClass>) : void` | Triggers JVMTI `RetransformClasses`. |
| `RedefineClasses(IEnumerable<KeyValuePair<JvmClass, byte[]>>) : void` | Replaces bytecode directly via `RedefineClasses`. |

### `IBytecodeTransformer`

| Member | Description |
| --- | --- |
| `Name : string` | Diagnostic name. |
| `Transform(string className, byte[] original) : byte[]?` | Returns rewritten bytes or `null` to keep the original. |

## Event listener

### `IJvmEventListener`

Each method returns an `IDisposable` that unregisters the handler (and turns off the JVMTI event bit when no subscribers remain):

- `SubscribeMethodEntry(Action<MethodEntryEventData>)`
- `SubscribeMethodExit(Action<MethodExitEventData>)`
- `SubscribeClassLoad(Action<ClassLoadEventData>)`
- `SubscribeClassPrepare(Action<ClassPrepareEventData>)`
- `SubscribeThreadStart(Action<ThreadStartEventData>)`
- `SubscribeThreadEnd(Action<ThreadEndEventData>)`
- `SubscribeVmInit(Action<VmInitEventData>)`
- `SubscribeVmDeath(Action<VmDeathEventData>)`
- `SubscribeException(Action<ExceptionEventData>)`

> **Note on MethodEntry / MethodExit / Exception:**
> These three events are simulated via bytecode instrumentation (方案 B). An embedded JVM started through `JNI_CreateJavaVM` enters the `live` phase immediately, so the required JVMTI capabilities (`can_generate_method_entry_events`, `can_generate_method_exit_events`, `can_generate_exception_events`) — which are `onload-only` — cannot be added. Instead, `Jvm.NET` injects a bridge class (`com.xsy.jn.JnBridge`) into the JVM and rewrites target methods at load time via `ClassFileLoadHook`:
> - **MethodEntry**: inserts `invokestatic JnBridge.onMethodEntry(className, methodName)` at method entry.
> - **MethodExit**: inserts `invokestatic JnBridge.onMethodExit(className, methodName)` before each return instruction.
> - **Exception**: wraps the method body in a `try-catch(all)` block; the catch handler calls `JnBridge.onException(className, methodName, exception)` then re-throws.
>
> Requires both `EnableBytecodeModification` and `EnableEventListening` to be `true`. Constructor methods (`<init>`) are instrumented for MethodEntry/MethodExit but **not** for Exception (try-catch across `super.<init>()` causes stackmap frame mismatch).

### Event data types

| Type | Key fields |
| --- | --- |
| `MethodEntryEventData` | `Class : JvmClass`, `MethodName : string`, `MethodSignature : string` |
| `MethodExitEventData` | `Class : JvmClass`, `MethodName`, `MethodSignature`, `ReturnValue : JvmValue`, `WasException : bool` |
| `ExceptionEventData` | `Exception : JvmObject`, `ThrowingClass : JvmClass?`, `MethodName`, `MethodSignature` |
| `ClassLoadEventData` | `Class : JvmClass` |
| `ClassPrepareEventData` | `Class : JvmClass` |
| `ThreadStartEventData` | `Thread : JvmObject` |
| `ThreadEndEventData` | `Thread : JvmObject` |
| `VmInitEventData` | `Thread : JvmObject` |
| `VmDeathEventData` | _(empty)_ |

## Value / handle types

### `JvmValue` (readonly struct)

Discriminated union of every primitive JVM value plus object references. Factory helpers: `FromBoolean`, `FromByte`, `FromChar`, `FromShort`, `FromInt`, `FromLong`, `FromFloat`, `FromDouble`, `FromObject(IntPtr)`, `Null`.

Accessors: `AsBoolean`, `AsByte`, `AsChar`, `AsShort`, `AsInt`, `AsLong`, `AsFloat`, `AsDouble`. The `ObjectHandle : IntPtr` property returns the object reference handle. The `Type : JvmValueType` property returns the currently held value type.

### `JvmClass` / `JvmObject` (implement `IDisposable`)

Wrappers around raw `jclass` / `jobject` handles. Both implement `IDisposable`.

**Handle ownership**:
- Instances created via `IJvmInvoker.FindClass` / `LoadClass` / `NewObject` **own** a JNI global reference; `Dispose` calls `DeleteGlobalRef`.
- Instances created via the public constructors `new JvmClass(handle, name)` / `new JvmObject(handle, clazz)` do **not** own the handle; `Dispose` is a no-op. Use this to wrap local references returned by `InvokeVirtual` / `InvokeStatic` (local-reference lifetime is managed by the caller).
- The `OwnsHandle : bool` property indicates whether the instance owns its handle.

**Fields**:
- `JvmClass` — `Handle : IntPtr`, `Name : string` (internal name, slash-separated), `OwnsHandle : bool`.
- `JvmObject` — `Handle : IntPtr`, `Class : JvmClass`, `OwnsHandle : bool`.

> When `JvmObject.Class.Handle` is `IntPtr.Zero` (a placeholder class), `IJvmInvoker.InvokeVirtual` falls back to `GetObjectClass` to obtain the object's runtime class for method-ID lookup. This lets you wrap a return value with `new JvmObject(handle, new JvmClass(IntPtr.Zero, "java/util/Set"))` and still call methods on it.

## Java/.NET interop

`Jvm.NET` provides bidirectional Java/.NET interop. The default `NativeOnly` mode is pure JNI+ASM and requires no additional Java jar.

### `TypeMapper` (static)

Automatic conversion between CLR and Java types.

| Method | Description |
| --- | --- |
| `FromClr(object? value, IJvmInvoker) : JvmValue` | Converts a CLR value to `JvmValue`. Supports `string`, all primitive types, `JvmObject`, `JavaObject` subclasses. `null` becomes `JvmValue.Null`. |
| `ToClr<T>(JvmValue value, IJvmInvoker) : T?` | Converts a `JvmValue` to a CLR type. When `T` is `string`, calls `GetString`; for value types, calls the matching `As*` method; for `JavaObject` subclasses, calls `Wrap`. |
| `ToString(JvmValue value, IJvmInvoker) : string` | Calls `toString()` on the Java object. |
| `ToStringArray(JvmValue arrayValue, IJvmInvoker) : string[]` | Reads a Java `String[]` into a .NET `string[]`. |
| `Box(JvmValue value, IJvmInvoker) : JvmValue` | Boxes a primitive into its Java wrapper class (e.g. `int` → `Integer.valueOf(int)`). Used when passing CLR value types to Java `Object` parameters. |
| `Unbox(JvmValue value, IJvmInvoker) : JvmValue` | Unboxes a Java wrapper class into a primitive (e.g. `Integer` → `intValue()`). Identifies the wrapper class via `getClass().getName()`. |

### `JavaList<T>` (implements `IList<T>`, `IDisposable`)

.NET wrapper for a Java `java.util.List`, providing `IList<T>` semantics.

| Member | Description |
| --- | --- |
| `JavaList(IJvmInvoker invoker, JvmObject list)` | Wraps an existing Java List object. |
| `NewArrayList(IJvmInvoker invoker) : JavaList<T>` | Creates a new `java.util.ArrayList`. |
| `UnderlyingObject : JvmObject` | The wrapped Java object. |
| `Count`, `IsReadOnly`, `this[int]`, `Add`, `Clear`, `Contains`, `CopyTo`, `IndexOf`, `Insert`, `Remove`, `RemoveAt` | Standard `IList<T>` members. |

> When `T` is a CLR value type, `Add` / the indexer automatically call `TypeMapper.Box`; `ConvertFromJava` calls `TypeMapper.Unbox`.

### `JavaMap<TKey, TValue>` (implements `IDictionary<TKey, TValue>`, `IDisposable`)

.NET wrapper for a Java `java.util.Map`, providing `IDictionary<TKey, TValue>` semantics.

| Member | Description |
| --- | --- |
| `JavaMap(IJvmInvoker invoker, JvmObject map)` | Wraps an existing Java Map object. |
| `NewHashMap(IJvmInvoker invoker) : JavaMap<TKey, TValue>` | Creates a new `java.util.HashMap`. |
| `UnderlyingObject : JvmObject` | The wrapped Java object. |
| `Count`, `IsReadOnly`, `this[TKey]`, `Keys`, `Values`, `Add`, `Clear`, `Contains`, `ContainsKey`, `CopyTo`, `GetEnumerator`, `Remove`, `TryGetValue` | Standard `IDictionary<TKey, TValue>` members. |

> Like `JavaList<T>`, when `TKey` / `TValue` are CLR value types, boxing/unboxing is automatic. `GetEnumerator` iterates via `entrySet().iterator()`, calling `getKey()` / `getValue()` on each `Map.Entry`.

### `JavaObject` (abstract base, implements `IDisposable`)

Base class for .NET wrappers of Java objects. Users subclass it to hand-write C# wrappers for Java classes, or use the Source Generator to auto-generate them.

**Usage**:
1. Subclass `JavaObject` and mark the Java class name with `[JavaClass]`.
2. Create new instances via the `Create<T>` factory, or wrap existing objects with `Wrap<T>`.
3. Operate on the object via `Invoke` / `GetField` / `SetField` and other protected methods.

| Member | Description |
| --- | --- |
| `Handle : JvmObject` | The wrapped Java object handle. Throws if not initialised. |
| `Invoker : IJvmInvoker` | The associated invoker. Throws if not initialised. |
| `JavaClassName : string` (protected virtual) | Fully-qualified Java class name (dot-separated). Defaults to the `[JavaClass]` attribute; can be overridden. |
| `Create<T>(IJvmInvoker, string ctorSig, params JvmValue[] args) : T` (static) | Creates a new `T`: loads the Java class, invokes the constructor, returns the wrapped object. |
| `Wrap<T>(IJvmInvoker, JvmObject obj) : T` (static) | Wraps an existing Java object. |
| `Invoke(string name, string sig, params JvmValue[] args) : JvmValue` (protected) | Calls an instance method. |
| `Invoke<T>(string name, string sig, params JvmValue[] args) : T?` (protected) | Calls an instance method and converts the return value via `TypeMapper.ToClr<T>`. |
| `InvokeStatic(string name, string sig, params JvmValue[] args) : JvmValue` (protected) | Calls a static method. |
| `InvokeStatic<T>(...) : T?` (protected) | Calls a static method and converts the return value. |
| `GetField(string name, string sig) : JvmValue` (protected) | Reads an instance field. |
| `GetField<T>(string name, string sig) : T?` (protected) | Reads an instance field and converts. |
| `SetField(string name, string sig, JvmValue value) : void` (protected) | Writes an instance field. |
| `GetStaticField(string name, string sig) : JvmValue` (protected) | Reads a static field. |
| `SetStaticField(string name, string sig, JvmValue value) : void` (protected) | Writes a static field. |
| `IsInstanceOf(string javaClassName) : bool` | Checks whether this object is an instance of the given Java class. |
| `Dispose() : void` | Disposes the wrapped `JvmObject`. |

### Attribute classes

| Attribute | Target | Description |
| --- | --- | --- |
| `[JavaClass(string name)]` | Class | Marks the Java class name for a C# class (e.g. `java.util.ArrayList`). Read automatically by `JavaObject`. |
| `[JavaMethod(string name, string signature)]` | Method | Marks a C# method as mapping to a Java method. The `IsStatic` property indicates a static method. The Source Generator reads this attribute to generate call code. |
| `[JavaField(string name, string signature)]` | Property | Marks a C# property as mapping to a Java field. The `IsStatic` property indicates a static field. The Source Generator reads this attribute to generate access code. |

### Source Generator (`Jvm.NET.SourceGenerator` package)

After referencing `Jvm.NET.SourceGenerator`, `partial` classes marked with `[JavaClass]` / `[JavaMethod]` / `[JavaField]` auto-generate strongly-typed wrapper code. You only declare the partial method signatures; the SG emits the JNI call implementation.

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

### Java bridge jar (`WithJar` mode)

The optional Java bridge jar (sources under `src/Jvm.NET.JavaBridge/`) provides a Java-side .NET object proxy and advanced callback routing.

**Core classes**:
- `com.xsy.jn.Bridge`: Java bridge entry point with native methods (`onBridgeInitialized`, `registerDotNetObject`, `invokeDotNetMethod`, `getDotNetField`, `setDotNetField`, `isInstanceOf`). The static initialiser calls `onBridgeInitialized()` to notify the .NET side.
- `com.xsy.jn.DotNetObject`: Java proxy for a .NET object, routing calls back to .NET via Bridge native methods. `finalize()` automatically calls `unregisterDotNetObject`.

**Build**: run `src/Jvm.NET.JavaBridge/build.ps1` (Windows) or `build.sh` (Linux/macOS) to produce `dist/jn-bridge.jar`. Requires `javac` and `jar` (from `JAVA_HOME` or `PATH`).
