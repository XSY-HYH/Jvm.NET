using System.Runtime.InteropServices;

namespace Jvm.NET.Interop.Jvmti;

//
// JVMTI 辅助结构体。布局逐字段对应 OpenJDK 21 的 src/hotspot/share/prims/jvmti.h。
// 注意：所有 jchar*/jbyte*/char* 都用 IntPtr 而非 string，因为 JVMTI 是由 native
// 侧分配内存（需要 Deallocate 释放）。
//

/// <summary>
/// Capability bitfield — see <c>jvmtiCapabilities</c> in <c>jvmti.h</c>.
/// Laid out as 16 uint32 fields = 512 bits (only the first ~40 are defined).
/// Bit numbering is GLOBAL (bit 0 = Word0 bit 0, bit 32 = Word1 bit 0, ...).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JvmtiCapabilities
{
    public uint Word0;
    public uint Word1;
    public uint Word2;
    public uint Word3;
    public uint Word4;
    public uint Word5;
    public uint Word6;
    public uint Word7;
    public uint Word8;
    public uint Word9;
    public uint Word10;
    public uint Word11;
    public uint Word12;
    public uint Word13;
    public uint Word14;
    public uint Word15;

    // Word0 bit indices (capabilities 0..31)
    // 顺序逐字段对应 jvmti.xml 中 <capabilitiestypedef id="jvmtiCapabilities"> 的 <capabilityfield> 顺序。
    // 标 [onload-only] 的 capability 只能在 JVMTI_PHASE_ONLOAD 期添加；
    // 嵌入式 JVM 经过 JNI_CreateJavaVM 返回后已处于 live phase，无法再添加这些 capability。
    public const int CanTagObjects = 0;
    public const int CanGenerateFieldModificationEvents = 1;        // [onload-only, solo]
    public const int CanGenerateFieldAccessEvents = 2;              // [onload-only, solo]
    public const int CanGetBytecodes = 3;
    public const int CanGetSyntheticAttribute = 4;
    public const int CanGetOwnedMonitorInfo = 5;                    // [onload-only]
    public const int CanGetCurrentContendedMonitor = 6;             // [onload-only]
    public const int CanGetMonitorInfo = 7;
    public const int CanPopFrame = 8;                               // [onload-only]
    public const int CanRedefineClasses = 9;
    public const int CanSignalThread = 10;
    public const int CanGetSourceFileName = 11;
    public const int CanGetLineNumbers = 12;
    public const int CanGetSourceDebugExtension = 13;               // [onload-only]
    public const int CanAccessLocalVariables = 14;                  // [onload-only]
    public const int CanMaintainOriginalMethodOrder = 15;           // [onload-only]
    public const int CanGenerateSingleStepEvents = 16;              // [onload-only]
    public const int CanGenerateExceptionEvents = 17;               // [onload-only]
    public const int CanGenerateFramePopEvents = 18;                // [onload-only]
    public const int CanGenerateBreakpointEvents = 19;              // [onload-only, solo]
    public const int CanSuspend = 20;                               // [solo]
    public const int CanRedefineAnyClass = 21;
    public const int CanGetCurrentThreadCpuTime = 22;
    public const int CanGetThreadCpuTime = 23;
    public const int CanGenerateMethodEntryEvents = 24;             // [onload-only]
    public const int CanGenerateMethodExitEvents = 25;              // [onload-only]
    public const int CanGenerateAllClassHookEvents = 26;
    public const int CanGenerateCompiledMethodLoadEvents = 27;
    public const int CanGenerateMonitorEvents = 28;
    public const int CanGenerateVmObjectAllocEvents = 29;
    public const int CanGenerateNativeMethodBindEvents = 30;
    public const int CanGenerateGarbageCollectionEvents = 31;

    // Word1 bit indices (capabilities 32..44)
    public const int CanGenerateObjectFreeEvents = 32;
    public const int CanForceEarlyReturn = 33;                      // [onload-only]
    public const int CanGetOwnedMonitorStackDepthInfo = 34;         // [onload-only]
    public const int CanGetConstantPool = 35;
    public const int CanSetNativeMethodPrefix = 36;
    public const int CanRetransformClasses = 37;
    public const int CanRetransformAnyClass = 38;
    public const int CanGenerateResourceExhaustionHeapEvents = 39;
    public const int CanGenerateResourceExhaustionThreadsEvents = 40;
    public const int CanGenerateEarlyVmstart = 41;                  // [onload-only]
    public const int CanGenerateEarlyClassHookEvents = 42;          // [onload-only]
    public const int CanGenerateSampledObjectAllocEvents = 43;      // [solo]
    public const int CanSupportVirtualThreads = 44;

    // 注意：jvmtiCapabilities 结构中 *不存在* can_generate_class_load_events。
    // ClassLoad / ClassPrepare / VMInit / VMDeath / ThreadStart / ThreadEnd 事件
    // 不需要任何 capability 即可在 live phase 启用。

    /// <summary>Sets the given GLOBAL capability bit (bit index 0..511).</summary>
    public void SetCapability(int globalBitIndex)
    {
        var word = globalBitIndex >> 5;
        var bit = globalBitIndex & 31;
        this[word] |= 1u << bit;
    }

    /// <summary>Returns <c>true</c> if the given GLOBAL capability bit is set.</summary>
    public bool HasCapability(int globalBitIndex)
    {
        var word = globalBitIndex >> 5;
        var bit = globalBitIndex & 31;
        return (this[word] & (1u << bit)) != 0;
    }

    private unsafe ref uint this[int wordIndex]
    {
        get
        {
            fixed (uint* p = &Word0)
                return ref p[wordIndex];
        }
    }
}

/// <summary>
/// <c>jvmtiThreadInfo</c> — returned by <c>GetThreadInfo</c>. The <c>Name</c>
/// pointer is allocated by JVMTI and must be freed via <c>Deallocate</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JvmtiThreadInfo
{
    public IntPtr Name;             // char* (UTF-8)
    public int Priority;            // jint
    public byte IsDaemon;           // jboolean
    public IntPtr ThreadGroup;      // jthreadGroup
    public IntPtr ContextClassLoader; // jobject

    public string? GetName() => Name == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Name);
}

/// <summary><c>jvmtiLineNumberEntry</c> — used by <c>GetLineNumberTable</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JvmtiLineNumberEntry
{
    public long StartLocation;      // jlocation
    public int LineNumber;          // jint
}

/// <summary><c>jvmtiMethodParametersEntry</c> — used by <c>GetMethodParameters</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JvmtiMethodParametersEntry
{
    public IntPtr Name;             // char*
    public int Flags;               // jint
}

/// <summary>
/// <c>jvmtiClassDefinition</c> — input to <c>RedefineClasses</c>.
/// The <c>ClassBytes</c> pointer is read-only inside JVMTI and may be freed by the
/// caller as soon as <c>RedefineClasses</c> returns.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JvmtiClassDefinition
{
    public IntPtr Klass;            // jclass
    public int ClassByteCount;      // jint
    public IntPtr ClassBytes;        // const unsigned char*
}

/// <summary>JVMTI phases — see <c>jvmtiPhase</c>.</summary>
internal enum JvmtiPhase
{
    Onload = 1,
    Primordial = 2,
    Start = 6,
    Live = 4,
    Dead = 8,
}

/// <summary>Event notification mode flags for <c>SetEventNotificationMode</c>.</summary>
internal static class JvmtiEventMode
{
    public const int JVMTI_ENABLE = 1;
    public const int JVMTI_DISABLE = 0;
}
