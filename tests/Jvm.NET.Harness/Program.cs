using System.Text;
using Jvm.NET;
using Jvm.NET.Abstractions;
using Jvm.NET.Abstractions.Jdk21;
using Jvm.NET.Asm;

// 测试程序：使用 Jvm.NET 嵌入 JVM，测试 RunMain / InvokeStatic / 字节码修改 / 事件监听。
// JDK 路径由用户指定：D:\Program Files\Microsoft\jdk-21.0.9.10-hotspot

const string JdkBinPath = @"D:\Program Files\Microsoft\jdk-21.0.9.10-hotspot\bin";
var jarPath = Path.Combine(AppContext.BaseDirectory, "Java", "HelloWorld.jar");

Console.WriteLine($"[Harness] JDK bin: {JdkBinPath}");
Console.WriteLine($"[Harness] Jar:     {jarPath}");
Console.WriteLine($"[Harness] Jar exists: {File.Exists(jarPath)}");
Console.WriteLine();

var options = new JvmInitializationOptions
{
    JdkBinPath                 = JdkBinPath,
    Version                    = JdkVersion.Jdk21,
    EnableBytecodeModification = true,
    EnableEventListening       = true,   // 本次需要测试事件监听
};

using var runtime = JvmInitializer.Initialize(options);
Console.WriteLine($"[Harness] JVM started. State={runtime.State}, Version={runtime.Version}");
Console.WriteLine();

var invoker = runtime.Invoker;

// 1) RunMain（原始字节码）
Console.WriteLine("=== Test 1: RunMain (original bytecode) ===");
invoker.RunMain(jarPath, "HelloWorld", "from-C#", "second-arg", "third");
Console.WriteLine();

// 2) InvokeStatic add(int, int) -> int
Console.WriteLine("=== Test 2: InvokeStatic add(3, 5) ===");
var helloClass = invoker.LoadClass("HelloWorld");
var sum = invoker.InvokeStatic(helloClass, "add", "(II)I",
    JvmValue.FromInt(3), JvmValue.FromInt(5));
Console.WriteLine($"add(3, 5) = {sum.AsInt()}");
Console.WriteLine();

// 3) InvokeStatic echo(String) -> String
Console.WriteLine("=== Test 3: InvokeStatic echo(\"Hello Jvm.NET\") ===");
var arg = invoker.NewString("Hello Jvm.NET");
var ret = invoker.InvokeStatic(helloClass, "echo", "(Ljava/lang/String;)Ljava/lang/String;", arg);
var echoed = invoker.GetString(ret.ObjectHandle);
Console.WriteLine($"echo(\"Hello Jvm.NET\") = \"{echoed}\"");
Console.WriteLine();

// 4) 字节码修改（等长替换）
Console.WriteLine("=== Test 4: Bytecode modification (equal length) ===");
Console.WriteLine("    (transformer replaces \"Hello from Java!\" -> \"Pwned from Java!\")");
var transformer = new StringReplaceTransformer("Hello from Java!", "Pwned from Java!");
using (runtime.BytecodeModifier.RegisterTransformer(transformer))
{
    invoker.RunMain(jarPath, "HelloWorld", "post-mod");
}
Console.WriteLine($"    Transformer hit count: {transformer.HitCount}");
Console.WriteLine();

// 5) 事件监听测试
// 注意：MethodEntry/MethodExit/Exception 事件需要 onload-only capability，
// 嵌入式 JVM 在 live phase 无法启用。这里只测试 live phase 可用的事件：
// ClassPrepare / ClassLoad / ThreadStart。
// 为触发事件，订阅后会加载一个新的 Java 标准库类（java.util.regex.Pattern）。
Console.WriteLine("=== Test 5: Event listening (ClassPrepare / ClassLoad / ThreadStart) ===");
Console.WriteLine("    (MethodEntry/MethodExit/Exception 需要 onload-only capability，");
Console.WriteLine("     嵌入式 JVM 在 live phase 无法启用，已跳过。)");
var classEvents = new List<string>();
int threadStartCount = 0;

using (runtime.EventListener.SubscribeClassPrepare(data =>
{
    lock (classEvents)
        classEvents.Add($"ClassPrepare: {data.Class.Name}");
}))
using (runtime.EventListener.SubscribeClassLoad(data =>
{
    lock (classEvents)
        classEvents.Add($"ClassLoad:     {data.Class.Name}");
}))
using (runtime.EventListener.SubscribeThreadStart(data =>
{
    Interlocked.Increment(ref threadStartCount);
}))
{
    Console.WriteLine("    Loading java/util/regex/Pattern to trigger class events...");
    // 故意加载一个之前没碰过的 Java 标准库类，触发 ClassLoad / ClassPrepare。
    // 用 FindClass（走 JVM 启动类加载器），因为 LoadClass 走应用类加载器，
    // 应用类加载器用斜杠格式会抛 ClassNotFoundException。
    var patternClass = invoker.FindClass("java/util/regex/Pattern");
    Console.WriteLine($"    Pattern class handle: {patternClass?.Handle ?? IntPtr.Zero}");
}
Console.WriteLine($"    ThreadStart events: {threadStartCount}");
Console.WriteLine($"    Class-related events: {classEvents.Count}");
foreach (var e in classEvents.Take(20))
    Console.WriteLine($"      {e}");
