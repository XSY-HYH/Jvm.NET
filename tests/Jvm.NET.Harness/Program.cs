using System.IO.Compression;
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
    Version                    = 21,
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

// 9) RedefineClasses 热替换：运行时直接替换已加载类的字节码
// 与 ClassFileLoadHook（加载时）不同，RedefineClasses 可以对已经加载的类热替换方法体。
// 这里把 Target.compute 的方法体从 `a*b+42` 改成 `return 999`。
Console.WriteLine("=== Test 9: RedefineClasses (hot-swap Target.compute) ===");
invoker.LoadJar(jarPath);
var targetForRedefine = invoker.LoadClass("Target");
var beforeResult = invoker.InvokeStatic(targetForRedefine, "compute", "(II)I",
    JvmValue.FromInt(6), JvmValue.FromInt(7));
Console.WriteLine($"    Before redefine: compute(6, 7) = {beforeResult.AsInt()} (expected 84)");

var targetOriginalBytes = ExtractClassFromJar(jarPath, "Target.class");
var redefinedBytes = OverrideMethodBody(targetOriginalBytes, "compute", "(II)I", mv =>
{
    mv.VisitIntInsn(Opcodes.SIPUSH, 999);   // sipush 999
    mv.VisitInsn(Opcodes.IRETURN);           // ireturn
}, maxStack: 1, maxLocals: 2);

runtime.BytecodeModifier.RedefineClasses(
    new[] { new KeyValuePair<JvmClass, byte[]>(targetForRedefine, redefinedBytes) });

var afterResult = invoker.InvokeStatic(targetForRedefine, "compute", "(II)I",
    JvmValue.FromInt(6), JvmValue.FromInt(7));
Console.WriteLine($"    After redefine:  compute(6, 7) = {afterResult.AsInt()} (expected 999)");
Console.WriteLine();

// 10) RetransformClasses：对已加载的类重新触发 ClassFileLoadHook
// 先加载类（无 transformer），再注册 transformer，再调用 RetransformClasses 让 transformer 重新处理。
Console.WriteLine("=== Test 10: RetransformClasses (re-trigger hook on loaded class) ===");
invoker.LoadJar(jarPath);
var targetForRetransform = invoker.LoadClass("Target");
Console.WriteLine($"    Target loaded (no transformer at load time).");

var recordingTransformer = new RecordingTransformer("Target");
using (runtime.BytecodeModifier.RegisterTransformer(recordingTransformer))
{
    Console.WriteLine($"    Before retransform: hit count = {recordingTransformer.HitCount}");
    runtime.BytecodeModifier.RetransformClasses(new[] { targetForRetransform });
    Console.WriteLine($"    After retransform:  hit count = {recordingTransformer.HitCount} (expected >= 1)");
}
Console.WriteLine();

// 11) 多 transformer 叠加：两个 transformer 同时注册，验证协作不冲突
// transformerA（ASM 不等长）替换 "Hello from Java!" -> "HACKED by A"
// transformerB（等长）替换 "Args count" -> "Hax count!"（都是 10 字符）
Console.WriteLine("=== Test 11: Multiple transformers stacked ===");
Console.WriteLine("    transformerA: \"Hello from Java!\" -> \"HACKED by A\" (ASM, unequal length)");
Console.WriteLine("    transformerB: \"Args count\"      -> \"Hax count!\"  (equal length)");
var transformerA = new AsmStringReplaceTransformer("Hello from Java!", "HACKED by A");
var transformerB = new StringReplaceTransformer("Args count", "Hax count!");
using (runtime.BytecodeModifier.RegisterTransformer(transformerA))
using (runtime.BytecodeModifier.RegisterTransformer(transformerB))
{
    invoker.RunMain(jarPath, "HelloWorld", "multi-transform");
}
Console.WriteLine($"    transformerA hit count: {transformerA.HitCount} (expected >= 1)");
Console.WriteLine($"    transformerB hit count: {transformerB.HitCount} (expected >= 1)");
Console.WriteLine();

// 12) 覆盖 main 方法体：用 RedefineClasses 把 HelloWorld.main 改成打印固定字符串
// 注意：不能用 RunMain（会创建新 ClassLoader 导致 redefine 无效），
// 改用 LoadJar + LoadClass + RedefineClasses + InvokeStatic main。
Console.WriteLine("=== Test 12: RedefineClasses (override main method body) ===");
invoker.LoadJar(jarPath);
var helloForOverride = invoker.LoadClass("HelloWorld");

