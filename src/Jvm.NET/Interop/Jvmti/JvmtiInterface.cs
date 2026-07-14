using System.Runtime.InteropServices;

namespace Jvm.NET.Interop.Jvmti;

//
// jvmtiEnv* 指向 jvmtiInterface_1_ 函数表。完整的表包含 156 个槽位
// (num 1..156)，字段顺序严格固定。每个 <function> 节点在 jvmti.xml 中的
// num 属性是 1-based 索引：num=1 对应表中第 0 个槽位（reserved），
// num=2 对应第 1 个槽位（SetEventNotificationMode），以此类推。
//
// 因此 FieldOffset = (num - 1) * 8。
//
// 参考 OpenJDK 21 的 src/hotspot/share/prims/jvmtiH.xsl 中的 funcStruct 模板：
// 它从 index=1 开始递归生成字段，每个 index 对应结构体中的一个槽位（offset 从 0 起）。
// 与 jvmtiEventCallbacks 不同，funcStruct 没有 "started" 跳过机制 ——
// num=1 不存在时生成 reserved1 字段占据 offset 0。
//
[StructLayout(LayoutKind.Explicit, Size = 1248)]
internal unsafe struct JvmtiInterface_1_
{
    // ---- Event management ----
    // num=2: SetEventNotificationMode
    [FieldOffset(8)]
    public delegate* unmanaged[Cdecl]<IntPtr, int, int, IntPtr, int> SetEventNotificationMode;

    // ---- Thread info ----
    // num=9: GetThreadInfo
    [FieldOffset(64)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, JvmtiThreadInfo*, int> GetThreadInfo;

    // ---- Raw monitors (num 31-37) ----
    [FieldOffset(240)]
    public delegate* unmanaged[Cdecl]<IntPtr, byte*, IntPtr*, int> CreateRawMonitor;       // num=31
    [FieldOffset(248)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> DestroyRawMonitor;              // num=32
    [FieldOffset(256)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> RawMonitorEnter;                // num=33
    [FieldOffset(264)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> RawMonitorExit;                 // num=34
    [FieldOffset(272)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, long, int> RawMonitorWait;           // num=35
    [FieldOffset(280)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> RawMonitorNotify;               // num=36
    [FieldOffset(288)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> RawMonitorNotifyAll;            // num=37

    // ---- Memory (num 46-47) ----
    [FieldOffset(360)]
    public delegate* unmanaged[Cdecl]<IntPtr, long, IntPtr*, int> Allocate;                // num=46
    [FieldOffset(368)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> Deallocate;                     // num=47

    // ---- Class info ----
    // num=48: GetClassSignature
    [FieldOffset(376)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr*, IntPtr*, int> GetClassSignature;

    // ---- Method info (num 64-65, 70) ----
    [FieldOffset(504)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr*, IntPtr*, IntPtr*, int> GetMethodName;             // num=64
    [FieldOffset(512)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr*, int> GetMethodDeclaringClass;                      // num=65
    [FieldOffset(552)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int*, JvmtiLineNumberEntry**, int> GetLineNumberTable;     // num=70

    // ---- Method inspection (num 75-77) ----
    [FieldOffset(592)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int*, IntPtr*, int> GetBytecodes;     // num=75
    [FieldOffset(600)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, int> IsMethodNative;           // num=76
    [FieldOffset(608)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, int> IsMethodSynthetic;        // num=77

    // ---- Class enumeration (num 78-79) ----
    [FieldOffset(616)]
    public delegate* unmanaged[Cdecl]<IntPtr, int*, IntPtr*, int> GetLoadedClasses;         // num=78
    [FieldOffset(624)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int*, IntPtr*, int> GetClassLoaderClasses; // num=79

    // ---- Class redefinition (num 87, 152) ----
    [FieldOffset(688)]
    public delegate* unmanaged[Cdecl]<IntPtr, int, JvmtiClassDefinition*, int> RedefineClasses;  // num=87
    [FieldOffset(1208)]
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr*, int> RetransformClasses;              // num=152

    // ---- Version / capabilities (num 88-90) ----
    [FieldOffset(696)]
    public delegate* unmanaged[Cdecl]<IntPtr, int*, int> GetVersionNumber;                  // num=88
    [FieldOffset(704)]
    public delegate* unmanaged[Cdecl]<IntPtr, JvmtiCapabilities*, int> GetCapabilities;     // num=89
    [FieldOffset(712)]
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr*, int> GetSourceDebugExtension; // num=90

    // ---- Event callbacks (num 122) ----
    [FieldOffset(968)]
    public delegate* unmanaged[Cdecl]<IntPtr, JvmtiEventCallbacks*, int, int> SetEventCallbacks;

    // ---- Capabilities (num 142-143) ----
    [FieldOffset(1128)]
    public delegate* unmanaged[Cdecl]<IntPtr, JvmtiCapabilities*, int> AddCapabilities;          // num=142
    [FieldOffset(1136)]
    public delegate* unmanaged[Cdecl]<IntPtr, JvmtiCapabilities*, int> RelinquishCapabilities;   // num=143

    /// <summary>
    /// Dereferences a <c>jvmtiEnv*</c> (which itself points at this table) and
    /// returns a managed ref.
    /// </summary>
    public static ref JvmtiInterface_1_ FromJvmtiEnv(IntPtr jvmtiEnv)
    {
        var tablePtr = Marshal.ReadIntPtr(jvmtiEnv);
        return ref System.Runtime.CompilerServices.Unsafe.AsRef<JvmtiInterface_1_>(tablePtr.ToPointer());
    }
}
