using System.Runtime.InteropServices;
using Jvm.NET.Interop.Jni;

namespace Jvm.NET.Abstractions.Jdk21;

//
// JNIEnv 安全封装：把 JNIEnv* 解引用成函数表后提供高级方法，
// 把异常检查 / 字符串转换 / 句柄管理 等样板代码集中在一处。
//
// 所有方法都假设调用线程持有 JNIEnv（即由 Jvm.NET 启动的调用栈），
// 不需要 AttachCurrentThread。
//
internal sealed unsafe class JniEnvHandle
{
    private readonly IntPtr _jniEnv;

    public JniEnvHandle(IntPtr jniEnv)
    {
        if (jniEnv == IntPtr.Zero)
            throw new ArgumentNullException(nameof(jniEnv));
        _jniEnv = jniEnv;
    }

    public IntPtr RawPointer => _jniEnv;

    private ref JNINativeInterface_ Fn
    {
        get
        {
            // JNIEnv* -> 第一层解引用拿到 JNINativeInterface_*
            var tablePtr = Marshal.ReadIntPtr(_jniEnv);
            return ref System.Runtime.CompilerServices.Unsafe.AsRef<JNINativeInterface_>(tablePtr.ToPointer());
        }
    }

    // ---- version / class loading ----

    public int GetVersion() => Fn.GetVersion(_jniEnv);

