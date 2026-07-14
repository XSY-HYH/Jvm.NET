using System.Runtime.InteropServices;

namespace Jvm.NET.Interop.Jvmti;

//
// jvmtiEventCallbacks — passed to SetEventCallbacks. Each slot is a function
// pointer (or NULL when not subscribed). The struct size is reported to
// SetEventCallbacks so that older JVMTI versions ignore trailing fields.
//
// Field order is dictated by the XSLT generator in OpenJDK 21
// (src/hotspot/share/prims/jvmtiLib.xsl, template "eventStruct"): it iterates
// num from 0 upward; for each num with exactly one <event> definition it emits
// a typed field, otherwise a reserved slot. The first event is VMInit (num=50),
// so the struct effectively starts at num=50.
//
// We use LayoutKind.Explicit with FieldOffset = (num - 50) * 8 so we only name
// the slots we care about; unassigned slots default to NULL (IntPtr.Zero),
// which is exactly what JVMTI expects for "no callback".
//
// All slots are IntPtr so the struct is blittable; callers convert managed
// delegates to function pointers via Marshal.GetFunctionPointerForDelegate
// and must keep the delegates alive for as long as JVMTI may invoke them.
//
[StructLayout(LayoutKind.Explicit, Size = 328)]
internal struct JvmtiEventCallbacks
{
    // ---- VM lifecycle (num 50-51) ----
    [FieldOffset(0)]   public IntPtr VMInit;                    // num=50
    [FieldOffset(8)]   public IntPtr VMDeath;                   // num=51

    // ---- Thread lifecycle (num 52-53) ----
    [FieldOffset(16)]  public IntPtr ThreadStart;               // num=52
    [FieldOffset(24)]  public IntPtr ThreadEnd;                 // num=53

    // ---- Class loading (num 54-57) ----
    [FieldOffset(32)]  public IntPtr ClassFileLoadHook;         // num=54
    [FieldOffset(40)]  public IntPtr ClassLoad;                 // num=55
    [FieldOffset(48)]  public IntPtr ClassPrepare;              // num=56
    // num=57: VMStart / ClassUnload (ambiguous in jvmti.xml) — left NULL.

    // ---- Execution (num 58-67) ----
    [FieldOffset(64)]  public IntPtr Exception;                 // num=58
    [FieldOffset(72)]  public IntPtr ExceptionCatch;            // num=59
    [FieldOffset(80)]  public IntPtr SingleStep;                // num=60
    [FieldOffset(88)]  public IntPtr FramePop;                  // num=61
    [FieldOffset(96)]  public IntPtr Breakpoint;                // num=62
    [FieldOffset(104)] public IntPtr FieldAccess;               // num=63
    [FieldOffset(112)] public IntPtr FieldModification;         // num=64
    [FieldOffset(120)] public IntPtr MethodEntry;               // num=65
    [FieldOffset(128)] public IntPtr MethodExit;                // num=66
    [FieldOffset(136)] public IntPtr NativeMethodBind;          // num=67

    // ---- Compiled code (num 68-71) ----
    [FieldOffset(144)] public IntPtr CompiledMethodLoad;        // num=68
    [FieldOffset(152)] public IntPtr CompiledMethodUnload;      // num=69
    [FieldOffset(160)] public IntPtr DynamicCodeGenerated;      // num=70
    [FieldOffset(168)] public IntPtr DataDumpRequest;           // num=71

    // ---- Monitor (num 73-76) ----
    [FieldOffset(176)] public IntPtr MonitorWait;               // num=73
    [FieldOffset(184)] public IntPtr MonitorWaited;             // num=74
    [FieldOffset(192)] public IntPtr MonitorContendedEnter;     // num=75
    [FieldOffset(200)] public IntPtr MonitorContendedEntered;   // num=76

    // ---- GC / alloc (num 80-86) ----
    [FieldOffset(208)] public IntPtr ResourceExhausted;         // num=80
    [FieldOffset(216)] public IntPtr GarbageCollectionStart;    // num=81
    [FieldOffset(224)] public IntPtr GarbageCollectionFinish;   // num=82
    [FieldOffset(232)] public IntPtr ObjectFree;                // num=83
    [FieldOffset(240)] public IntPtr VMObjectAlloc;             // num=84
    [FieldOffset(248)] public IntPtr VerboseOutput;             // num=85
    [FieldOffset(256)] public IntPtr SampledObjectAlloc;        // num=86

    // ---- Virtual threads (num 87-90, JDK 21) ----
    [FieldOffset(264)] public IntPtr VirtualThreadStart;        // num=87
    [FieldOffset(272)] public IntPtr VirtualThreadEnd;          // num=88
    [FieldOffset(280)] public IntPtr VirtualThreadMount;        // num=89
    [FieldOffset(288)] public IntPtr VirtualThreadUnmount;      // num=90

    /// <summary>Size in bytes that should be reported to <c>SetEventCallbacks</c>.</summary>
    public static readonly int SizeInBytes = Marshal.SizeOf<JvmtiEventCallbacks>();
}