var helloOriginalBytes = ExtractClassFromJar(jarPath, "HelloWorld.class");
var overriddenHelloBytes = OverrideMethodBody(helloOriginalBytes, "main", "([Ljava/lang/String;)V", mv =>
{
    // 等价 Java: System.out.println("PWNED MAIN");
    mv.VisitFieldInsn(Opcodes.GETSTATIC, "java/lang/System", "out", "Ljava/io/PrintStream;");
    mv.VisitLdcInsn("PWNED MAIN");
    mv.VisitMethodInsn(Opcodes.INVOKEVIRTUAL, "java/io/PrintStream", "println", "(Ljava/lang/String;)V");
    mv.VisitInsn(Opcodes.RETURN);
}, maxStack: 2, maxLocals: 1);

runtime.BytecodeModifier.RedefineClasses(
    new[] { new KeyValuePair<JvmClass, byte[]>(helloForOverride, overriddenHelloBytes) });

Console.WriteLine("    Calling main after override (expect 'PWNED MAIN')...");
var mainArgs = invoker.NewStringArray(Array.Empty<string>());
invoker.InvokeStatic(helloForOverride, "main", "([Ljava/lang/String;)V", mainArgs);
Console.WriteLine();

// 13) 互操作：字段访问（实例字段 + 静态字段）
Console.WriteLine("=== Test 13: Field access (instance + static) ===");
invoker.LoadJar(jarPath);
var interopClass = invoker.LoadClass("InteropTarget");
var interopObj = invoker.NewObject(interopClass, "(ILjava/lang/String;)V",
    JvmValue.FromInt(42), invoker.NewString("test-instance"));
var fIntValue = invoker.GetField(interopObj, "intValue", "I").AsInt();
var fNameValue = invoker.GetString(invoker.GetField(interopObj, "name", "Ljava/lang/String;").ObjectHandle);
Console.WriteLine($"    intValue={fIntValue} (expected 42), name=\"{fNameValue}\" (expected \"test-instance\")");
invoker.SetField(interopObj, "intValue", "I", JvmValue.FromInt(100));
var fNewInt = invoker.GetField(interopObj, "intValue", "I").AsInt();
Console.WriteLine($"    After SetField: intValue={fNewInt} (expected 100)");
var fVersion = invoker.GetString(invoker.GetStaticField(interopClass, "VERSION", "Ljava/lang/String;").ObjectHandle);
var fTotal = invoker.GetStaticField(interopClass, "s_total", "J").AsLong();
Console.WriteLine($"    Static VERSION=\"{fVersion}\", s_total={fTotal} (expected >= 42)");
Console.WriteLine();

// 14) 互操作：数组操作（基本类型数组 + 对象数组）
Console.WriteLine("=== Test 14: Array operations (primitive + object arrays) ===");
var arrVal = invoker.InvokeVirtual(interopObj, "makeIntArray", "(II)[I",
    JvmValue.FromInt(5), JvmValue.FromInt(7));
var arrLen = invoker.GetArrayLength(arrVal);
var arrVals = invoker.GetArrayValues<int>(arrVal);
Console.WriteLine($"    makeIntArray(5,7): length={arrLen}, values=[{string.Join(",", arrVals)}] (expected 7,7,7,7,7)");
var newArr = invoker.NewArray(new int[] { 10, 20, 30 });
var newVals = invoker.GetArrayValues<int>(newArr);
Console.WriteLine($"    NewArray: values=[{string.Join(",", newVals)}] (expected 10,20,30)");
var strArrVal = invoker.InvokeVirtual(interopObj, "makeStringArray", "()[Ljava/lang/String;");
var strArrLen = invoker.GetArrayLength(strArrVal);
Console.WriteLine($"    makeStringArray: length={strArrLen} (expected 3)");
for (int i = 0; i < strArrLen; i++)
{
    var elem = invoker.GetObjectArrayElement(strArrVal, i);
    var s = invoker.GetString(elem.ObjectHandle);
    Console.WriteLine($"      [{i}] = \"{s}\"");
}
Console.WriteLine();

// 15) 互操作：Java 集合（JavaList + JavaMap）
Console.WriteLine("=== Test 15: Java collections (JavaList + JavaMap) ===");
var listVal = invoker.InvokeVirtual(interopObj, "makeList", "()Ljava/util/List;");
var listClass = invoker.LoadClass("java.util.List");
var javaList = new JavaList<string>(invoker, new JvmObject(listVal.ObjectHandle, listClass));
Console.WriteLine($"    JavaList count={javaList.Count} (expected 3)");
foreach (var item in javaList)
    Console.WriteLine($"      item: \"{item}\"");
// 测试 Add
JavaList<string> ownedList = JavaList<string>.NewArrayList(invoker);
ownedList.Add("dotnet-1");
ownedList.Add("dotnet-2");
Console.WriteLine($"    NewArrayList after Add: count={ownedList.Count} (expected 2)");
ownedList.Dispose();

