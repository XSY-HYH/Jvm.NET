# Extending Jvm.NET — Custom JDK Implementations

`Jvm.NET` ships built-in support for JDK 21-25. This document describes how a
third-party package can register its own `IJdkImplementation` to support any
other JDK version (for example **JDK 8**) or to override the built-in behaviour
for an already-supported version.

---

## How registration works

1. The core package auto-registers `Jdk21Implementation`..`Jdk25Implementation`
   via a `ModuleInitializer` when the `Jvm.NET` assembly loads.
2. `JvmInitializer.Initialize` looks up `JdkImplementationRegistry.Resolve(options.Version)`.
3. If a third-party package registers an implementation for the same `Version`
   **before** `Initialize` is called, the third-party implementation wins
   (the registry is a `ConcurrentDictionary<int, IJdkImplementation>` keyed
   by major version).
4. If `Resolve` returns `null`, `Initialize` throws
   `NotSupportedException` listing the requested version.

> `Version` is now a plain `int` (JDK major version). This lets third-party
> packages register any version number without waiting for the core package
> to extend an enum.

---

## The three extension points

| Type | Purpose |
| --- | --- |
| `IJdkImplementation` | Contract: provide `Version`, `JniVersion`, `JvmtiVersion`, and a `CreateRuntime` factory. |
| `JdkImplementationRegistry` | Static registry consulted by `JvmInitializer`. Call `Register` before `Initialize`. |
| `JdkRuntimeBase` | Public base class that implements `IJvmRuntime` with parameterised version constants. Reuse it if your target JDK has the same JNI/JVMTI ABI as JDK 21+. |

