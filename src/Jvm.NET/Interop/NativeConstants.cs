using System.Runtime.InteropServices;

namespace Jvm.NET.Interop;

/// <summary>
/// JNI / JVMTI constants mirroring <c>jni.h</c> and <c>jvmti.h</c> from OpenJDK 21.
/// Kept in a single place so the version abstraction layers can reference them
/// without duplicating magic numbers.
/// </summary>
internal static class NativeConstants
{
    // ---- JNI return codes (jni.h) ----
    public const int JNI_OK = 0;
    public const int JNI_ERR = -1;
    public const int JNI_EDETACHED = -2;
    public const int JNI_EVERSION = -3;
    public const int JNI_ENOMEM = -4;
    public const int JNI_EEXIST = -5;
    public const int JNI_EINVAL = -6;

    // ---- JNI version constants ----
    // 注意：自 JDK 9 起 JNI 版本号布局变为 (major << 16)，即 0x00MM0000。
    // JDK 1.8 之前是 0x0001000X 布局，不要混淆。参考 jni.h。
    public const int JNI_VERSION_1_8 = 0x00010008;
    public const int JNI_VERSION_9  = 0x00090000;
    public const int JNI_VERSION_10 = 0x000A0000;
    public const int JNI_VERSION_19 = 0x00130000;
    public const int JNI_VERSION_21 = 0x00150000;

    /// <summary>
    /// Minimum JNI version we ask for when invoking GetEnv. JDK 21 supports 0x15 directly.
    /// </summary>
    public const int RequiredJniVersion = JNI_VERSION_21;

    // ---- JVMTI version (jvmti.h) ----
    /// <summary>
    /// JVMTI version for JDK 21. Layout: 0x30000000 (interface type) | (major &lt;&lt; 16) | (minor &lt;&lt; 8).
    /// Reference: src/hotspot/share/prims/jvmtiH.xsl defines JVMTI_VERSION_21 = 0x30150000.
    /// </summary>
    public const int JVMTI_VERSION_21 = 0x30150000;

    // ---- JVMTI event kinds (subset, see jvmtiEvent enum) ----
    // 编号来自 jvmti.xml 中 <event> 元素的 num 属性。
    // 注意：MethodEntry/MethodExit/Exception/SingleStep 等事件需要 onload-only capability，
    // 嵌入式 JVM 在 live phase 无法启用这些事件。
    public const int JVMTI_EVENT_VM_INIT = 50;
    public const int JVMTI_EVENT_VM_DEATH = 51;
    public const int JVMTI_EVENT_THREAD_START = 52;
    public const int JVMTI_EVENT_THREAD_END = 53;
    public const int JVMTI_EVENT_CLASS_FILE_LOAD_HOOK = 54;
    public const int JVMTI_EVENT_CLASS_LOAD = 55;
    public const int JVMTI_EVENT_CLASS_PREPARE = 56;
    public const int JVMTI_EVENT_VM_START = 57;
    public const int JVMTI_EVENT_EXCEPTION = 58;            // [需 onload-only capability]
    public const int JVMTI_EVENT_SINGLE_STEP = 60;          // [需 onload-only capability]
    public const int JVMTI_EVENT_FRAME_POP = 61;            // [需 onload-only capability]
    public const int JVMTI_EVENT_BREAKPOINT = 62;           // [需 onload-only capability]
    public const int JVMTI_EVENT_FIELD_ACCESS = 63;         // [需 onload-only capability]
    public const int JVMTI_EVENT_FIELD_MODIFICATION = 64;   // [需 onload-only capability]
    public const int JVMTI_EVENT_METHOD_ENTRY = 65;         // [需 onload-only capability]
    public const int JVMTI_EVENT_METHOD_EXIT = 66;          // [需 onload-only capability]
    public const int JVMTI_EVENT_NATIVE_METHOD_BIND = 67;
}

/// <summary>
/// Delegate signatures for the three exported entry points of <c>jvm.dll</c> / <c>libjvm.so</c>.
/// We resolve them through <see cref="INativeLibraryLoader.GetExport"/> at runtime and marshal
/// to these delegates — this avoids hard-coding the library path in <c>DllImport</c>.
/// </summary>
internal static class JniDelegates
{
    // Cdecl is used because on x64/arm64 (the only platforms we target — see csproj
    // <Platforms>x64;arm64</Platforms>) Cdecl and Stdcall collapse to the same ABI.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int JNI_CreateJavaVMDelegate(out IntPtr javaVm, out IntPtr jniEnv, IntPtr args);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int JNI_GetDefaultJavaVMInitArgsDelegate(IntPtr args);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int JNI_GetCreatedJavaVMsDelegate(IntPtr[] vmBuf, int bufLen, out int nVMs);
}
