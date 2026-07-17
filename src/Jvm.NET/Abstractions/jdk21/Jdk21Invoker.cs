using System.Runtime.InteropServices;

namespace Jvm.NET.Abstractions.Jdk21;

/// <summary>
/// JDK 21 specific <see cref="IJvmInvoker"/>. Backed by a <see cref="JniEnvHandle"/>
/// obtained at JVM startup. Calls into the standard JNI function table.
/// </summary>
internal sealed unsafe class Jdk21Invoker : IJvmInvoker, IDisposable
{
    private readonly JniEnvHandle _env;
    private readonly JvmCallbackRegistry _callbacks;

    // Cached class/method IDs that we use repeatedly.
    private IntPtr _urlClassLoaderClass;
    private IntPtr _urlClassLoaderCtor;
    private IntPtr _urlClass;
    private IntPtr _urlCtor;
    private IntPtr _stringClass;
    private IntPtr _threadClass;
    private IntPtr _threadCurrentThread;
    private IntPtr _threadGetContextClassLoader;
    private IntPtr _threadSetContextClassLoader;
    private IntPtr _classClass;
    private IntPtr _classForName;
    private IntPtr _classGetName;

    // Throwable 的方法 ID（延迟初始化）
    private IntPtr _throwableClass;
    private IntPtr _throwableGetMessage;
    private IntPtr _throwableGetStackTrace;
    private IntPtr _stackTraceElementClass;
    private IntPtr _stackTraceElementToString;

    // Cache of URLClassLoaders created via LoadJar — last one wins for LoadClass/RunMain.
    private IntPtr _activeClassLoader;
    private IntPtr _classLoaderLoadClass;

    // 标记 Invoker 是否已释放。释放回调检查此标志避免 JVM 销毁后调用 DeleteGlobalRef 崩溃。
    private volatile bool _disposed;

    public Jdk21Invoker(IntPtr jniEnv)
    {
        _env = new JniEnvHandle(jniEnv);
        _callbacks = new JvmCallbackRegistry(_env);
    }

    /// <summary>创建拥有全局引用的 JvmClass，Dispose 时自动释放。</summary>
    private JvmClass CreateOwnedClass(IntPtr globalRef, string name)
        => new JvmClass(globalRef, name, h => { if (!_disposed) _env.DeleteGlobalRef(h); });

    /// <summary>创建拥有全局引用的 JvmObject，Dispose 时自动释放。</summary>
    private JvmObject CreateOwnedObject(IntPtr globalRef, JvmClass clazz)
        => new JvmObject(globalRef, clazz, h => { if (!_disposed) _env.DeleteGlobalRef(h); });

    public void LoadJar(string jarPath)
    {
        if (string.IsNullOrWhiteSpace(jarPath))
            throw new ArgumentException("Jar path must be non-empty.", nameof(jarPath));
        if (!File.Exists(jarPath))
            throw new FileNotFoundException($"Jar not found: {jarPath}", jarPath);

        EnsureClassCache();

        // Build "file:/absolute/path" URL via the single-arg constructor new URL(String)
        var urlStr = new Uri(jarPath).AbsoluteUri;
        var urlJStr = _env.NewStringUTF(urlStr);
        try
        {
            var urlArgs = stackalloc long[1];
            urlArgs[0] = urlJStr.ToInt64();
            var url = _env.NewObjectA(_urlClass, _urlCtor, (IntPtr)urlArgs);
            try
            {
                // new URLClassLoader(URL[], ClassLoader)
                var urlsArray = _env.NewObjectArray(1, _urlClass, url);

                // Get current thread's context class loader as parent
                var thread = _env.CallStaticObjectMethodA(_threadClass, _threadCurrentThread, IntPtr.Zero);
                var parentLoader = _env.CallObjectMethodA(thread, _threadGetContextClassLoader, IntPtr.Zero);

                var ctorArgs = stackalloc long[2];
                ctorArgs[0] = urlsArray.ToInt64();
                ctorArgs[1] = parentLoader.ToInt64();
                var newLoader = _env.NewObjectA(_urlClassLoaderClass, _urlClassLoaderCtor, (IntPtr)ctorArgs);

                // Swap in as the active class loader. Replace previous if any.
                if (_activeClassLoader != IntPtr.Zero)
                    _env.DeleteGlobalRef(_activeClassLoader);
                _activeClassLoader = _env.NewGlobalRef(newLoader);

                // Look up loadClass on the new loader.
                _classLoaderLoadClass = _env.GetMethodID(_env.GetObjectClass(_activeClassLoader),
                    "loadClass", "(Ljava/lang/String;)Ljava/lang/Class;");
            }
            finally
            {
                _env.DeleteLocalRef(url);
            }
        }
        finally
        {
            _env.DeleteLocalRef(urlJStr);
        }
    }