See [API.md](API.md#jdk-version-abstraction) for the full signature reference.

---

## Quick start: override JDK 21

The simplest example — replace the built-in JDK 21 implementation with a
custom one (e.g. to add extra logging or vendor-specific options).

```csharp
using Jvm.NET.Abstractions;

internal sealed class MyJdk21Implementation : IJdkImplementation
{
    public int Version => 21;
    public int JniVersion => 0x00150000;
    public int JvmtiVersion => 0x30150000;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
    {
        // Add your own logging / option massaging here.
        return new JdkRuntimeBase(options, this);
    }
}

public static class MyPackageInitializer
{
    public static void Register()
    {
        // This overrides the built-in Jdk21Implementation because both
        // share Version == 21. Call this BEFORE JvmInitializer.Initialize.
        JdkImplementationRegistry.Register(new MyJdk21Implementation());
    }
}
```

---

## Full example: add JDK 8 support

JDK 8 is **not** shipped by the core package. A third-party package can add
it. Two important differences from JDK 21+:

1. **Version-number layout changed in JDK 9.** Pre-9 versions use the legacy
   `0x0001000X` layout, not the modern `major << 16` layout.
2. **JVMTI version constant** also follows the legacy layout. Verify against
   the `jvmti.h` shipped with your target JDK — values below are from
   OpenJDK 8u.

### Step 1 — Create the implementation class

```csharp
using Jvm.NET.Abstractions;

namespace YourPackage.Jdk8;

internal sealed class Jdk8Implementation : IJdkImplementation
{
    // JDK major version. Any positive int is accepted by the registry.
    public int Version => 8;

    // JNI_VERSION_1_8 from jni.h (legacy layout, NOT 0x00080000).
    public int JniVersion => 0x00010008;

    // JVMTI_VERSION_1_8 from jvmti.h (0x80010008 in OpenJDK 8u).
    // Note the 0x80000000 interface-type bit — different from JDK 21's
    // 0x30000000 bit. Always confirm against your JDK's jvmti.h.
    public int JvmtiVersion => 0x80010008;

    public IJvmRuntime CreateRuntime(JvmInitializationOptions options)
        => new JdkRuntimeBase(options, this);
}
```

### Step 2 — Register it before `Initialize`

Two common patterns:

**A. Library-side helper** — the consumer calls it explicitly:

```csharp
public static class JvmNetJdk8
{
    public static void Use()
        => JdkImplementationRegistry.Register(new Jdk8Implementation());
}
```

```csharp
// In your application startup:
JvmNetJdk8.Use();

var runtime = JvmInitializer.Initialize(new JvmInitializationOptions
{
    JdkBinPath = @"C:\Program Files\Java\jdk-8\bin",
    Version    = 8,
});
```

**B. Module initializer** — register automatically when your package loads.
Use a static constructor or `[ModuleInitializer]` so consumers don't have to
call anything:

```csharp
using System.Runtime.CompilerServices;

internal static class Jdk8Registration
{
#pragma warning disable CA2255 // ModuleInitializer in a library is intentional.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register()
        => JdkImplementationRegistry.Register(new Jdk8Implementation());
}
```

### Step 3 — Run

```csharp
var runtime = JvmInitializer.Initialize(new JvmInitializationOptions
{
    JdkBinPath = @"C:\Program Files\Java\jdk-8\bin",
    Version    = 8,
});

runtime.Invoker.RunMain("myapp.jar", "com.example.Main", "arg1", "arg2");
```

---

## Version number reference

| JDK | `Version` | `JniVersion`  | `JvmtiVersion` | Notes |
| --- | --- | --- | --- | --- |
| 8   | `8`  | `0x00010008` | `0x80010008` | Legacy layout. Confirm in OpenJDK 8 `jvmti.h`. |
| 9   | `9`  | `0x00090000` | — | Modern layout starts. |
| 11  | `11` | `0x000B0000` | `0x300B0000` | LTS. |
| 17  | `17` | `0x00110000` | `0x30110000` | LTS. |
| 21  | `21` | `0x00150000` | `0x30150000` | LTS. Built-in. |
| 22  | `22` | `0x00160000` | `0x30160000` | Built-in. |
| 23  | `23` | `0x00170000` | `0x30170000` | Built-in. |
| 24  | `24` | `0x00180000` | `0x30180000` | Built-in. |
| 25  | `25` | `0x00190000` | `0x30190000` | LTS. Built-in. |

> **Modern layout (JDK 9+):** `JniVersion = major << 16`,
> `JvmtiVersion = 0x30000000 | (major << 16)`.
>
> **Legacy layout (JDK ≤ 8):** `JniVersion = 0x0001000X` where `X` is the
> minor version (e.g. 8 → `0x00010008`). The JVMTI version constant is
> published per-version in `jvmti.h` and does **not** follow a single
> formula — always verify against the target JDK's headers.

---

## When to implement `IJvmRuntime` directly

`JdkRuntimeBase` assumes the target JDK exposes the same JNI/JVMTI function
table as JDK 21. This holds for every OpenJDK from 8 onward — the function
tables only grow, never shrink — so `JdkRuntimeBase` works for JDK 8 too.

You should implement `IJvmRuntime` directly only if you need to:

- Use a non-OpenJDK ABI (e.g. a stripped-down embedded JVM).
- Hook into a different lifecycle (e.g. attaching to an already-running JVM
  via `JNI_GetCreatedJavaVMs` instead of `JNI_CreateJavaVM`).
- Replace the invoker / bytecode modifier / event listener with your own
  implementations instead of the built-in `Jdk21Invoker` /
  `Jdk21BytecodeModifier` / `Jdk21EventListener`.

---

## Caveats

- **`onload-only` capabilities** (`can_generate_method_entry_events` etc.)
  still require an `-agentpath:` native agent. `Jvm.NET`'s bytecode
  instrumentation fallback (方案 B) is used regardless of JDK version.
- **Class file version**: the built-in ASM port targets modern class file
  versions. JDK 8 class files (version 52) are read correctly, but if you
  emit class files via `Jvm.NET.Asm` make sure to set `ClassWriter` to the
  correct version.
- **Registration order**: `Register` replaces any prior implementation for
  the same `Version`. If multiple third-party packages register JDK 8, the
  last one registered wins. Load order is determined by .NET's assembly
  load order — if you need determinism, call `Register` explicitly at app
  startup.
