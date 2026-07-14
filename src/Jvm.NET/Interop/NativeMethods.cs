using System.Runtime.InteropServices;

namespace Jvm.NET.Interop;

/// <summary>
/// Resolves the three JNI entry points from a previously loaded native JVM library handle.
/// The version abstraction layers (e.g. <c>Jdk21Runtime</c>) call into this to obtain
/// callable delegates without relying on <c>DllImport</c> (which would require the
/// library name / path to be known at compile time).
/// </summary>
internal static class NativeMethods
{
    private const string CreateJavaVM = "JNI_CreateJavaVM";
    private const string GetDefaultJavaVMInitArgs = "JNI_GetDefaultJavaVMInitArgs";
    private const string GetCreatedJavaVMs = "JNI_GetCreatedJavaVMs";

    /// <summary>Resolves <c>JNI_CreateJavaVM</c> from <paramref name="libraryHandle"/>.</summary>
    public static JniDelegates.JNI_CreateJavaVMDelegate GetCreateJavaVM(INativeLibraryLoader loader, IntPtr libraryHandle)
    {
        var ptr = loader.GetExport(libraryHandle, CreateJavaVM);
        return Marshal.GetDelegateForFunctionPointer<JniDelegates.JNI_CreateJavaVMDelegate>(ptr);
    }

    /// <summary>Resolves <c>JNI_GetDefaultJavaVMInitArgs</c> from <paramref name="libraryHandle"/>.</summary>
    public static JniDelegates.JNI_GetDefaultJavaVMInitArgsDelegate GetGetDefaultJavaVMInitArgs(INativeLibraryLoader loader, IntPtr libraryHandle)
    {
        var ptr = loader.GetExport(libraryHandle, GetDefaultJavaVMInitArgs);
        return Marshal.GetDelegateForFunctionPointer<JniDelegates.JNI_GetDefaultJavaVMInitArgsDelegate>(ptr);
    }

    /// <summary>Resolves <c>JNI_GetCreatedJavaVMs</c> from <paramref name="libraryHandle"/>.</summary>
    public static JniDelegates.JNI_GetCreatedJavaVMsDelegate GetGetCreatedJavaVMs(INativeLibraryLoader loader, IntPtr libraryHandle)
    {
        var ptr = loader.GetExport(libraryHandle, GetCreatedJavaVMs);
        return Marshal.GetDelegateForFunctionPointer<JniDelegates.JNI_GetCreatedJavaVMsDelegate>(ptr);
    }
}
