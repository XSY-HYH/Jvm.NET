using System.Runtime.InteropServices;

namespace Jvm.NET.Abstractions.Jdk21;

/// <summary>
/// JDK 21 specific <see cref="IJvmInvoker"/>. Backed by a <see cref="JniEnvHandle"/>
/// obtained at JVM startup. Calls into the standard JNI function table.
/// </summary>
internal sealed unsafe class Jdk21Invoker : IJvmInvoker, IDisposable
{
    private readonly JniEnvHandle _env;

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

    // Cache of URLClassLoaders created via LoadJar — last one wins for LoadClass/RunMain.
    private IntPtr _activeClassLoader;
    private IntPtr _classLoaderLoadClass;

    public Jdk21Invoker(IntPtr jniEnv)
    {
        _env = new JniEnvHandle(jniEnv);
    }

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
        return new JvmClass(global, fullyQualifiedName);
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
            return new JvmClass(global, fullyQualifiedName);
        }
        finally
        {
            _env.DeleteLocalRef(nameJStr);
        }
    }

    public JvmObject NewObject(JvmClass clazz, string constructorSignature, params JvmValue[] args)
    {
        var ctorId = _env.GetMethodID(clazz.Handle, "<init>", constructorSignature);
        using var jargs = new JValueArray(args);
        var obj = _env.NewObjectA(clazz.Handle, ctorId, jargs.RawPointer);
        var global = _env.NewGlobalRef(obj);
        _env.DeleteLocalRef(obj);
        return new JvmObject(global, clazz);
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
        var methodId = _env.GetMethodID(instance.Class.Handle, methodName, signature);
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

    public void Dispose()
    {
        if (_activeClassLoader != IntPtr.Zero)
        {
            _env.DeleteGlobalRef(_activeClassLoader);
            _activeClassLoader = IntPtr.Zero;
        }
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
