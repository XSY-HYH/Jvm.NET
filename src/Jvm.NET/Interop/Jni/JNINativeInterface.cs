using System.Runtime.InteropServices;

namespace Jvm.NET.Interop.Jni;

//
// 完整的 JNINativeInterface_ 结构体布局，逐字段对应 OpenJDK 21 的
// src/java.base/share/native/include/jni.h。
//
// 约定：所有 opaque 句柄（jobject / jclass / jstring / jthrowable / jarray /
// jweak / jmethodID / jfieldID）一律用 IntPtr 表示，保持 blittable。
// 所有 jchar -> char, jbyte -> byte, jshort -> short, jint -> int, jlong -> long,
// jfloat -> float, jdouble -> double, jboolean -> byte, jsize -> int。
//
// 调用约定使用 Winapi（Windows x86 = stdcall，Windows x64 / Linux / macOS = cdecl），
// 与 JNI JNICALL 宏保持一致。
//
// 使用 `delegate* unmanaged[Cdecl]<...>` 直接保存函数指针，避免委托分配开销。
// 这要求 unsafe 上下文（csproj 已开启 AllowUnsafeBlocks）。
//
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct JNINativeInterface_
{
    public IntPtr Reserved0;
    public IntPtr Reserved1;
    public IntPtr Reserved2;
    public IntPtr Reserved3;

    // 4 — version
    public delegate* unmanaged[Cdecl]<IntPtr, int> GetVersion;

    // 5-6 — class loading
    public delegate* unmanaged[Cdecl]<IntPtr, byte*, IntPtr, byte*, int, IntPtr> DefineClass;
    public delegate* unmanaged[Cdecl]<IntPtr, byte*, IntPtr> FindClass;

    // 7-9 — reflection
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> FromReflectedMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> FromReflectedField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte, IntPtr> ToReflectedMethod;

    // 10-11 — class hierarchy
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> GetSuperclass;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> IsAssignableFrom;

    // 12 — reflection (field)
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte, IntPtr> ToReflectedField;

    // 13-18 — exceptions
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> Throw;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, int> ThrowNew;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr> ExceptionOccurred;
    public delegate* unmanaged[Cdecl]<IntPtr, void> ExceptionDescribe;
    public delegate* unmanaged[Cdecl]<IntPtr, void> ExceptionClear;
    public delegate* unmanaged[Cdecl]<IntPtr, byte*, void> FatalError;

    // 19-20 — local frames
    public delegate* unmanaged[Cdecl]<IntPtr, int, int> PushLocalFrame;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> PopLocalFrame;

    // 21-26 — references
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> NewGlobalRef;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> DeleteGlobalRef;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> DeleteLocalRef;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> IsSameObject;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> NewLocalRef;
    public delegate* unmanaged[Cdecl]<IntPtr, int, int> EnsureLocalCapacity;

    // 27-30 — allocation
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> AllocObject;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> NewObject;          // variadic — only used via V/A variants
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> NewObjectV;          // va_list — opaque
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> NewObjectA;  // const jvalue*

    // 31-32 — object introspection
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> GetObjectClass;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> IsInstanceOf;

    // 33 — method id
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, byte*, IntPtr> GetMethodID;

    // 34-36 — CallObjectMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> CallObjectMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> CallObjectMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> CallObjectMethodA;

    // 37-39 — CallBooleanMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> CallBooleanMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallBooleanMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallBooleanMethodA;

    // 40-42 — CallByteMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> CallByteMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallByteMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallByteMethodA;

    // 43-45 — CallCharMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, char> CallCharMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, char> CallCharMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, char> CallCharMethodA;

    // 46-48 — CallShortMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, short> CallShortMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, short> CallShortMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, short> CallShortMethodA;

    // 49-51 — CallIntMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> CallIntMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int> CallIntMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int> CallIntMethodA;

    // 52-54 — CallLongMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, long> CallLongMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, long> CallLongMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, long> CallLongMethodA;

    // 55-57 — CallFloatMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, float> CallFloatMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, float> CallFloatMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, float> CallFloatMethodA;

    // 58-60 — CallDoubleMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, double> CallDoubleMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, double> CallDoubleMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, double> CallDoubleMethodA;

    // 61-63 — CallVoidMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> CallVoidMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void> CallVoidMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void> CallVoidMethodA;

    // 64-93 — CallNonvirtual*Method* (30 entries — Boolean/Byte/Char/Short/Int/Long/Float/Double/Object/Void × V/A/non-V)
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> CallNonvirtualObjectMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> CallNonvirtualObjectMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> CallNonvirtualObjectMethodA;

    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallNonvirtualBooleanMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, byte> CallNonvirtualBooleanMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, byte> CallNonvirtualBooleanMethodA;

    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallNonvirtualByteMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, byte> CallNonvirtualByteMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, byte> CallNonvirtualByteMethodA;

    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, char> CallNonvirtualCharMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, char> CallNonvirtualCharMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, char> CallNonvirtualCharMethodA;

    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, short> CallNonvirtualShortMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, short> CallNonvirtualShortMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, short> CallNonvirtualShortMethodA;

    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int> CallNonvirtualIntMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int> CallNonvirtualIntMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, int> CallNonvirtualIntMethodA;

    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, long> CallNonvirtualLongMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, long> CallNonvirtualLongMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, long> CallNonvirtualLongMethodA;

    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, float> CallNonvirtualFloatMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, float> CallNonvirtualFloatMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, float> CallNonvirtualFloatMethodA;

    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, double> CallNonvirtualDoubleMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, double> CallNonvirtualDoubleMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, double> CallNonvirtualDoubleMethodA;

    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void> CallNonvirtualVoidMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void> CallNonvirtualVoidMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, void> CallNonvirtualVoidMethodA;

    // 94 — field id
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, byte*, IntPtr> GetFieldID;

    // 95-103 — GetXxxField
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> GetObjectField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> GetBooleanField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> GetByteField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, char> GetCharField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, short> GetShortField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> GetIntField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, long> GetLongField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, float> GetFloatField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, double> GetDoubleField;

    // 104-112 — SetXxxField
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void> SetObjectField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte, void> SetBooleanField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte, void> SetByteField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, char, void> SetCharField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, short, void> SetShortField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int, void> SetIntField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, long, void> SetLongField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, float, void> SetFloatField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, double, void> SetDoubleField;

    // 113 — static method id
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, byte*, IntPtr> GetStaticMethodID;

    // 114-116 — CallStaticObjectMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> CallStaticObjectMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> CallStaticObjectMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr> CallStaticObjectMethodA;

    // 117-119 — CallStaticBooleanMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> CallStaticBooleanMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallStaticBooleanMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallStaticBooleanMethodA;

    // 120-122 — CallStaticByteMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> CallStaticByteMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallStaticByteMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, byte> CallStaticByteMethodA;

    // 123-125 — CallStaticCharMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, char> CallStaticCharMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, char> CallStaticCharMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, char> CallStaticCharMethodA;

    // 126-128 — CallStaticShortMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, short> CallStaticShortMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, short> CallStaticShortMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, short> CallStaticShortMethodA;

    // 129-131 — CallStaticIntMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> CallStaticIntMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int> CallStaticIntMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, int> CallStaticIntMethodA;

    // 132-134 — CallStaticLongMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, long> CallStaticLongMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, long> CallStaticLongMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, long> CallStaticLongMethodA;

    // 135-137 — CallStaticFloatMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, float> CallStaticFloatMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, float> CallStaticFloatMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, float> CallStaticFloatMethodA;

    // 138-140 — CallStaticDoubleMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, double> CallStaticDoubleMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, double> CallStaticDoubleMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, double> CallStaticDoubleMethodA;

    // 141-143 — CallStaticVoidMethod*
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> CallStaticVoidMethod;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void> CallStaticVoidMethodV;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void> CallStaticVoidMethodA;

    // 144 — static field id
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, byte*, IntPtr> GetStaticFieldID;

    // 145-153 — GetStaticXxxField
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr> GetStaticObjectField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> GetStaticBooleanField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte> GetStaticByteField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, char> GetStaticCharField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, short> GetStaticShortField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> GetStaticIntField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, long> GetStaticLongField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, float> GetStaticFloatField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, double> GetStaticDoubleField;

    // 154-162 — SetStaticXxxField
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, IntPtr, void> SetStaticObjectField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte, void> SetStaticBooleanField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, byte, void> SetStaticByteField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, char, void> SetStaticCharField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, short, void> SetStaticShortField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int, void> SetStaticIntField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, long, void> SetStaticLongField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, float, void> SetStaticFloatField;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, double, void> SetStaticDoubleField;

    // 163-166 — strings
    public delegate* unmanaged[Cdecl]<IntPtr, char*, int, IntPtr> NewString;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> GetStringLength;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, char*> GetStringChars;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, char*, void> ReleaseStringChars;

    // 167-170 — UTF strings
    public delegate* unmanaged[Cdecl]<IntPtr, byte*, IntPtr> NewStringUTF;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> GetStringUTFLength;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, byte*> GetStringUTFChars;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, void> ReleaseStringUTFChars;

    // 171 — array length
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> GetArrayLength;

    // 172-174 — object array
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr, IntPtr, IntPtr> NewObjectArray;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, IntPtr> GetObjectArrayElement;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, IntPtr, void> SetObjectArrayElement;

    // 175-182 — primitive array allocators
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr> NewBooleanArray;
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr> NewByteArray;
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr> NewCharArray;
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr> NewShortArray;
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr> NewIntArray;
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr> NewLongArray;
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr> NewFloatArray;
    public delegate* unmanaged[Cdecl]<IntPtr, int, IntPtr> NewDoubleArray;

    // 183-190 — primitive array getters
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, byte*> GetBooleanArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, byte*> GetByteArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, char*> GetCharArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, short*> GetShortArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, int*> GetIntArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, long*> GetLongArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, float*> GetFloatArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, double*> GetDoubleArrayElements;

    // 191-198 — primitive array releasers
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, int, void> ReleaseBooleanArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, int, void> ReleaseByteArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, char*, int, void> ReleaseCharArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, short*, int, void> ReleaseShortArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int*, int, void> ReleaseIntArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, long*, int, void> ReleaseLongArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, float*, int, void> ReleaseFloatArrayElements;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, double*, int, void> ReleaseDoubleArrayElements;

    // 199-206 — GetXxxArrayRegion
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, byte*, void> GetBooleanArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, byte*, void> GetByteArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, char*, void> GetCharArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, short*, void> GetShortArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, int*, void> GetIntArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, long*, void> GetLongArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, float*, void> GetFloatArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, double*, void> GetDoubleArrayRegion;

    // 207-214 — SetXxxArrayRegion
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, byte*, void> SetBooleanArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, byte*, void> SetByteArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, char*, void> SetCharArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, short*, void> SetShortArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, int*, void> SetIntArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, long*, void> SetLongArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, float*, void> SetFloatArrayRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, double*, void> SetDoubleArrayRegion;

    // 215-216 — RegisterNatives / UnregisterNatives
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int, int> RegisterNatives;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> UnregisterNatives;

    // 217-219 — monitors + GetJavaVM
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> MonitorEnter;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> MonitorExit;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr*, int> GetJavaVM;

    // 220-221 — string regions
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, char*, void> GetStringRegion;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, byte*, void> GetStringUTFRegion;

    // 222-223 — primitive critical
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, IntPtr> GetPrimitiveArrayCritical;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int, void> ReleasePrimitiveArrayCritical;

    // 224-225 — string critical
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, byte*, char*> GetStringCritical;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, char*, void> ReleaseStringCritical;

    // 226-227 — weak globals
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> NewWeakGlobalRef;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void> DeleteWeakGlobalRef;

    // 228 — exception check
    public delegate* unmanaged[Cdecl]<IntPtr, byte> ExceptionCheck;

    // 229-231 — direct buffers
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, long, IntPtr> NewDirectByteBuffer;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr> GetDirectBufferAddress;
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, long> GetDirectBufferCapacity;

    // 232 — object ref type
    public delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int> GetObjectRefType;

    /// <summary>
    /// Dereferences a <c>JNIEnv*</c> (which itself is a pointer to this table) and
    /// returns a managed ref. The pointer must remain valid for the lifetime of the ref.
    /// </summary>
    public static ref JNINativeInterface_ FromJNIEnv(IntPtr jniEnv)
    {
        var tablePtr = Marshal.ReadIntPtr(jniEnv);
        return ref System.Runtime.CompilerServices.Unsafe.AsRef<JNINativeInterface_>(tablePtr.ToPointer());
    }
}
