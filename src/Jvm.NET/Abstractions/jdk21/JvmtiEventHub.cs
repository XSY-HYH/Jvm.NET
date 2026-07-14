using System.Runtime.InteropServices;
using Jvm.NET.Interop;
using Jvm.NET.Interop.Jvmti;

namespace Jvm.NET.Abstractions.Jdk21;

//
// JVMTI 的事件回调表是 per-jvmtiEnv 的全局状态：SetEventCallbacks 会整体替换
// 回调表，因此 BytecodeModifier 和 EventListener 不能各自独立调用它。
//
// JvmtiEventHub 作为共享协调者：
//   1. 持有唯一的 JvmtiEventCallbacks 表
//   2. 持有所有托管委托引用（防止 GC 回收函数指针）
//   3. 在所有组件注册完回调后由 Runtime 统一调用 Apply()
//   4. 提供 EnableEvent / DisableEvent 的封装
//
// 注意：回调可能由非托管线程调用，访问托管对象前需确保当前线程已 AttachCurrentThread
// （JVMTI 事件回调默认会附带 JNIEnv*，JVM 已经把回调线程 attach 了）。
//
internal sealed unsafe class JvmtiEventHub
{
    private readonly IntPtr _jvmtiEnv;
    private JvmtiEventCallbacks _callbacks;
    private bool _installed;

    // 持有委托引用防止 GC 回收函数指针。只要 hub 活着这些委托就不会被回收。
    private readonly List<object> _pins = new();

    public JvmtiEventHub(IntPtr jvmtiEnv)
    {
        if (jvmtiEnv == IntPtr.Zero)
            throw new ArgumentNullException(nameof(jvmtiEnv));
        _jvmtiEnv = jvmtiEnv;
    }

    /// <summary>拿到 JVMTI 函数表的 managed ref，供外部调用 Allocate / RetransformClasses 等。</summary>
    public ref JvmtiInterface_1_ Interface => ref JvmtiInterface_1_.FromJvmtiEnv(_jvmtiEnv);

    /// <summary>回调表引用，供 BytecodeModifier / EventListener 填写槽位。</summary>
    public ref JvmtiEventCallbacks Callbacks => ref _callbacks;

    public IntPtr JvmtiEnv => _jvmtiEnv;

    /// <summary>
    /// 把托管委托安装到回调表的指定槽位。委托引用会被 hub 钉住，直到 hub 被重置。
    /// </summary>
    public void SetCallback<TDelegate>(ref IntPtr slot, TDelegate callback) where TDelegate : Delegate
    {
        slot = Marshal.GetFunctionPointerForDelegate(callback);
        _pins.Add(callback);
    }

    /// <summary>
    /// 把当前回调表提交给 JVMTI。必须在所有组件注册完回调后由 Runtime 调用一次。
    /// 幂等：重复调用不会重新提交（如需更新表请先 Reset）。
    /// </summary>
    public void Apply()
    {
        if (_installed) return;
        ref var jvmti = ref Interface;
        fixed (JvmtiEventCallbacks* pCallbacks = &_callbacks)
        {
            int rc = jvmti.SetEventCallbacks(_jvmtiEnv, pCallbacks, JvmtiEventCallbacks.SizeInBytes);
            if (rc != NativeConstants.JNI_OK)
                throw new InvalidOperationException($"jvmtiEnv->SetEventCallbacks returned {rc}.");
        }
        _installed = true;
    }

    /// <summary>启用指定 event kind 的通知（全局，对所有线程生效）。</summary>
    public void EnableEvent(int eventKind)
    {
        ref var jvmti = ref Interface;
        int rc = jvmti.SetEventNotificationMode(_jvmtiEnv, JvmtiEventMode.JVMTI_ENABLE, eventKind, IntPtr.Zero);
        if (rc != NativeConstants.JNI_OK)
            throw new InvalidOperationException($"SetEventNotificationMode(ENABLE, {eventKind}) returned {rc}.");
    }

    /// <summary>禁用指定 event kind 的通知。失败时静默（用于 Dispose 路径）。</summary>
    public void DisableEvent(int eventKind)
    {
        if (!_installed) return;
        ref var jvmti = ref Interface;
        jvmti.SetEventNotificationMode(_jvmtiEnv, JvmtiEventMode.JVMTI_DISABLE, eventKind, IntPtr.Zero);
    }

    /// <summary>
    /// 清空回调表并通知 JVMTI。仅由 Runtime 在 Shutdown 时调用。
    /// </summary>
    public void Reset()
    {
        if (!_installed) return;
        ref var jvmti = ref Interface;
        jvmti.SetEventCallbacks(_jvmtiEnv, null, 0);
        _pins.Clear();
        _callbacks = default;
        _installed = false;
    }
}