    public IntPtr FindClass(string name)
    {
        // JNI 期望 '/' 分隔的内部类名（如 java/lang/String）
        var internalName = name.Replace('.', '/');
        byte* pName = (byte*)Marshal.StringToHGlobalAnsi(internalName);
        try
        {
            var clazz = Fn.FindClass(_jniEnv, pName);
            ThrowIfPending();
            return clazz;
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)pName);
        }
    }

    // ---- method / field ids ----

    public IntPtr GetMethodID(IntPtr clazz, string name, string signature)
    {
        byte* pName = (byte*)Marshal.StringToHGlobalAnsi(name);
        byte* pSig = (byte*)Marshal.StringToHGlobalAnsi(signature);
        try
        {
            var id = Fn.GetMethodID(_jniEnv, clazz, pName, pSig);
            ThrowIfPending();
            return id;
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)pName);
            Marshal.FreeHGlobal((IntPtr)pSig);
        }
    }

    public IntPtr GetStaticMethodID(IntPtr clazz, string name, string signature)
    {
        byte* pName = (byte*)Marshal.StringToHGlobalAnsi(name);
        byte* pSig = (byte*)Marshal.StringToHGlobalAnsi(signature);
        try
        {
            var id = Fn.GetStaticMethodID(_jniEnv, clazz, pName, pSig);
            ThrowIfPending();
            return id;
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)pName);
            Marshal.FreeHGlobal((IntPtr)pSig);
        }
    }

    // ---- object allocation ----

    public IntPtr NewObjectA(IntPtr clazz, IntPtr methodId, IntPtr argsPtr)
    {
        var obj = Fn.NewObjectA(_jniEnv, clazz, methodId, argsPtr);
        ThrowIfPending();
        return obj;
    }

    // ---- static / virtual calls (via *A variants, taking a jvalue* array) ----

    public IntPtr CallStaticObjectMethodA(IntPtr clazz, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallStaticObjectMethodA(_jniEnv, clazz, methodId, argsPtr);
        ThrowIfPending();
        return r;
    }

    public int CallStaticIntMethodA(IntPtr clazz, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallStaticIntMethodA(_jniEnv, clazz, methodId, argsPtr);
        ThrowIfPending();
        return r;
    }

    public long CallStaticLongMethodA(IntPtr clazz, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallStaticLongMethodA(_jniEnv, clazz, methodId, argsPtr);
        ThrowIfPending();
        return r;
    }

    public double CallStaticDoubleMethodA(IntPtr clazz, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallStaticDoubleMethodA(_jniEnv, clazz, methodId, argsPtr);
        ThrowIfPending();
        return r;
    }

    public void CallStaticVoidMethodA(IntPtr clazz, IntPtr methodId, IntPtr argsPtr)
    {
        Fn.CallStaticVoidMethodA(_jniEnv, clazz, methodId, argsPtr);
        ThrowIfPending();
    }

    public IntPtr CallObjectMethodA(IntPtr instance, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallObjectMethodA(_jniEnv, instance, methodId, argsPtr);
        ThrowIfPending();
        return r;
    }

    public int CallIntMethodA(IntPtr instance, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallIntMethodA(_jniEnv, instance, methodId, argsPtr);
        ThrowIfPending();
        return r;
    }

    public long CallLongMethodA(IntPtr instance, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallLongMethodA(_jniEnv, instance, methodId, argsPtr);
        ThrowIfPending();
        return r;
    }

    public double CallDoubleMethodA(IntPtr instance, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallDoubleMethodA(_jniEnv, instance, methodId, argsPtr);
        ThrowIfPending();
        return r;
    }

    public void CallVoidMethodA(IntPtr instance, IntPtr methodId, IntPtr argsPtr)
    {
        Fn.CallVoidMethodA(_jniEnv, instance, methodId, argsPtr);
        ThrowIfPending();
    }

    public byte CallBooleanMethodA(IntPtr instance, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallBooleanMethodA(_jniEnv, instance, methodId, argsPtr);
        ThrowIfPending();
        return r;
    }

    // ---- strings ----

    public IntPtr NewStringUTF(string str)
    {
        byte* pUtf = (byte*)Marshal.StringToHGlobalAnsi(str);
        try
        {
            var r = Fn.NewStringUTF(_jniEnv, pUtf);
            ThrowIfPending();
            return r;
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)pUtf);
        }
    }

    public string GetStringUTFChars(IntPtr jstring)
    {
        byte* pChars = Fn.GetStringUTFChars(_jniEnv, jstring, null);
        try
        {
            return pChars == null ? string.Empty : Marshal.PtrToStringUTF8((IntPtr)pChars)!;
        }
        finally
        {
            if (pChars != null)
                Fn.ReleaseStringUTFChars(_jniEnv, jstring, pChars);
        }
    }

    // ---- object arrays (for main(String[])) ----

    public IntPtr NewObjectArray(int length, IntPtr elementClass, IntPtr initialElement)
    {
        var r = Fn.NewObjectArray(_jniEnv, length, elementClass, initialElement);
        ThrowIfPending();
        return r;
    }

    public void SetObjectArrayElement(IntPtr array, int index, IntPtr value)
    {
        Fn.SetObjectArrayElement(_jniEnv, array, index, value);
        ThrowIfPending();
    }

    // ---- references ----

    public IntPtr NewGlobalRef(IntPtr localRef) => Fn.NewGlobalRef(_jniEnv, localRef);
    public void DeleteGlobalRef(IntPtr globalRef) => Fn.DeleteGlobalRef(_jniEnv, globalRef);
    public void DeleteLocalRef(IntPtr localRef) => Fn.DeleteLocalRef(_jniEnv, localRef);
    public IntPtr NewLocalRef(IntPtr ref_) => Fn.NewLocalRef(_jniEnv, ref_);
    public IntPtr GetObjectClass(IntPtr obj)
    {
        var r = Fn.GetObjectClass(_jniEnv, obj);
        ThrowIfPending();
        return r;
    }

    // ---- field ids ----

    public IntPtr GetFieldID(IntPtr clazz, string name, string signature)
    {
        byte* pName = (byte*)Marshal.StringToHGlobalAnsi(name);
        byte* pSig = (byte*)Marshal.StringToHGlobalAnsi(signature);
        try
        {
            var id = Fn.GetFieldID(_jniEnv, clazz, pName, pSig);
            ThrowIfPending();
            return id;
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)pName);
            Marshal.FreeHGlobal((IntPtr)pSig);
        }
    }

    public IntPtr GetStaticFieldID(IntPtr clazz, string name, string signature)
    {
        byte* pName = (byte*)Marshal.StringToHGlobalAnsi(name);
        byte* pSig = (byte*)Marshal.StringToHGlobalAnsi(signature);
        try
        {
            var id = Fn.GetStaticFieldID(_jniEnv, clazz, pName, pSig);
            ThrowIfPending();
            return id;
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)pName);
            Marshal.FreeHGlobal((IntPtr)pSig);
        }
    }

    // ---- instance field access ----

    public IntPtr GetObjectField(IntPtr obj, IntPtr fieldId) => Fn.GetObjectField(_jniEnv, obj, fieldId);
    public bool GetBooleanField(IntPtr obj, IntPtr fieldId) => Fn.GetBooleanField(_jniEnv, obj, fieldId) != JniTypes.JNI_FALSE;
    public byte GetByteField(IntPtr obj, IntPtr fieldId) => Fn.GetByteField(_jniEnv, obj, fieldId);
    public char GetCharField(IntPtr obj, IntPtr fieldId) => Fn.GetCharField(_jniEnv, obj, fieldId);
    public short GetShortField(IntPtr obj, IntPtr fieldId) => Fn.GetShortField(_jniEnv, obj, fieldId);
    public int GetIntField(IntPtr obj, IntPtr fieldId) => Fn.GetIntField(_jniEnv, obj, fieldId);
    public long GetLongField(IntPtr obj, IntPtr fieldId) => Fn.GetLongField(_jniEnv, obj, fieldId);
    public float GetFloatField(IntPtr obj, IntPtr fieldId) => Fn.GetFloatField(_jniEnv, obj, fieldId);
    public double GetDoubleField(IntPtr obj, IntPtr fieldId) => Fn.GetDoubleField(_jniEnv, obj, fieldId);

    public void SetObjectField(IntPtr obj, IntPtr fieldId, IntPtr value) => Fn.SetObjectField(_jniEnv, obj, fieldId, value);
    public void SetBooleanField(IntPtr obj, IntPtr fieldId, bool value) => Fn.SetBooleanField(_jniEnv, obj, fieldId, value ? JniTypes.JNI_TRUE : JniTypes.JNI_FALSE);
    public void SetByteField(IntPtr obj, IntPtr fieldId, byte value) => Fn.SetByteField(_jniEnv, obj, fieldId, value);
    public void SetCharField(IntPtr obj, IntPtr fieldId, char value) => Fn.SetCharField(_jniEnv, obj, fieldId, value);
    public void SetShortField(IntPtr obj, IntPtr fieldId, short value) => Fn.SetShortField(_jniEnv, obj, fieldId, value);
    public void SetIntField(IntPtr obj, IntPtr fieldId, int value) => Fn.SetIntField(_jniEnv, obj, fieldId, value);
    public void SetLongField(IntPtr obj, IntPtr fieldId, long value) => Fn.SetLongField(_jniEnv, obj, fieldId, value);
    public void SetFloatField(IntPtr obj, IntPtr fieldId, float value) => Fn.SetFloatField(_jniEnv, obj, fieldId, value);
    public void SetDoubleField(IntPtr obj, IntPtr fieldId, double value) => Fn.SetDoubleField(_jniEnv, obj, fieldId, value);

    // ---- static field access ----

    public IntPtr GetStaticObjectField(IntPtr clazz, IntPtr fieldId) => Fn.GetStaticObjectField(_jniEnv, clazz, fieldId);
    public bool GetStaticBooleanField(IntPtr clazz, IntPtr fieldId) => Fn.GetStaticBooleanField(_jniEnv, clazz, fieldId) != JniTypes.JNI_FALSE;
    public byte GetStaticByteField(IntPtr clazz, IntPtr fieldId) => Fn.GetStaticByteField(_jniEnv, clazz, fieldId);
    public char GetStaticCharField(IntPtr clazz, IntPtr fieldId) => Fn.GetStaticCharField(_jniEnv, clazz, fieldId);
    public short GetStaticShortField(IntPtr clazz, IntPtr fieldId) => Fn.GetStaticShortField(_jniEnv, clazz, fieldId);
    public int GetStaticIntField(IntPtr clazz, IntPtr fieldId) => Fn.GetStaticIntField(_jniEnv, clazz, fieldId);
    public long GetStaticLongField(IntPtr clazz, IntPtr fieldId) => Fn.GetStaticLongField(_jniEnv, clazz, fieldId);
    public float GetStaticFloatField(IntPtr clazz, IntPtr fieldId) => Fn.GetStaticFloatField(_jniEnv, clazz, fieldId);
    public double GetStaticDoubleField(IntPtr clazz, IntPtr fieldId) => Fn.GetStaticDoubleField(_jniEnv, clazz, fieldId);

    public void SetStaticObjectField(IntPtr clazz, IntPtr fieldId, IntPtr value) => Fn.SetStaticObjectField(_jniEnv, clazz, fieldId, value);
    public void SetStaticBooleanField(IntPtr clazz, IntPtr fieldId, bool value) => Fn.SetStaticBooleanField(_jniEnv, clazz, fieldId, value ? JniTypes.JNI_TRUE : JniTypes.JNI_FALSE);
    public void SetStaticByteField(IntPtr clazz, IntPtr fieldId, byte value) => Fn.SetStaticByteField(_jniEnv, clazz, fieldId, value);
    public void SetStaticCharField(IntPtr clazz, IntPtr fieldId, char value) => Fn.SetStaticCharField(_jniEnv, clazz, fieldId, value);
    public void SetStaticShortField(IntPtr clazz, IntPtr fieldId, short value) => Fn.SetStaticShortField(_jniEnv, clazz, fieldId, value);
    public void SetStaticIntField(IntPtr clazz, IntPtr fieldId, int value) => Fn.SetStaticIntField(_jniEnv, clazz, fieldId, value);
    public void SetStaticLongField(IntPtr clazz, IntPtr fieldId, long value) => Fn.SetStaticLongField(_jniEnv, clazz, fieldId, value);
    public void SetStaticFloatField(IntPtr clazz, IntPtr fieldId, float value) => Fn.SetStaticFloatField(_jniEnv, clazz, fieldId, value);
    public void SetStaticDoubleField(IntPtr clazz, IntPtr fieldId, double value) => Fn.SetStaticDoubleField(_jniEnv, clazz, fieldId, value);

    // ---- static boolean call (补齐缺失) ----

    public bool CallStaticBooleanMethodA(IntPtr clazz, IntPtr methodId, IntPtr argsPtr)
    {
        var r = Fn.CallStaticBooleanMethodA(_jniEnv, clazz, methodId, argsPtr);
        ThrowIfPending();
        return r != JniTypes.JNI_FALSE;
    }

    // ---- type hierarchy ----

    public IntPtr GetSuperclass(IntPtr clazz) => Fn.GetSuperclass(_jniEnv, clazz);

    public bool IsAssignableFrom(IntPtr clazz1, IntPtr clazz2)
        => Fn.IsAssignableFrom(_jniEnv, clazz1, clazz2) != JniTypes.JNI_FALSE;

    public bool IsInstanceOf(IntPtr obj, IntPtr clazz)
        => Fn.IsInstanceOf(_jniEnv, obj, clazz) != JniTypes.JNI_FALSE;

    public bool IsSameObject(IntPtr obj1, IntPtr obj2)
        => Fn.IsSameObject(_jniEnv, obj1, obj2) != JniTypes.JNI_FALSE;

    // ---- object allocation without ctor ----

    public IntPtr AllocObject(IntPtr clazz)
    {
        var obj = Fn.AllocObject(_jniEnv, clazz);
        ThrowIfPending();
        return obj;
    }

    // ---- arrays: length / object array elements ----

    public int GetArrayLength(IntPtr array)
    {
        var r = Fn.GetArrayLength(_jniEnv, array);
        ThrowIfPending();
        return r;
    }

    public IntPtr GetObjectArrayElement(IntPtr array, int index)
    {
        var r = Fn.GetObjectArrayElement(_jniEnv, array, index);
        ThrowIfPending();
        return r;
    }

    // ---- primitive arrays: alloc + region I/O ----
    // 使用 Region API 而不是 Get*ArrayElements，因为 Region API 不需要释放且更安全。

    public IntPtr NewBooleanArray(int length) => Fn.NewBooleanArray(_jniEnv, length);
    public IntPtr NewByteArray(int length) => Fn.NewByteArray(_jniEnv, length);
    public IntPtr NewCharArray(int length) => Fn.NewCharArray(_jniEnv, length);
    public IntPtr NewShortArray(int length) => Fn.NewShortArray(_jniEnv, length);
    public IntPtr NewIntArray(int length) => Fn.NewIntArray(_jniEnv, length);
    public IntPtr NewLongArray(int length) => Fn.NewLongArray(_jniEnv, length);
    public IntPtr NewFloatArray(int length) => Fn.NewFloatArray(_jniEnv, length);
    public IntPtr NewDoubleArray(int length) => Fn.NewDoubleArray(_jniEnv, length);

    public void GetBooleanArrayRegion(IntPtr array, int start, int len, byte* buf)
    {
        Fn.GetBooleanArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void GetByteArrayRegion(IntPtr array, int start, int len, byte* buf)
    {
        Fn.GetByteArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void GetCharArrayRegion(IntPtr array, int start, int len, char* buf)
    {
        Fn.GetCharArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void GetShortArrayRegion(IntPtr array, int start, int len, short* buf)
    {
        Fn.GetShortArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void GetIntArrayRegion(IntPtr array, int start, int len, int* buf)
    {
        Fn.GetIntArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void GetLongArrayRegion(IntPtr array, int start, int len, long* buf)
    {
        Fn.GetLongArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void GetFloatArrayRegion(IntPtr array, int start, int len, float* buf)
    {
        Fn.GetFloatArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void GetDoubleArrayRegion(IntPtr array, int start, int len, double* buf)
    {
        Fn.GetDoubleArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void SetBooleanArrayRegion(IntPtr array, int start, int len, byte* buf)
    {
        Fn.SetBooleanArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void SetByteArrayRegion(IntPtr array, int start, int len, byte* buf)
    {
        Fn.SetByteArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void SetCharArrayRegion(IntPtr array, int start, int len, char* buf)
    {
        Fn.SetCharArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void SetShortArrayRegion(IntPtr array, int start, int len, short* buf)
    {
        Fn.SetShortArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void SetIntArrayRegion(IntPtr array, int start, int len, int* buf)
    {
        Fn.SetIntArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void SetLongArrayRegion(IntPtr array, int start, int len, long* buf)
    {
        Fn.SetLongArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void SetFloatArrayRegion(IntPtr array, int start, int len, float* buf)
    {
        Fn.SetFloatArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    public void SetDoubleArrayRegion(IntPtr array, int start, int len, double* buf)
    {
        Fn.SetDoubleArrayRegion(_jniEnv, array, start, len, buf);
        ThrowIfPending();
    }

    // ---- exception object access ----

    /// <summary>
    /// 返回当前 pending 异常的 jthrowable 局部引用（不清除异常）。
    /// 调用方负责 DeleteLocalRef 或提升为全局引用。
    /// </summary>
    public IntPtr ExceptionOccurred() => Fn.ExceptionOccurred(_jniEnv);

    // ---- monitors ----

    public int MonitorEnter(IntPtr obj) => Fn.MonitorEnter(_jniEnv, obj);
    public int MonitorExit(IntPtr obj) => Fn.MonitorExit(_jniEnv, obj);

    // ---- native method registration ----

    public IntPtr DefineClass(string name, IntPtr loader, byte* bytes, int length)
    {
        byte* pName = (byte*)Marshal.StringToHGlobalAnsi(name);
        try
        {
            var clazz = Fn.DefineClass(_jniEnv, pName, loader, bytes, length);
            ThrowIfPending();
            return clazz;
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)pName);
        }
    }

    public void RegisterNatives(IntPtr clazz, JNINativeMethod[] methods)
    {
        var size = Marshal.SizeOf<JNINativeMethod>();
        var ptr = Marshal.AllocHGlobal(methods.Length * size);
        try
        {
            for (int i = 0; i < methods.Length; i++)
                Marshal.StructureToPtr(methods[i], ptr + (i * size), fDeleteOld: false);
            int rc = Fn.RegisterNatives(_jniEnv, clazz, ptr, methods.Length);
            ThrowIfPending();
            if (rc != 0)
                throw new InvalidOperationException($"RegisterNatives returned {rc}.");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public void UnregisterNatives(IntPtr clazz)
    {
        Fn.UnregisterNatives(_jniEnv, clazz);
        ThrowIfPending();
    }

    // ---- exception helpers ----

    public bool ExceptionCheck() => Fn.ExceptionCheck(_jniEnv) != JniTypes.JNI_FALSE;

    public void ExceptionDescribe() => Fn.ExceptionDescribe(_jniEnv);

    public void ExceptionClear() => Fn.ExceptionClear(_jniEnv);

    /// <summary>
    /// Throws a <see cref="JvmException"/> if JNI has a pending exception.
    /// The pending exception is cleared after being described to stderr.
    /// </summary>
    public void ThrowIfPending()
    {
        if (!ExceptionCheck())
            return;

        ExceptionDescribe();
        ExceptionClear();
        throw new JvmException("A Java exception was thrown. See stderr for the stack trace.");
    }
}

/// <summary>
/// Raised when a JNI call reports a pending Java exception. The native stack
/// trace has already been written to stderr by <c>ExceptionDescribe</c>.
/// </summary>
public sealed class JvmException : Exception
{
    public JvmException(string message) : base(message) { }
}
