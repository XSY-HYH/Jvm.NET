using System.Runtime.InteropServices;
using Jvm.NET.Interop.Jni;

namespace Jvm.NET.Interop;

/// <summary>
/// 通过 [DllImport] 声明 JNI 入口点，借助 <see cref="NativeLibrary.SetDllImportResolver"/>
/// 解析到运行时加载的 jvm.dll。
/// 用 unsafe 指针完全绕过 marshalling，避免 .NET 10 的 P/Invoke thunk 崩溃。
/// </summary>
internal static class PInvoke
{
    [DllImport("jvm", EntryPoint = "JNI_CreateJavaVM", CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int CreateJavaVM(
        IntPtr* pvm,
        IntPtr* penv,
        JavaVMInitArgs* args);
}
