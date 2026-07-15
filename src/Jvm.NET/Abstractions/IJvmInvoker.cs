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

    // ---- 字段访问 ----

    /// <summary>
    /// 读取实例字段的值。签名用于定位字段并决定返回类型，
    /// 例如 <c>(I)</c> 表示 int 字段，<c>(Ljava/lang/String;)</c> 表示 String 字段。
    /// </summary>
    JvmValue GetField(JvmObject instance, string name, string signature);

    /// <summary>写入实例字段。</summary>
    void SetField(JvmObject instance, string name, string signature, JvmValue value);

    /// <summary>读取静态字段的值。</summary>
    JvmValue GetStaticField(JvmClass clazz, string name, string signature);

    /// <summary>写入静态字段。</summary>
    void SetStaticField(JvmClass clazz, string name, string signature, JvmValue value);

    // ---- 类型层次 ----

    /// <summary>检查 <paramref name="instance"/> 是否是 <paramref name="clazz"/> 的实例。</summary>
    bool IsInstanceOf(JvmObject instance, JvmClass clazz);

    /// <summary>检查 <paramref name="from"/> 是否可赋值给 <paramref name="to"/>。</summary>
    bool IsAssignableFrom(JvmClass from, JvmClass to);

    /// <summary>获取父类，<paramref name="clazz"/> 为 Object 或接口时返回 null。</summary>
    JvmClass? GetSuperclass(JvmClass clazz);

    /// <summary>获取 <paramref name="instance"/> 的运行时类。</summary>
    JvmClass GetObjectClass(JvmObject instance);

    // ---- 数组 ----

    /// <summary>获取数组长度。</summary>
    int GetArrayLength(JvmValue array);

    /// <summary>读取对象数组的元素。</summary>
    JvmValue GetObjectArrayElement(JvmValue array, int index);

    /// <summary>设置对象数组的元素。</summary>
    void SetObjectArrayElement(JvmValue array, int index, JvmValue value);

    /// <summary>
    /// 创建 Java 基本类型数组并用 <paramref name="values"/> 填充。
    /// <typeparamref name="T"/> 必须是 Java 基本类型的 .NET 对应类型：
    /// <c>bool</c>/<c>byte</c>/<c>char</c>/<c>short</c>/<c>int</c>/<c>long</c>/<c>float</c>/<c>double</c>。
    /// </summary>
    JvmValue NewArray<T>(T[] values) where T : unmanaged;

    /// <summary>读取 Java 基本类型数组的所有元素。</summary>
    T[] GetArrayValues<T>(JvmValue array) where T : unmanaged;

    /// <summary>
    /// 创建 Java 对象数组。<paramref name="elementClass"/> 是元素类型，
    /// <paramref name="elements"/> 是初始元素（可为空数组）。
    /// </summary>
    JvmValue NewObjectArray(JvmClass elementClass, JvmValue[] elements);

    // ---- 异常 ----

    /// <summary>
    /// 获取当前 pending 异常（不清除）。返回的 <see cref="JvmObject"/> 包装 jthrowable，
    /// 无 pending 异常时返回 null。
    /// </summary>
    JvmObject? GetPendingException();

    /// <summary>调用 <c>Throwable.getMessage()</c> 获取异常消息。</summary>
    string GetExceptionMessage(JvmObject exception);

    /// <summary>调用 <c>Throwable.getStackTrace()</c> 获取堆栈跟踪字符串。</summary>
    string GetExceptionStackTrace(JvmObject exception);

    // ---- Java→C# 回调 ----

    /// <summary>
    /// 在 <paramref name="clazz"/> 上注册 native 方法，使 Java 代码可以回调到 .NET 委托。
    /// <para>
    /// <paramref name="clazz"/> 必须是一个已加载的 Java 类，且其中已声明了
    /// <c>native</c> 方法 <paramref name="methodName"/>（签名匹配 <paramref name="signature"/>）。
    /// 通常通过 ASM 字节码生成或 Java 源码声明。
    /// </para>
    /// <para>
    /// <paramref name="callback"/> 的签名必须与 JNI 回调约定一致：
    /// 第一个参数是 <c>IntPtr jniEnv</c>，第二个是 <c>IntPtr jclass</c>（静态方法）或
    /// <c>IntPtr jobject</c>（实例方法），之后按 <paramref name="signature"/> 顺序的参数。
    /// 返回值按签名返回类型匹配。
    /// </para>
    /// <para>
    /// 委托会被强引用保持，直到 <see cref="UnregisterCallbacks"/> 被调用或 Invoker 释放。
    /// </para>
    /// </summary>
    void RegisterCallback(JvmClass clazz, string methodName, string signature, Delegate callback);

    /// <summary>注销 <paramref name="clazz"/> 上所有通过 <see cref="RegisterCallback"/> 注册的 native 方法。</summary>
    void UnregisterCallbacks(JvmClass clazz);
}