var mapVal = invoker.InvokeVirtual(interopObj, "makeMap", "()Ljava/util/Map;");
var mapClass = invoker.LoadClass("java.util.Map");
var javaMap = new JavaMap<string, int>(invoker, new JvmObject(mapVal.ObjectHandle, mapClass));
Console.WriteLine($"    JavaMap count={javaMap.Count} (expected 3)");
foreach (var kv in javaMap)
    Console.WriteLine($"      {kv.Key}={kv.Value}");
javaMap.Dispose();
Console.WriteLine();

// 16) 互操作：类型检查（IsInstanceOf / IsAssignableFrom / GetSuperclass / GetObjectClass）
Console.WriteLine("=== Test 16: Type checks (IsInstanceOf / GetSuperclass) ===");
var objClass = invoker.GetObjectClass(interopObj);
Console.WriteLine($"    GetObjectClass: {objClass.Name} (expected InteropTarget)");
var stringClass = invoker.LoadClass("java.lang.String");
var isString = invoker.IsInstanceOf(interopObj, stringClass);
Console.WriteLine($"    IsInstanceOf(interopObj, String): {isString} (expected False)");
var isStrResult = invoker.InvokeStatic(interopClass, "isString", "(Ljava/lang/Object;)Z",
    invoker.NewString("hello")).AsBoolean();
Console.WriteLine($"    isString(\"hello\"): {isStrResult} (expected True)");
var super = invoker.GetSuperclass(interopClass);
Console.WriteLine($"    GetSuperclass(InteropTarget): {super?.Name ?? "<null>"} (expected java.lang.Object)");
var objSuper = invoker.LoadClass("java.lang.Object");
var assignable = invoker.IsAssignableFrom(interopClass, objSuper);
Console.WriteLine($"    IsAssignableFrom(InteropTarget, Object): {assignable} (expected True)");
Console.WriteLine();

// 17) 互操作：异常获取（GetPendingException / GetExceptionMessage / GetExceptionStackTrace）
Console.WriteLine("=== Test 17: Exception handling (GetExceptionMessage / StackTrace) ===");
try
{
    invoker.InvokeStatic(interopClass, "throwNamed", "(Ljava/lang/String;)V",
        invoker.NewString("interop-exception-test"));
}
catch (JvmException ex)
{
    Console.WriteLine($"    JvmException caught: {ex.Message}");
    var pending = invoker.GetPendingException();
    if (pending is not null)
    {
        var msg = invoker.GetExceptionMessage(pending);
        Console.WriteLine($"    GetExceptionMessage: \"{msg}\" (expected \"interop-exception-test\")");
        var trace = invoker.GetExceptionStackTrace(pending);
        var firstLine = trace.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        Console.WriteLine($"    StackTrace first line: {firstLine}");
    }
    else
    {
        Console.WriteLine("    (pending exception already cleared)");
    }
}
Console.WriteLine();

// 18) 互操作：TypeMapper 类型转换
Console.WriteLine("=== Test 18: TypeMapper conversions ===");
var tmInt = TypeMapper.FromClr(123, invoker);
Console.WriteLine($"    FromClr(123).AsInt() = {tmInt.AsInt()} (expected 123)");
var tmStr = TypeMapper.FromClr("hello-typemapper", invoker);
var tmStrBack = TypeMapper.ToString(tmStr, invoker);
Console.WriteLine($"    FromClr(\"hello-typemapper\") -> ToString = \"{tmStrBack}\"");
var tmBool = TypeMapper.FromClr(true, invoker);
Console.WriteLine($"    FromClr(true).AsBoolean() = {tmBool.AsBoolean()} (expected True)");
var tmLong = TypeMapper.FromClr(9999999999L, invoker);
Console.WriteLine($"    FromClr(9999999999L).AsLong() = {tmLong.AsLong()} (expected 9999999999)");
var tmDouble = TypeMapper.FromClr(3.14, invoker);
Console.WriteLine($"    FromClr(3.14).AsDouble() = {tmDouble.AsDouble()} (expected 3.14)");
var tmBack = TypeMapper.ToClr<int>(tmInt, invoker);
Console.WriteLine($"    ToClr<int>(123) = {tmBack} (expected 123)");
Console.WriteLine();

// 19) 互操作：JavaObject 基类 + 特性驱动封装
Console.WriteLine("=== Test 19: JavaObject base class + attribute-driven wrapper ===");
var wrapper = InteropTargetWrapper.Create(invoker, 777, "via-wrapper");
Console.WriteLine($"    Wrapper created: Value={wrapper.Value}, Name=\"{wrapper.Name}\"");
var greetResult = wrapper.Greet("world");
Console.WriteLine($"    Greet(\"world\"): \"{greetResult}\"");
var counter = wrapper.GetCounter();
Console.WriteLine($"    Static counter: {counter}");
wrapper.Dispose();
Console.WriteLine();