if (classEvents.Count > 20)
    Console.WriteLine($"      ... and {classEvents.Count - 20} more");
Console.WriteLine();

// 6) 方案 B：通过字节码插桩模拟 MethodEntry/MethodExit 事件
// 嵌入式 JVM 在 live phase 无法添加 onload-only capability（can_generate_method_entry_events 等），
// 方案 B 通过 ClassFileLoadHook 在目标方法入口/出口插入 invokestatic JnBridge.onMethod* 指令，
// JVM 执行到此处通过 JNI 回调 .NET，模拟 MethodEntry/MethodExit 事件。
// 注意：插桩只对订阅后新加载的类生效。Target 类之前没加载过，适合测试。
Console.WriteLine("=== Test 6: MethodEntry/MethodExit via bytecode instrumentation (方案 B) ===");
var entryEvents = new List<string>();
var exitEvents = new List<string>();

using (runtime.EventListener.SubscribeMethodEntry(data =>
{
    lock (entryEvents)
        entryEvents.Add($"{data.Class.Name}.{data.MethodName}");
}))
using (runtime.EventListener.SubscribeMethodExit(data =>
{
    lock (exitEvents)
        exitEvents.Add($"{data.Class.Name}.{data.MethodName}");
}))
{
    Console.WriteLine("    Loading Target class (first time, triggers instrumentation)...");
    var targetClass = invoker.LoadClass("Target");
    Console.WriteLine($"    Target class handle: {targetClass?.Handle ?? IntPtr.Zero}");

    Console.WriteLine("    Calling Target.compute(6, 7)...");
    var result = invoker.InvokeStatic(targetClass, "compute", "(II)I",
        JvmValue.FromInt(6), JvmValue.FromInt(7));
    Console.WriteLine($"    compute(6, 7) = {result.AsInt()} (expected 84)");
}
Console.WriteLine($"    MethodEntry events: {entryEvents.Count}");
foreach (var e in entryEvents.Take(10))
    Console.WriteLine($"      {e}");
Console.WriteLine($"    MethodExit events: {exitEvents.Count}");
foreach (var e in exitEvents.Take(10))
    Console.WriteLine($"      {e}");
Console.WriteLine();

// 7) 方案 B：Exception 事件（字节码插桩 try-catch 模拟）
// can_generate_exception_events 也是 onload-only，live phase 加不了。
// 方案 B：在方法体外包裹 try-catch，catch 块中调用 JnBridge.onException 回调 .NET。
Console.WriteLine("=== Test 7: Exception event via bytecode instrumentation (方案 B) ===");
var exceptionEvents = new List<string>();

using (runtime.EventListener.SubscribeException(data =>
{
    lock (exceptionEvents)
        exceptionEvents.Add($"{data.ThrowingClass?.Name ?? "<unknown>"}.{data.MethodName}: {data.Exception.Class.Name}");
}))
{
    Console.WriteLine("    Loading TargetEx class (first time, triggers instrumentation)...");
    var targetExClass = invoker.LoadClass("TargetEx");
    Console.WriteLine($"    TargetEx class handle: {targetExClass?.Handle ?? IntPtr.Zero}");

    Console.WriteLine("    Calling TargetEx.throwEx() — expect Java exception caught by instrumentation...");
    try
    {
        invoker.InvokeStatic(targetExClass, "throwEx", "()V");
    }
    catch (JvmException)
    {
        // Java 异常会被 JnBridge.onException 捕获回调后再重新抛出，
        // .NET 侧通过 JNI 检测到 pending exception 并抛出 JvmException。
        Console.WriteLine("    Java exception propagated to .NET as expected.");
    }
}
Console.WriteLine($"    Exception events: {exceptionEvents.Count}");
foreach (var e in exceptionEvents.Take(10))
    Console.WriteLine($"      {e}");
Console.WriteLine();

// 8) 不等长替换测试（基于 ASM，正常工作）
// 之前的直接字节替换方式无法支持不等长（破坏常量池 length 字段导致 ClassFormatError）。
// 用 ASM ClassReader/ClassWriter 重写 class 文件，常量池会自动重建，支持任意长度替换。
Console.WriteLine("=== Test 8: Bytecode modification (unequal length via ASM) ===");
Console.WriteLine("    (transformer replaces \"Hello from Java!\" -> \"HACKED by ASM\")");
var asmTransformer = new AsmStringReplaceTransformer("Hello from Java!", "HACKED by ASM");
using (runtime.BytecodeModifier.RegisterTransformer(asmTransformer))
{
    invoker.RunMain(jarPath, "HelloWorld", "unequal-test");
}
Console.WriteLine($"    ASM transformer hit count: {asmTransformer.HitCount}");
Console.WriteLine();

