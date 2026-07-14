using Jvm.NET.Asm;
using Type = Jvm.NET.Asm.Type;

namespace Jvm.NET.Abstractions.Jdk21;

/// <summary>
/// 方案 B 的字节码插桩器：实现 <see cref="IBytecodeTransformer"/>，
/// 在目标方法入口插入 <c>invokestatic JnBridge.onMethodEntry</c>，
/// 在每个 return 指令前插入 <c>invokestatic JnBridge.onMethodExit</c>。
///
/// 只插桩非核心类（跳过 java/ javax/ sun/ jdk/ com/xsy/jn/ 前缀），
/// 且跳过 abstract / native 方法（它们没有方法体）。
///
/// 插桩开关：当 <see cref="Enabled"/> 为 false 时不做任何变换（返回 null），
/// 由 EventListener 在有订阅者时置 true，无订阅者时置 false。
/// </summary>
internal sealed class MethodEventInstrumentor : IBytecodeTransformer
{
    string IBytecodeTransformer.Name => nameof(MethodEventInstrumentor);

    /// <summary>
    /// 插桩总开关。设为 true 时对所有后续类加载进行插桩；
    /// 设为 false 时直接放行。线程安全：用 volatile 保证可见性。
    /// </summary>
    public volatile bool Enabled;

    byte[]? IBytecodeTransformer.Transform(string className, byte[] originalBytes)
    {
        if (!Enabled) return null;
        if (ShouldSkip(className)) return null;
        if (originalBytes is null || originalBytes.Length == 0) return null;

        try
        {
            var reader = new ClassReader(originalBytes);
            // COMPUTE_MAXS：让 ClassWriter 自动计算 max_stack / max_locals，
            // 因为插桩后栈深度变了，原始值不可信。
            var writer = new ClassWriter(ClassWriter.COMPUTE_MAXS);
            var visitor = new InstrumentingClassVisitor(Opcodes.ASM9, writer, className);
            reader.Accept(visitor, 0);
            return writer.ToByteArray();
        }
        catch
        {
            // 插桩失败不应中断类加载，返回 null 让 JVM 用原始字节码。
            return null;
        }
    }

    /// <summary>
    /// 跳过核心库和 JnBridge 自身。JnBridge 的方法不能被插桩，否则会无限递归。
    /// </summary>
    private static bool ShouldSkip(string className)
    {
        if (string.IsNullOrEmpty(className)) return true;
        return className.StartsWith("java/", StringComparison.Ordinal)
            || className.StartsWith("javax/", StringComparison.Ordinal)
            || className.StartsWith("sun/", StringComparison.Ordinal)
            || className.StartsWith("jdk/", StringComparison.Ordinal)
            || className.StartsWith("com/xsy/jn/", StringComparison.Ordinal)
            || className.StartsWith("[", StringComparison.Ordinal);  // 数组类型
    }

    // ---- 自定义 ClassVisitor：包装 ClassWriter，拦截 VisitMethod ----

    private sealed class InstrumentingClassVisitor : ClassVisitor
    {
        private readonly string _className;

        public InstrumentingClassVisitor(int api, ClassVisitor cv, string className)
            : base(api, cv)
        {
            _className = className;
        }

        public override MethodVisitor? VisitMethod(
            int access, string? name, string? descriptor, string? signature, string[]? exceptions)
        {
            var mv = base.VisitMethod(access, name, descriptor, signature, exceptions);
            if (mv is null) return null;

            // 跳过 abstract 和 native 方法（无方法体）
            if ((access & Opcodes.ACC_ABSTRACT) != 0) return mv;
            if ((access & Opcodes.ACC_NATIVE) != 0) return mv;

            return new InstrumentingMethodVisitor(Opcodes.ASM9, mv, _className, name ?? "<unknown>", access, descriptor ?? "()V");
        }
    }

    // ---- 自定义 MethodVisitor：入口/出口/异常插桩 ----

    private sealed class InstrumentingMethodVisitor : MethodVisitor
    {
        private readonly string _className;
        private readonly string _methodName;
        private readonly int _access;
        private readonly string _descriptor;
        private readonly bool _isConstructor;

        // try-catch 块的三个 label：start 覆盖整个方法体，handler 处理异常
        private readonly Label _startLabel = new();
        private readonly Label _endLabel = new();
        private readonly Label _handlerLabel = new();

        // 跟踪方法体中使用的最大局部变量 index，用于分配 catch handler 的临时 slot
        private int _maxLocalIndex = -1;

        public InstrumentingMethodVisitor(int api, MethodVisitor mv, string className, string methodName, int access, string descriptor)
            : base(api, mv)
        {
            _className = className;
            _methodName = methodName;
            _access = access;
            _descriptor = descriptor;
            _isConstructor = methodName == "<init>";
        }

        public override void VisitCode()
        {
            // try 块从方法开始覆盖到所有 return 指令
            Mv!.VisitLabel(_startLabel);
            // 方法入口：插入 onMethodEntry(className, methodName)
            EmitCallback("onMethodEntry");
            base.VisitCode();
        }

        public override void VisitInsn(int opcode)
        {
            // 在每个 return 指令前插入 onMethodExit(className, methodName)
            if (IsReturn(opcode))
            {
                EmitCallback("onMethodExit");
            }
            base.VisitInsn(opcode);
        }