    public JvmClass? FindClass(string fullyQualifiedName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedName))
            throw new ArgumentException("Class name must be non-empty.", nameof(fullyQualifiedName));

        var handle = _env.FindClass(fullyQualifiedName);
        if (handle == IntPtr.Zero)
            return null;

        // Promote to global ref so the caller can use it across JNI frames.
        var global = _env.NewGlobalRef(handle);
        _env.DeleteLocalRef(handle);
        return CreateOwnedClass(global, fullyQualifiedName);
    }

    public JvmClass LoadClass(string fullyQualifiedName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedName))
            throw new ArgumentException("Class name must be non-empty.", nameof(fullyQualifiedName));

        if (_activeClassLoader == IntPtr.Zero)
        {
            // Fall back to JNIEnv.FindClass which uses the JVM's startup class loader.
            var cls = FindClass(fullyQualifiedName);
            if (cls is null)
                throw new TypeLoadException($"Class '{fullyQualifiedName}' could not be loaded.");
            return cls;
        }

        EnsureClassCache();
        var nameJStr = _env.NewStringUTF(fullyQualifiedName);
        try
        {
            var args = stackalloc long[1];
            args[0] = nameJStr.ToInt64();
            var cls = _env.CallObjectMethodA(_activeClassLoader, _classLoaderLoadClass, (IntPtr)args);
            var global = _env.NewGlobalRef(cls);
            _env.DeleteLocalRef(cls);
            return CreateOwnedClass(global, fullyQualifiedName);
        }
        finally
        {
            _env.DeleteLocalRef(nameJStr);
        }
    }

    public JvmClass DefineClass(string name, byte[] bytecode)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Class name must be non-empty.", nameof(name));
        if (bytecode is null || bytecode.Length == 0)
            throw new ArgumentException("Bytecode must be non-empty.", nameof(bytecode));

        fixed (byte* pBytes = bytecode)
        {
            var localRef = _env.DefineClass(name, IntPtr.Zero, pBytes, bytecode.Length);
            if (localRef == IntPtr.Zero)
                throw new InvalidOperationException($"JNIEnv->DefineClass returned null for '{name}'.");
            var global = _env.NewGlobalRef(localRef);
            _env.DeleteLocalRef(localRef);
            return CreateOwnedClass(global, name.Replace('/', '.'));
        }
    }

    public JvmObject NewObject(JvmClass clazz, string constructorSignature, params JvmValue[] args)
    {
        var ctorId = _env.GetMethodID(clazz.Handle, "<init>", constructorSignature);
        using var jargs = new JValueArray(args);
        var obj = _env.NewObjectA(clazz.Handle, ctorId, jargs.RawPointer);
        var global = _env.NewGlobalRef(obj);
        _env.DeleteLocalRef(obj);
        return CreateOwnedObject(global, clazz);
    }

    public JvmValue InvokeStatic(JvmClass clazz, string methodName, string signature, params JvmValue[] args)
    {
        var methodId = _env.GetStaticMethodID(clazz.Handle, methodName, signature);
        var returnType = ParseReturnType(signature);

        using var j_args = new JValueArray(args);
        return returnType switch
        {
            'V' => RunVoid(() => _env.CallStaticVoidMethodA(clazz.Handle, methodId, j_args.RawPointer)),
            'L' or '[' => JvmValue.FromObject(_env.CallStaticObjectMethodA(clazz.Handle, methodId, j_args.RawPointer)),
            'I' or 'Z' or 'B' or 'C' or 'S' => JvmValue.FromInt(_env.CallStaticIntMethodA(clazz.Handle, methodId, j_args.RawPointer)),
            'J' => JvmValue.FromLong(_env.CallStaticLongMethodA(clazz.Handle, methodId, j_args.RawPointer)),
            'F' => JvmValue.FromDouble(_env.CallStaticDoubleMethodA(clazz.Handle, methodId, j_args.RawPointer)),
            'D' => JvmValue.FromDouble(_env.CallStaticDoubleMethodA(clazz.Handle, methodId, j_args.RawPointer)),
            _ => throw new ArgumentException($"Unsupported return type '{returnType}' in signature: {signature}", nameof(signature)),
        };
    }

    public JvmValue InvokeVirtual(JvmObject instance, string methodName, string signature, params JvmValue[] args)
    {
        // 当 Class.Handle 为 Zero（占位类）时，用 GetObjectClass 获取对象的实际运行时类。
        var classHandle = instance.Class.Handle;
        if (classHandle == IntPtr.Zero)
            classHandle = _env.GetObjectClass(instance.Handle);
        var methodId = _env.GetMethodID(classHandle, methodName, signature);
        var returnType = ParseReturnType(signature);

        using var j_args = new JValueArray(args);
        return returnType switch
        {
            'V' => RunVoid(() => _env.CallVoidMethodA(instance.Handle, methodId, j_args.RawPointer)),
            'L' or '[' => JvmValue.FromObject(_env.CallObjectMethodA(instance.Handle, methodId, j_args.RawPointer)),
            'I' or 'Z' or 'B' or 'C' or 'S' => JvmValue.FromInt(_env.CallIntMethodA(instance.Handle, methodId, j_args.RawPointer)),
            'J' => JvmValue.FromLong(_env.CallLongMethodA(instance.Handle, methodId, j_args.RawPointer)),
            'F' => JvmValue.FromDouble(_env.CallDoubleMethodA(instance.Handle, methodId, j_args.RawPointer)),
            'D' => JvmValue.FromDouble(_env.CallDoubleMethodA(instance.Handle, methodId, j_args.RawPointer)),
            _ => throw new ArgumentException($"Unsupported return type '{returnType}' in signature: {signature}", nameof(signature)),
        };
    }

    public void RunMain(string jarPath, string mainClassName, params string[] args)
    {
        LoadJar(jarPath);
        var mainClass = LoadClass(mainClassName);

        EnsureClassCache();
        var mainMethodId = _env.GetStaticMethodID(mainClass.Handle, "main", "([Ljava/lang/String;)V");
        if (mainMethodId == IntPtr.Zero)
            throw new MissingMethodException(mainClassName, "main(String[])");

        // Build the String[] argument
        var javaArgs = _env.NewObjectArray(args.Length, _stringClass, IntPtr.Zero);
        for (int i = 0; i < args.Length; i++)
        {
            var s = _env.NewStringUTF(args[i]);
            _env.SetObjectArrayElement(javaArgs, i, s);
            _env.DeleteLocalRef(s);
        }

        var argPtr = stackalloc long[1];
        argPtr[0] = javaArgs.ToInt64();
        _env.CallStaticVoidMethodA(mainClass.Handle, mainMethodId, (IntPtr)argPtr);
        _env.DeleteLocalRef(javaArgs);
    }

    public JvmValue NewString(string str)
    {
        if (str is null) throw new ArgumentNullException(nameof(str));
        // NewStringUTF returns a JNI local reference. The caller consumes it
        // synchronously as an argument to a subsequent Invoke* call within the
        // same JNI frame, so we do NOT promote to a global ref here.
        var localRef = _env.NewStringUTF(str);
        return JvmValue.FromObject(localRef);
    }

    public string GetString(IntPtr javaStringHandle)
    {
        if (javaStringHandle == IntPtr.Zero)
            return string.Empty;
        return _env.GetStringUTFChars(javaStringHandle);
    }

    public JvmValue NewStringArray(string[] args)
    {
        if (args is null) throw new ArgumentNullException(nameof(args));
        EnsureClassCache();
        var javaArgs = _env.NewObjectArray(args.Length, _stringClass, IntPtr.Zero);
        for (int i = 0; i < args.Length; i++)
        {
            var s = _env.NewStringUTF(args[i]);
            _env.SetObjectArrayElement(javaArgs, i, s);
            _env.DeleteLocalRef(s);
        }
        return JvmValue.FromObject(javaArgs);
    }

    public void Dispose()
    {
        _disposed = true;
        _callbacks.Dispose();
        if (_activeClassLoader != IntPtr.Zero)
        {
            _env.DeleteGlobalRef(_activeClassLoader);
            _activeClassLoader = IntPtr.Zero;
        }
    }

    // ---- 字段访问 ----

    public JvmValue GetField(JvmObject instance, string name, string signature)
    {
        var fieldId = _env.GetFieldID(instance.Class.Handle, name, signature);
        return ReadFieldByType(instance.Handle, fieldId, signature, isStatic: false);
    }

    public void SetField(JvmObject instance, string name, string signature, JvmValue value)
    {
        var fieldId = _env.GetFieldID(instance.Class.Handle, name, signature);
        WriteFieldByType(instance.Handle, fieldId, signature, value, isStatic: false, clazz: IntPtr.Zero);
    }

    public JvmValue GetStaticField(JvmClass clazz, string name, string signature)
    {
        var fieldId = _env.GetStaticFieldID(clazz.Handle, name, signature);
        return ReadFieldByType(clazz.Handle, fieldId, signature, isStatic: true);
    }

    public void SetStaticField(JvmClass clazz, string name, string signature, JvmValue value)
    {
        var fieldId = _env.GetStaticFieldID(clazz.Handle, name, signature);
        WriteFieldByType(clazz.Handle, fieldId, signature, value, isStatic: true, clazz: clazz.Handle);
    }

    private JvmValue ReadFieldByType(IntPtr objOrClass, IntPtr fieldId, string signature, bool isStatic)
    {
        // 字段签名就是类型签名本身，第一个字符是类型代码
        var typeCode = signature[0];
        return typeCode switch
        {
            'L' or '[' => JvmValue.FromObject(isStatic
                ? _env.GetStaticObjectField(objOrClass, fieldId)
                : _env.GetObjectField(objOrClass, fieldId)),
            'Z' => JvmValue.FromBoolean(isStatic
                ? _env.GetStaticBooleanField(objOrClass, fieldId)
                : _env.GetBooleanField(objOrClass, fieldId)),
            'B' => JvmValue.FromByte(isStatic
                ? _env.GetStaticByteField(objOrClass, fieldId)
                : _env.GetByteField(objOrClass, fieldId)),
            'C' => JvmValue.FromChar(isStatic
                ? _env.GetStaticCharField(objOrClass, fieldId)
                : _env.GetCharField(objOrClass, fieldId)),
            'S' => JvmValue.FromShort(isStatic
                ? _env.GetStaticShortField(objOrClass, fieldId)
                : _env.GetShortField(objOrClass, fieldId)),
            'I' => JvmValue.FromInt(isStatic
                ? _env.GetStaticIntField(objOrClass, fieldId)
                : _env.GetIntField(objOrClass, fieldId)),
            'J' => JvmValue.FromLong(isStatic
                ? _env.GetStaticLongField(objOrClass, fieldId)
                : _env.GetLongField(objOrClass, fieldId)),
            'F' => JvmValue.FromFloat(isStatic
                ? _env.GetStaticFloatField(objOrClass, fieldId)
                : _env.GetFloatField(objOrClass, fieldId)),
            'D' => JvmValue.FromDouble(isStatic
                ? _env.GetStaticDoubleField(objOrClass, fieldId)
                : _env.GetDoubleField(objOrClass, fieldId)),
            _ => throw new ArgumentException($"Unsupported field type '{typeCode}' in signature: {signature}", nameof(signature)),
        };
    }

    private void WriteFieldByType(IntPtr objOrClass, IntPtr fieldId, string signature, JvmValue value, bool isStatic, IntPtr clazz)
    {
        var typeCode = signature[0];
        switch (typeCode)
        {
            case 'L' or '[':
                if (isStatic) _env.SetStaticObjectField(clazz, fieldId, value.ObjectHandle);
                else _env.SetObjectField(objOrClass, fieldId, value.ObjectHandle);
                break;
            case 'Z':
                if (isStatic) _env.SetStaticBooleanField(clazz, fieldId, value.AsBoolean());
                else _env.SetBooleanField(objOrClass, fieldId, value.AsBoolean());
                break;
            case 'B':
                if (isStatic) _env.SetStaticByteField(clazz, fieldId, value.AsByte());
                else _env.SetByteField(objOrClass, fieldId, value.AsByte());
                break;
            case 'C':
                if (isStatic) _env.SetStaticCharField(clazz, fieldId, value.AsChar());
                else _env.SetCharField(objOrClass, fieldId, value.AsChar());
                break;
            case 'S':
                if (isStatic) _env.SetStaticShortField(clazz, fieldId, value.AsShort());
                else _env.SetShortField(objOrClass, fieldId, value.AsShort());
                break;
            case 'I':
                if (isStatic) _env.SetStaticIntField(clazz, fieldId, value.AsInt());
                else _env.SetIntField(objOrClass, fieldId, value.AsInt());
                break;
            case 'J':
                if (isStatic) _env.SetStaticLongField(clazz, fieldId, value.AsLong());
                else _env.SetLongField(objOrClass, fieldId, value.AsLong());
                break;
            case 'F':
                if (isStatic) _env.SetStaticFloatField(clazz, fieldId, value.AsFloat());
                else _env.SetFloatField(objOrClass, fieldId, value.AsFloat());
                break;
            case 'D':
                if (isStatic) _env.SetStaticDoubleField(clazz, fieldId, value.AsDouble());
                else _env.SetDoubleField(objOrClass, fieldId, value.AsDouble());
                break;
            default:
                throw new ArgumentException($"Unsupported field type '{typeCode}' in signature: {signature}", nameof(signature));
        }
    }

    // ---- 类型层次 ----

    public bool IsInstanceOf(JvmObject instance, JvmClass clazz)
        => _env.IsInstanceOf(instance.Handle, clazz.Handle);

    public bool IsAssignableFrom(JvmClass from, JvmClass to)
        => _env.IsAssignableFrom(from.Handle, to.Handle);

    public JvmClass? GetSuperclass(JvmClass clazz)
    {
        var super = _env.GetSuperclass(clazz.Handle);
        if (super == IntPtr.Zero) return null;
        var global = _env.NewGlobalRef(super);
        _env.DeleteLocalRef(super);
        // 无法获取父类的二进制名（需要调用 Class.getName()），这里用 clazz.Name + ".super" 作为占位
        // 调用方通常只需要句柄，Name 仅用于调试
        return CreateOwnedClass(global, clazz.Name + "$super");
    }

    public JvmClass GetObjectClass(JvmObject instance)
    {
        var clazz = _env.GetObjectClass(instance.Handle);
        var global = _env.NewGlobalRef(clazz);
        _env.DeleteLocalRef(clazz);
        return CreateOwnedClass(global, instance.Class.Name);
    }

    // ---- 数组 ----

    public int GetArrayLength(JvmValue array)
    {
        if (array.Type != JvmValueType.Object)
            throw new ArgumentException("Array must be an object reference.", nameof(array));
        return _env.GetArrayLength(array.ObjectHandle);
    }

    public JvmValue GetObjectArrayElement(JvmValue array, int index)
    {
        if (array.Type != JvmValueType.Object)
            throw new ArgumentException("Array must be an object reference.", nameof(array));
        return JvmValue.FromObject(_env.GetObjectArrayElement(array.ObjectHandle, index));
    }

    public void SetObjectArrayElement(JvmValue array, int index, JvmValue value)
    {
        if (array.Type != JvmValueType.Object)
            throw new ArgumentException("Array must be an object reference.", nameof(array));
        var handle = value.Type == JvmValueType.Object ? value.ObjectHandle : IntPtr.Zero;
        _env.SetObjectArrayElement(array.ObjectHandle, index, handle);
    }

    public JvmValue NewArray<T>(T[] values) where T : unmanaged
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        var t = typeof(T);
        IntPtr array;

        // 固定托管数组，使其内存地址在调用期间稳定
        fixed (T* pValues = values)
        {
            if (t == typeof(int))
            {
                array = _env.NewIntArray(values.Length);
                _env.SetIntArrayRegion(array, 0, values.Length, (int*)pValues);
            }
            else if (t == typeof(long))
            {
                array = _env.NewLongArray(values.Length);
                _env.SetLongArrayRegion(array, 0, values.Length, (long*)pValues);
            }
            else if (t == typeof(double))
            {
                array = _env.NewDoubleArray(values.Length);
                _env.SetDoubleArrayRegion(array, 0, values.Length, (double*)pValues);
            }
            else if (t == typeof(float))
            {
                array = _env.NewFloatArray(values.Length);
                _env.SetFloatArrayRegion(array, 0, values.Length, (float*)pValues);
            }
            else if (t == typeof(byte))
            {
                array = _env.NewByteArray(values.Length);
                _env.SetByteArrayRegion(array, 0, values.Length, (byte*)pValues);
            }
            else if (t == typeof(char))
            {
                array = _env.NewCharArray(values.Length);
                _env.SetCharArrayRegion(array, 0, values.Length, (char*)pValues);
            }
            else if (t == typeof(short))
            {
                array = _env.NewShortArray(values.Length);
                _env.SetShortArrayRegion(array, 0, values.Length, (short*)pValues);
            }
            else if (t == typeof(bool))
            {
                array = _env.NewBooleanArray(values.Length);
                // Java boolean 是 1 字节，与 C# bool 内存布局一致
                _env.SetBooleanArrayRegion(array, 0, values.Length, (byte*)pValues);
            }
            else
            {
                throw new NotSupportedException($"Unsupported primitive array type: {t}");
            }
        }

        return JvmValue.FromObject(array);
    }

    public T[] GetArrayValues<T>(JvmValue array) where T : unmanaged
    {
        if (array.Type != JvmValueType.Object)
            throw new ArgumentException("Array must be an object reference.", nameof(array));
        var t = typeof(T);
        var length = _env.GetArrayLength(array.ObjectHandle);
        var result = new T[length];

        fixed (T* pResult = result)
        {
            if (t == typeof(int))
                _env.GetIntArrayRegion(array.ObjectHandle, 0, length, (int*)pResult);
            else if (t == typeof(long))
                _env.GetLongArrayRegion(array.ObjectHandle, 0, length, (long*)pResult);
            else if (t == typeof(double))
                _env.GetDoubleArrayRegion(array.ObjectHandle, 0, length, (double*)pResult);
            else if (t == typeof(float))
                _env.GetFloatArrayRegion(array.ObjectHandle, 0, length, (float*)pResult);
            else if (t == typeof(byte))
                _env.GetByteArrayRegion(array.ObjectHandle, 0, length, (byte*)pResult);
            else if (t == typeof(char))
                _env.GetCharArrayRegion(array.ObjectHandle, 0, length, (char*)pResult);
            else if (t == typeof(short))
                _env.GetShortArrayRegion(array.ObjectHandle, 0, length, (short*)pResult);
            else if (t == typeof(bool))
                _env.GetBooleanArrayRegion(array.ObjectHandle, 0, length, (byte*)pResult);
            else
                throw new NotSupportedException($"Unsupported primitive array type: {t}");
        }

        return result;
    }

    public JvmValue NewObjectArray(JvmClass elementClass, JvmValue[] elements)
    {
        if (elementClass is null) throw new ArgumentNullException(nameof(elementClass));
        if (elements is null) throw new ArgumentNullException(nameof(elements));

        var array = _env.NewObjectArray(elements.Length, elementClass.Handle, IntPtr.Zero);
        for (int i = 0; i < elements.Length; i++)
        {
            var handle = elements[i].Type == JvmValueType.Object ? elements[i].ObjectHandle : IntPtr.Zero;
            _env.SetObjectArrayElement(array, i, handle);
        }
        return JvmValue.FromObject(array);
    }

    // ---- 异常 ----

    public JvmObject? GetPendingException()
    {
        var throwable = _env.ExceptionOccurred();
        if (throwable == IntPtr.Zero) return null;
        var global = _env.NewGlobalRef(throwable);
        _env.DeleteLocalRef(throwable);
        var throwableCls = CreateOwnedClass(_env.NewGlobalRef(_env.GetObjectClass(global)), "java/lang/Throwable");
        return CreateOwnedObject(global, throwableCls);
    }

    public string GetExceptionMessage(JvmObject exception)
    {
        EnsureThrowableCache();
        var msgJStr = _env.CallObjectMethodA(exception.Handle, _throwableGetMessage, IntPtr.Zero);
        if (msgJStr == IntPtr.Zero) return string.Empty;
        try { return _env.GetStringUTFChars(msgJStr); }
        finally { _env.DeleteLocalRef(msgJStr); }
    }

    public string GetExceptionStackTrace(JvmObject exception)
    {
        EnsureThrowableCache();
        // 调用 getStackTrace() 返回 StackTraceElement[]
        var traceArray = _env.CallObjectMethodA(exception.Handle, _throwableGetStackTrace, IntPtr.Zero);
        if (traceArray == IntPtr.Zero) return string.Empty;
        try
        {
            var length = _env.GetArrayLength(traceArray);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < length; i++)
            {
                var element = _env.GetObjectArrayElement(traceArray, i);
                if (element == IntPtr.Zero) continue;
                try
                {
                    var strJStr = _env.CallObjectMethodA(element, _stackTraceElementToString, IntPtr.Zero);
                    if (strJStr != IntPtr.Zero)
                    {
                        try { sb.AppendLine("\tat " + _env.GetStringUTFChars(strJStr)); }
                        finally { _env.DeleteLocalRef(strJStr); }
                    }
                }
                finally { _env.DeleteLocalRef(element); }
            }
            return sb.ToString();
        }
        finally { _env.DeleteLocalRef(traceArray); }
    }

    // ---- Java→C# 回调 ----

    public void RegisterCallback(JvmClass clazz, string methodName, string signature, Delegate callback)
        => _callbacks.Register(clazz.Handle, methodName, signature, callback);

    public void UnregisterCallbacks(JvmClass clazz)
        => _callbacks.UnregisterAll(clazz.Handle);

    // ---- 全局引用管理 ----

    public IntPtr NewGlobalRef(IntPtr localRef)
        => _env.NewGlobalRef(localRef);

    public void DeleteGlobalRef(IntPtr globalRef)
    {
        if (!_disposed) _env.DeleteGlobalRef(globalRef);
    }

    private void EnsureThrowableCache()
    {
        if (_throwableClass != IntPtr.Zero) return;

        _throwableClass = _env.NewGlobalRef(_env.FindClass("java/lang/Throwable"));
        _throwableGetMessage = _env.GetMethodID(_throwableClass, "getMessage", "()Ljava/lang/String;");
        _throwableGetStackTrace = _env.GetMethodID(_throwableClass, "getStackTrace", "()[Ljava/lang/StackTraceElement;");

        _stackTraceElementClass = _env.NewGlobalRef(_env.FindClass("java/lang/StackTraceElement"));
        _stackTraceElementToString = _env.GetMethodID(_stackTraceElementClass, "toString", "()Ljava/lang/String;");
    }

    // ---- helpers ----

    private void EnsureClassCache()
    {
        if (_urlClass != IntPtr.Zero) return;

        _urlClass = _env.NewGlobalRef(_env.FindClass("java/net/URL"));
        // new URL(String spec) — single-arg ctor; deprecated in Java 20+ but still functional in 21.
        _urlCtor = _env.GetMethodID(_urlClass, "<init>", "(Ljava/lang/String;)V");

        _urlClassLoaderClass = _env.NewGlobalRef(_env.FindClass("java/net/URLClassLoader"));
        _urlClassLoaderCtor = _env.GetMethodID(_urlClassLoaderClass, "<init>", "([Ljava/net/URL;Ljava/lang/ClassLoader;)V");

        _stringClass = _env.NewGlobalRef(_env.FindClass("java/lang/String"));
        _threadClass = _env.NewGlobalRef(_env.FindClass("java/lang/Thread"));
        _threadCurrentThread = _env.GetStaticMethodID(_threadClass, "currentThread", "()Ljava/lang/Thread;");
        _threadGetContextClassLoader = _env.GetMethodID(_threadClass, "getContextClassLoader", "()Ljava/lang/ClassLoader;");
        _threadSetContextClassLoader = _env.GetMethodID(_threadClass, "setContextClassLoader", "(Ljava/lang/ClassLoader;)V");

        _classClass = _env.NewGlobalRef(_env.FindClass("java/lang/Class"));
        _classForName = _env.GetStaticMethodID(_classClass, "forName", "(Ljava/lang/String;ZLjava/lang/ClassLoader;)Ljava/lang/Class;");
        _classGetName = _env.GetMethodID(_classClass, "getName", "()Ljava/lang/String;");
    }

    private static char ParseReturnType(string signature)
    {
        // 形如 "(args)R" — 找到 ')' 后第一个字符。
        var close = signature.IndexOf(')');
        if (close < 0 || close + 1 >= signature.Length)
            throw new ArgumentException($"Invalid JNI signature: {signature}", nameof(signature));
        return signature[close + 1];
    }

    private static JvmValue RunVoid(Action body)
    {
        body();
        return JvmValue.Null;
    }
}