Console.WriteLine("[Harness] All tests completed.");

// ---- 等长字符串替换 transformer ----
// 在 class 字节码中扫描 pattern 字节序列，替换为等长的 replacement。
// 等长是为了不改 class 文件常量池的 length 字段，避免解析 class 文件格式。
sealed class StringReplaceTransformer : IBytecodeTransformer
{
    private readonly byte[] _pattern;
    private readonly byte[] _replacement;
    private int _hitCount;

    public StringReplaceTransformer(string pattern, string replacement)
    {
        _pattern = Encoding.UTF8.GetBytes(pattern);
        _replacement = Encoding.UTF8.GetBytes(replacement);
        if (_pattern.Length != _replacement.Length)
            throw new ArgumentException(
                $"Pattern ({_pattern.Length} bytes) and replacement ({_replacement.Length} bytes) must be the same length.");
    }

    public string Name => "StringReplace";
    public int HitCount => _hitCount;

    public byte[]? Transform(string className, byte[] originalBytes)
    {
        if (className != "HelloWorld")
            return null;

        var idx = IndexOf(originalBytes, _pattern);
        if (idx < 0)
            return null;

        var result = (byte[])originalBytes.Clone();
        Buffer.BlockCopy(_replacement, 0, result, idx, _replacement.Length);
        Interlocked.Increment(ref _hitCount);
        return result;
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}

// ---- 基于 ASM 的字符串替换 transformer（支持不等长）----
// 用 ClassReader 读取 class 字节，自定义 MethodVisitor 拦截 VisitLdcInsn，
// 匹配的字符串替换为新值，ClassWriter 重写整个 class 文件。
// 常量池由 ClassWriter 自动重建，支持任意长度替换，不会破坏 class 文件结构。
sealed class AsmStringReplaceTransformer : IBytecodeTransformer
{
    private readonly string _pattern;
    private readonly string _replacement;
    private int _hitCount;

    public AsmStringReplaceTransformer(string pattern, string replacement)
    {
        _pattern = pattern;
        _replacement = replacement;
    }

    public string Name => "AsmStringReplace";
    public int HitCount => _hitCount;

    public byte[]? Transform(string className, byte[] originalBytes)
    {
        if (className != "HelloWorld")
            return null;

        var reader = new ClassReader(originalBytes);
        var writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);
        var visitor = new ReplaceClassVisitor(Opcodes.ASM9, writer, this);
        reader.Accept(visitor, 0);
        return writer.ToByteArray();
    }

    internal string? TryReplace(string value)
    {
        if (value.Contains(_pattern))
        {
            Interlocked.Increment(ref _hitCount);
            return value.Replace(_pattern, _replacement);
        }
        return null;
    }

    private sealed class ReplaceClassVisitor : ClassVisitor
    {
        private readonly AsmStringReplaceTransformer _owner;

        public ReplaceClassVisitor(int api, ClassVisitor cv, AsmStringReplaceTransformer owner)
            : base(api, cv)
        {
            _owner = owner;
        }

        public override MethodVisitor? VisitMethod(
            int access, string? name, string? descriptor, string? signature, string[]? exceptions)
        {
            var mv = base.VisitMethod(access, name, descriptor, signature, exceptions);
            if (mv is null) return null;
            return new ReplaceMethodVisitor(Opcodes.ASM9, mv, _owner);
        }
    }

    private sealed class ReplaceMethodVisitor : MethodVisitor
    {
        private readonly AsmStringReplaceTransformer _owner;

        public ReplaceMethodVisitor(int api, MethodVisitor mv, AsmStringReplaceTransformer owner)
            : base(api, mv)
        {
            _owner = owner;
        }

        public override void VisitLdcInsn(object? value)
        {
            if (value is string s)
            {
                var replaced = _owner.TryReplace(s);
                if (replaced != null)
                {
                    base.VisitLdcInsn(replaced);
                    return;
                }
            }
            base.VisitLdcInsn(value);
        }

        public override void VisitInvokeDynamicInsn(
            string? name, string? descriptor, Handle? bootstrapMethodHandle,
            params object?[]? bootstrapMethodArguments)
        {
            // 字符串拼接（makeConcatWithConstants）的字面量在 bootstrap arguments（recipe）中，
            // 不在 LDC 指令里，需要在这里拦截替换。
            if (bootstrapMethodArguments != null)
            {
                for (int i = 0; i < bootstrapMethodArguments.Length; i++)
                {
                    if (bootstrapMethodArguments[i] is string s)
                    {
                        var replaced = _owner.TryReplace(s);
                        if (replaced != null)
                            bootstrapMethodArguments[i] = replaced;
                    }
                }
            }
            base.VisitInvokeDynamicInsn(name, descriptor, bootstrapMethodHandle, bootstrapMethodArguments);
        }
    }
}
