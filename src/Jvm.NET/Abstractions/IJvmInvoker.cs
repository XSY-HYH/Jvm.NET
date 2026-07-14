namespace Jvm.NET.Abstractions;

/// <summary>
/// Entry point for invoking Java code from .NET: loading jars / classes,
/// constructing objects, and calling static / virtual methods.
/// </summary>
public interface IJvmInvoker
{
    /// <summary>
    /// Appends a jar file (or directory) to the running JVM's classpath at runtime.
    /// Equivalent to adding it to <c>-cp</c> at startup.
    /// </summary>
    /// <param name="jarPath">Absolute path to the jar file or class directory.</param>
    void LoadJar(string jarPath);

    /// <summary>
    /// Resolves a class that has already been loaded by the JVM.
    /// Returns <c>null</c> when the class is not found (no exception thrown).
    /// </summary>
    /// <param name="fullyQualifiedName">Class name using dots, e.g. <c>java.lang.String</c>.</param>
    JvmClass? FindClass(string fullyQualifiedName);

    /// <summary>
    /// Forces the JVM to load (and link) the given class, throwing if it cannot be found.
    /// </summary>
    JvmClass LoadClass(string fullyQualifiedName);

    /// <summary>
    /// Allocates a new instance of <paramref name="clazz"/> and invokes the constructor
    /// matching <paramref name="constructorSignature"/>.
    /// </summary>
    /// <param name="constructorSignature">JNI-style signature without the enclosing class,
    /// e.g. <c>(Ljava/lang/String;I)V</c>.</param>
    JvmObject NewObject(JvmClass clazz, string constructorSignature, params JvmValue[] args);

    /// <summary>
    /// Invokes a static method on <paramref name="clazz"/>.
    /// </summary>
    /// <param name="methodName">JVM method name, e.g. <c>valueOf</c>.</param>
    /// <param name="signature">JNI signature, e.g. <c>(I)Ljava/lang/Integer;</c>.</param>
    JvmValue InvokeStatic(JvmClass clazz, string methodName, string signature, params JvmValue[] args);

    /// <summary>
    /// Invokes an instance (virtual) method on <paramref name="instance"/>.
    /// </summary>
    JvmValue InvokeVirtual(JvmObject instance, string methodName, string signature, params JvmValue[] args);

    /// <summary>
    /// Convenience entry point: loads <paramref name="jarPath"/>, locates the given
    /// main class and invokes <c>public static void main(String[] args)</c> with the
    /// supplied arguments. This is the primary API for "launch a jar in-process".
    /// </summary>
    /// <param name="jarPath">Absolute path to the jar whose manifest / main class should run.</param>
    /// <param name="mainClassName">Fully-qualified main class name (e.g. <c>com.example.App</c>).</param>
    /// <param name="args">Arguments forwarded to <c>main</c> as <c>String[]</c>.</param>
    void RunMain(string jarPath, string mainClassName, params string[] args);

    /// <summary>
    /// Creates a Java <c>String</c> from a .NET string. The returned <see cref="JvmValue"/>
    /// holds a JNI local reference; pass it directly as an argument to
    /// <see cref="InvokeStatic"/> / <see cref="InvokeVirtual"/> / <see cref="NewObject"/>.
    /// </summary>
    /// <remarks>
    /// The local reference is valid only for the duration of the current JNI frame.
    /// For long-lived strings, promote via <c>NewGlobalRef</c> on the raw handle.
    /// </remarks>
    JvmValue NewString(string str);

    /// <summary>
    /// Reads a Java <c>String</c> (modified UTF-8) back into a .NET <see cref="string"/>.
    /// Accepts any <c>jstring</c> handle, whether local or global ref.
    /// </summary>
    string GetString(IntPtr javaStringHandle);

    /// <summary>
    /// Creates a Java <c>String[]</c> from .NET strings. The returned <see cref="JvmValue"/>
    /// holds a JNI local reference; pass it directly as an argument to
    /// <see cref="InvokeStatic"/> / <see cref="InvokeVirtual"/> (e.g. for calling <c>main</c>).
    /// </summary>
    JvmValue NewStringArray(string[] args);
}