        public override void VisitVarInsn(int opcode, int varIndex)
        {
            if (varIndex > _maxLocalIndex)
                _maxLocalIndex = varIndex;
            base.VisitVarInsn(opcode, varIndex);
        }

        public override void VisitMaxs(int maxStack, int maxLocals)
        {
            // 构造函数 <init> 跳过 try-catch 插桩：
            // 构造函数中 this 在 super.<init>() 调用前是 uninitializedThis，
            // try-catch 跨越 super.<init>() 会导致 stackmap frame 类型不匹配（uninitializedThis vs Target）。
            // MethodEntry/MethodExit 插桩不受影响（只加 invokestatic，不改栈布局）。
            if (!_isConstructor)
            {
                int exceptionLocal = _maxLocalIndex + 1;

                // try 块结束
                Mv!.VisitLabel(_endLabel);
                // 声明 try-catch：catch Throwable（type=null 表示 catch all）
                Mv!.VisitTryCatchBlock(_startLabel, _endLabel, _handlerLabel, null);
                // catch handler 开始
                Mv!.VisitLabel(_handlerLabel);

                // catch handler 入口必须有一个 stackmap frame，否则 JVM 验证器报 VerifyError。
                // frame 描述：栈顶 1 个 Throwable，局部变量表与方法入口相同（handler 可从方法任意位置跳转过来）。
                // COMPUTE_MAXS 不自动生成 frame，需要手动调用 VisitFrame。
                var entryLocals = BuildEntryLocals();
                Mv!.VisitFrame(
                    Opcodes.F_NEW,
                    entryLocals.Count, entryLocals.ToArray(),
                    1, new object[] { "java/lang/Throwable" });

                // 异常对象在栈顶，存到临时局部变量
                Mv!.VisitVarInsn(Opcodes.ASTORE, exceptionLocal);
                // 发射 onException(className, methodName, exception) 回调
                Mv!.VisitLdcInsn(_className);
                Mv!.VisitLdcInsn(_methodName);
                Mv!.VisitVarInsn(Opcodes.ALOAD, exceptionLocal);
                Mv!.VisitMethodInsn(
                    Opcodes.INVOKESTATIC,
                    JnBridge.BridgeClassInternalName,
                    "onException",
                    "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Throwable;)V",
                    false);
                // 重新抛出异常
                Mv!.VisitVarInsn(Opcodes.ALOAD, exceptionLocal);
                Mv!.VisitInsn(Opcodes.ATHROW);
            }

            // COMPUTE_MAXS 模式下 ClassWriter 会自动计算包含 catch handler 的 max_stack/max_locals
            base.VisitMaxs(maxStack, maxLocals);
        }

        /// <summary>
        /// 构造方法入口的局部变量表（用于 catch handler 的 stackmap frame）。
        /// 实例方法 slot 0 是 this，之后按方法描述符的参数顺序填充。
        /// long/double 占 2 个 slot，第二个用 Opcodes.TOP 填充。
        /// </summary>
        private List<object> BuildEntryLocals()
        {
            var locals = new List<object>();
            // 实例方法（非 static）：slot 0 是 this，类型为当前类
            if ((_access & Opcodes.ACC_STATIC) == 0)
                locals.Add(_className);

            foreach (var argType in Type.GetArgumentTypes(_descriptor))
            {
                switch (argType.GetSort())
                {
                    case Type.BOOLEAN:
                    case Type.CHAR:
                    case Type.BYTE:
                    case Type.SHORT:
                    case Type.INT:
                        locals.Add(Opcodes.INTEGER);
                        break;
                    case Type.FLOAT:
                        locals.Add(Opcodes.FLOAT);
                        break;
                    case Type.LONG:
                        locals.Add(Opcodes.LONG);
                        locals.Add(Opcodes.TOP);
                        break;
                    case Type.DOUBLE:
                        locals.Add(Opcodes.DOUBLE);
                        locals.Add(Opcodes.TOP);
                        break;
                    case Type.ARRAY:
                        locals.Add(argType.GetDescriptor());
                        break;
                    default: // Type.OBJECT
                        locals.Add(argType.GetInternalName());
                        break;
                }
            }
            return locals;
        }

        /// <summary>
        /// 发射 invokestatic JnBridge.xxx(String, String) 指令序列。
        /// 栈影响：push 2 refs, pop 2 refs → 净变化 0。
        /// </summary>
        private void EmitCallback(string bridgeMethodName)
        {
            VisitLdcInsn(_className);
            VisitLdcInsn(_methodName);
            VisitMethodInsn(
                Opcodes.INVOKESTATIC,
                JnBridge.BridgeClassInternalName,
                bridgeMethodName,
                "(Ljava/lang/String;Ljava/lang/String;)V",
                false);
        }

        private static bool IsReturn(int opcode)
        {
            return opcode == Opcodes.IRETURN
                || opcode == Opcodes.LRETURN
                || opcode == Opcodes.FRETURN
                || opcode == Opcodes.DRETURN
                || opcode == Opcodes.ARETURN
                || opcode == Opcodes.RETURN;
        }
    }
}
