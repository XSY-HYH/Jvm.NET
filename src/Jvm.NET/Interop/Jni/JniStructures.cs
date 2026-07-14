using System.Runtime.InteropServices;

namespace Jvm.NET.Interop.Jni;

//
// JNI 原生类型别名。在 OpenJDK 21 的 jni.h 中这些都是 typedef，
// 我们直接用 C# 类型等价物以保持 blittable。
//
// jboolean -> unsigned char (0/1)，用 byte 表示
// jchar    -> unsigned short (UTF-16 code unit)
// jshort   -> short
// jint     -> int
// jlong    -> long
// jfloat   -> float
// jdouble  -> double
//
// jobject / jclass / jstring / jthrowable / jarray / jweak 都是 opaque 指针，
// 统一用 IntPtr 表示。jmethodID / jfieldID 同理。
//

internal static class JniTypes
{
    public const byte JNI_TRUE = 1;
    public const byte JNI_FALSE = 0;
}

/// <summary>
/// Single JVM startup option, e.g. <c>-Xmx512m</c> or <c>-Djava.class.path=...</c>.
/// Mirrors <c>struct JavaVMOption</c> in <c>jni.h</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JavaVMOption
{
    public IntPtr OptionString;     // char* — UTF-8, NUL-terminated
    public IntPtr ExtraInfo;        // void* — opaque payload passed to the option handler

    public JavaVMOption(string option, IntPtr extra = default)
    {
        OptionString = Marshal.StringToHGlobalAnsi(option);
        ExtraInfo = extra;
    }

    public void Free()
    {
        if (OptionString != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(OptionString);
            OptionString = IntPtr.Zero;
        }
    }
}

/// <summary>
/// Arguments passed to <c>JNI_CreateJavaVM</c>. Mirrors <c>struct JavaVMInitArgs</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JavaVMInitArgs
{
    public int Version;             // JNI_VERSION_*
    public int nOptions;
    public IntPtr Options;          // JavaVMOption* (managed as an unmanaged array)
    public byte IgnoreUnrecognized; // jboolean
}

/// <summary>
/// Layout of the <c>JNIInvokeInterface</c> table that backs a <c>JavaVM*</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct JNIInvokeInterface
{
    public IntPtr Reserved0;
    public IntPtr Reserved1;
    public IntPtr Reserved2;

    public delegate* unmanaged[Cdecl]<IntPtr, int> DestroyJavaVM;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr*, IntPtr, int> AttachCurrentThread;
    public delegate* unmanaged[Cdecl]<IntPtr, int> DetachCurrentThread;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr*, int, int> GetEnv;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr*, IntPtr, int> AttachCurrentThreadAsDaemon;

    /// <summary>Reads the table from a <c>JavaVM*</c> (which itself points at this table).</summary>
    public static ref JNIInvokeInterface FromJavaVm(IntPtr javaVm)
    {
        var tablePtr = Marshal.ReadIntPtr(javaVm);
        return ref System.Runtime.CompilerServices.Unsafe.AsRef<JNIInvokeInterface>(tablePtr.ToPointer());
    }
}
