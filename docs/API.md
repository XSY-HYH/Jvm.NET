# Jvm.NET API Reference

This document lists the public API surface of `Jvm.NET`. Types live in the `Jvm.NET.Abstractions` namespace unless noted otherwise.

## Table of contents

- [Initialization](#initialization)
- [Runtime facade](#runtime-facade)
- [Invoker](#invoker)
- [Bytecode modifier](#bytecode-modifier)
- [Event listener](#event-listener)
- [Value / handle types](#value--handle-types)

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
| `Version` | `JdkVersion` | **Required.** Currently only `Jdk21`. |
| `VmArguments` | `IReadOnlyList<string>` | Extra `-X` / `-D` arguments forwarded to `JNI_CreateJavaVM`. |
| `Classpath` | `IReadOnlyList<string>` | Entries appended to `java.class.path` at startup. |
| `EnableBytecodeModification` | `bool` | Loads the JVMTI agent for `ClassFileLoadHook`. Default `true`. |
| `EnableEventListening` | `bool` | Enables JVMTI event callbacks. Default `true`. |
| `RequireJvmti` | `bool` | Throws if JVMTI cannot be obtained. |

## Runtime facade

### `IJvmRuntime`

| Member | Description |
| --- | --- |
| `State : JvmRuntimeState` | `NotStarted` / `Starting` / `Running` / `ShuttingDown` / `Stopped` / `Faulted`. |
| `Version : JdkVersion` | The JDK version this runtime was initialised against. |
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
| `GetString(IntPtr javaStringHandle) : string` | Reads a Java `String` (modified UTF-8) into a .NET `string`. |
| `NewStringArray(string[] args) : JvmValue` | Creates a Java `String[]` for passing to methods like `main`. |

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

### `JvmClass` / `JvmObject`

Lightweight wrappers around raw `jclass` / `jobject` handles. Lifetime is owned by the JVM — the wrappers do **not** call `DeleteLocalRef`.

- `JvmClass` — `Handle : IntPtr`, `Name : string` (internal name, slash-separated).
- `JvmObject` — `Handle : IntPtr`, `Class : JvmClass`.
