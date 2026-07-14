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
