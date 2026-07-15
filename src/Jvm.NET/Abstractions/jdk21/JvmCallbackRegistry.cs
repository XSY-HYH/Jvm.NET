using System.Runtime.InteropServices;
using Jvm.NET.Interop.Jni;

namespace Jvm.NET.Abstractions.Jdk21;

/// <summary>
/// 管理 Java→C# 回调的注册和生命周期。
/// <para>
/// 通过 JNI <c>RegisterNatives</c> 把 .NET 委托注册为 Java 类的 native 方法实现。
/// 委托必须保持强引用，否则 GC 回收后函数指针失效会导致 JVM 崩溃。
/// </para>
/// <para>
/// 线程安全：所有方法都加锁。
/// </para>
/// </summary>
internal sealed unsafe class JvmCallbackRegistry : IDisposable
{
    private readonly JniEnvHandle _env;

    // jclass(global ref handle) → 已注册的委托列表（强引用保持活着）
    private readonly Dictionary<IntPtr, List<Delegate>> _callbacks = new();

    private readonly object _gate = new();
    private bool _disposed;

    public JvmCallbackRegistry(JniEnvHandle env)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    /// <summary>
    /// 在 <paramref name="clazz"/> 上注册一个 native 方法实现。
    /// <paramref name="callback"/> 的签名必须匹配 JNI 回调约定。
    /// </summary>
    public void Register(IntPtr clazz, string methodName, string signature, Delegate callback)
    {
        ThrowIfDisposed();
        if (clazz == IntPtr.Zero) throw new ArgumentNullException(nameof(clazz));
        if (string.IsNullOrEmpty(methodName)) throw new ArgumentException("methodName must be non-empty", nameof(methodName));
        if (string.IsNullOrEmpty(signature)) throw new ArgumentException("signature must be non-empty", nameof(signature));
        if (callback is null) throw new ArgumentNullException(nameof(callback));

        // JNINativeMethod 的 Name/Signature 字段是 char*，RegisterNatives 会拷贝内容所以注册后即可释放。
        var method = new JNINativeMethod
        {
            Name = (IntPtr)Marshal.StringToHGlobalAnsi(methodName),
            Signature = (IntPtr)Marshal.StringToHGlobalAnsi(signature),
            FnPtr = Marshal.GetFunctionPointerForDelegate(callback),
        };

        try
        {
            _env.RegisterNatives(clazz, new[] { method });
        }
        finally
        {
            Marshal.FreeHGlobal(method.Name);
            Marshal.FreeHGlobal(method.Signature);
        }

        // 保持委托强引用：RegisterNatives 只保存了函数指针，不知道对应的委托对象。
        // 如果委托被 GC 回收，函数指针将指向已释放的 trampoline，JVM 调用时会崩溃。
        lock (_gate)
        {
            if (!_callbacks.TryGetValue(clazz, out var list))
            {
                list = new List<Delegate>();
                _callbacks[clazz] = list;
            }
            list.Add(callback);
        }
    }

    /// <summary>注销 <paramref name="clazz"/> 上所有通过 <see cref="Register"/> 注册的 native 方法。</summary>
    public void UnregisterAll(IntPtr clazz)
    {
        ThrowIfDisposed();
        if (clazz == IntPtr.Zero) return;

        try { _env.UnregisterNatives(clazz); } catch { /* 静默 */ }

        lock (_gate)
        {
            _callbacks.Remove(clazz);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_gate)
        {
            foreach (var kvp in _callbacks)
            {
                try { _env.UnregisterNatives(kvp.Key); } catch { /* Dispose 路径静默 */ }
            }
            _callbacks.Clear();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JvmCallbackRegistry));
    }
}