// 20) 互操作：对象参数传递（Java 对象作为方法参数）
Console.WriteLine("=== Test 20: Object parameter passing ===");
var objA = invoker.NewObject(interopClass, "(ILjava/lang/String;)V",
    JvmValue.FromInt(1), invoker.NewString("Alice"));
var objB = invoker.NewObject(interopClass, "(ILjava/lang/String;)V",
    JvmValue.FromInt(2), invoker.NewString("Bob"));
var greetVal = invoker.InvokeVirtual(objA, "greet", "(LInteropTarget;)Ljava/lang/String;",
    JvmValue.FromObject(objB.Handle));
var greetStr = invoker.GetString(greetVal.ObjectHandle);
Console.WriteLine($"    greet(Alice, Bob): \"{greetStr}\" (expected \"Alice greets Bob\")");
Console.WriteLine();

Console.WriteLine("[Harness] All tests completed.");

// ---- 本地函数 ----
static byte[] ExtractClassFromJar(string jarPath, string entryName)
{
    using var archive = ZipFile.OpenRead(jarPath);
    var entry = archive.GetEntry(entryName)
        ?? throw new FileNotFoundException($"Entry '{entryName}' not in jar '{jarPath}'.");
    using var stream = entry.Open();
    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    return ms.ToArray();
}

static byte[] OverrideMethodBody(byte[] originalBytes, string methodName, string methodDesc,
    Action<MethodVisitor> emitBody, int maxStack, int maxLocals)
{
    var reader = new ClassReader(originalBytes);
    var writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);
    var visitor = new BodyOverrideClassVisitor(Opcodes.ASM9, writer,
        methodName, methodDesc, emitBody, maxStack, maxLocals);
    reader.Accept(visitor, 0);
    return writer.ToByteArray();
}

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

sealed class BodyOverrideClassVisitor : ClassVisitor
{
    private readonly string _methodName;
    private readonly string _methodDesc;
    private readonly Action<MethodVisitor> _emitBody;
    private readonly int _maxStack;
    private readonly int _maxLocals;

    public BodyOverrideClassVisitor(int api, ClassVisitor cv, string methodName, string methodDesc,
        Action<MethodVisitor> emitBody, int maxStack, int maxLocals)
        : base(api, cv)
    {
        _methodName = methodName;
        _methodDesc = methodDesc;
        _emitBody = emitBody;
        _maxStack = maxStack;
        _maxLocals = maxLocals;
    }

    public override MethodVisitor? VisitMethod(int access, string? name, string? descriptor,
        string? signature, string[]? exceptions)
    {
        if (name == _methodName && descriptor == _methodDesc)
        {
            // 直接生成新方法体，返回 null 让 ClassReader 跳过原始方法体解析
            var mv = Cv!.VisitMethod(access, name, descriptor, signature, exceptions);
            if (mv != null)
            {
                mv.VisitCode();
                _emitBody(mv);
                mv.VisitMaxs(_maxStack, _maxLocals);
                mv.VisitEnd();
            }
            return null;
        }
        return base.VisitMethod(access, name, descriptor, signature, exceptions);
    }
}

// ---- 记录型 transformer（用于 RetransformClasses 测试）----
// 不修改字节码（返回 null），只记录 Transform 被调用的次数。
sealed class RecordingTransformer : IBytecodeTransformer
{
    private readonly string _targetClass;
    private int _hitCount;

    public RecordingTransformer(string targetClass) => _targetClass = targetClass;
    public string Name => "Recording";
    public int HitCount => _hitCount;

    public byte[]? Transform(string className, byte[] originalBytes)
    {
        if (className == _targetClass)
            Interlocked.Increment(ref _hitCount);
        return null;
    }
}

// ---- JavaObject 基类 + 特性驱动封装（Test 19 用）----
[JavaClass("InteropTarget")]
sealed class InteropTargetWrapper : JavaObject
{
    public static InteropTargetWrapper Create(IJvmInvoker invoker, int value, string name)
        => Create<InteropTargetWrapper>(invoker, "(ILjava/lang/String;)V",
            JvmValue.FromInt(value), invoker.NewString(name));

    public int Value => GetField<int>("intValue", "I");

    public string Name
    {
        get
        {
            var v = GetField("name", "Ljava/lang/String;");
            return Invoker.GetString(v.ObjectHandle) ?? "";
        }
    }

    public string Greet(string otherName)
    {
        // 构造另一个 InteropTarget 作为参数
        var other = Create(Invoker, 0, otherName);
        try
        {
            var result = Invoke("greet", "(LInteropTarget;)Ljava/lang/String;",
                JvmValue.FromObject(other.Handle.Handle));
            return Invoker.GetString(result.ObjectHandle) ?? "";
        }
        finally
        {
            other.Dispose();
        }
    }

    public int GetCounter()
    {
        var v = InvokeStatic("getCounter", "()I");
        return v.AsInt();
    }
}
