// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. Neither the name of the copyright holders nor the names of its
//    contributors may be used to endorse or promote products derived from
//    this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.

using Attribute = Jvm.NET.Asm.Attribute;

namespace Jvm.NET.Asm;

/// <summary>
/// A visitor to visit a Java method. The methods of this class must be called in the following
/// order: ( <c>VisitParameter</c> )* [ <c>VisitAnnotationDefault</c> ] ( <c>VisitAnnotation</c> |
/// <c>VisitAnnotableParameterCount</c> | <c>VisitParameterAnnotation</c> | <c>VisitTypeAnnotation</c>
/// | <c>VisitAttribute</c> )* [ <c>VisitCode</c> ( <c>VisitFrame</c> | <c>Visit&lt;X&gt;Insn</c> |
/// <c>VisitLabel</c> | <c>VisitInsnAnnotation</c> | <c>VisitTryCatchBlock</c> |
/// <c>VisitTryCatchAnnotation</c> | <c>VisitLocalVariable</c> |
/// <c>VisitLocalVariableAnnotation</c> | <c>VisitLineNumber</c> | <c>VisitAttribute</c> )*
/// <c>VisitMaxs</c> ] <c>VisitEnd</c>. In addition, the <c>Visit&lt;X&gt;Insn</c> and
/// <c>VisitLabel</c> methods must be called in the sequential order of the bytecode instructions of
/// the visited code, <c>VisitInsnAnnotation</c> must be called <i>after</i> the annotated
/// instruction, <c>VisitTryCatchBlock</c> must be called <i>before</i> the labels passed as arguments
/// have been visited, <c>VisitTryCatchBlockAnnotation</c> must be called <i>after</i> the
/// corresponding try catch block has been visited, and the <c>VisitLocalVariable</c>,
/// <c>VisitLocalVariableAnnotation</c> and <c>VisitLineNumber</c> methods must be called
/// <i>after</i> the labels passed as arguments have been visited. Finally, the
/// <c>VisitAttribute</c> method must be called before <c>VisitCode</c> for non-code attributes, and
/// after it for code attributes.
/// <para><b>Author:</b> Eric Bruneton</para>
/// </summary>
public abstract class MethodVisitor
{
    private const string REQUIRES_ASM5 = "This feature requires ASM5";

    /// <summary>
    /// The ASM API version implemented by this visitor. The value of this field must be one of the
    /// <c>ASM</c><i>x</i> values in <see cref="Opcodes"/>.
    /// </summary>
    protected readonly int Api;

    /// <summary>
    /// The method visitor to which this visitor must delegate method calls. May be <c>null</c>.
    /// </summary>
    protected MethodVisitor? Mv;

    /// <summary>
    /// Constructs a new <see cref="MethodVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of the
    /// <c>ASM</c><i>x</i> values in <see cref="Opcodes"/>.</param>
    protected MethodVisitor(int api)
        : this(api, null)
    {
    }

    /// <summary>
    /// Constructs a new <see cref="MethodVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of the
    /// <c>ASM</c><i>x</i> values in <see cref="Opcodes"/>.</param>
    /// <param name="methodVisitor">the method visitor to which this visitor must delegate method
    /// calls. May be <c>null</c>.</param>
    protected MethodVisitor(int api, MethodVisitor? methodVisitor)
    {
        if (api != Opcodes.ASM9
            && api != Opcodes.ASM8
            && api != Opcodes.ASM7
            && api != Opcodes.ASM6
            && api != Opcodes.ASM5
            && api != Opcodes.ASM4
            && api != Opcodes.ASM10_EXPERIMENTAL) // TODO: Opcodes.ASM10_EXPERIMENTAL
        {
            throw new ArgumentException("Unsupported api " + api);
        }
        if (api == Opcodes.ASM10_EXPERIMENTAL) // TODO: Opcodes.ASM10_EXPERIMENTAL
        {
            Constants.CheckAsmExperimental(this); // TODO: Constants
        }
        this.Api = api;
        this.Mv = methodVisitor;
    }

    /// <summary>
    /// The method visitor to which this visitor must delegate method calls. May be <c>null</c>.
    /// </summary>
    /// <returns>the method visitor to which this visitor must delegate method calls, or
    /// <c>null</c>.</returns>
    public MethodVisitor? GetDelegate()
    {
        return Mv;
    }

    // -----------------------------------------------------------------------------------------------
    // Parameters, annotations and non standard attributes
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Visits a parameter of this method.
    /// </summary>
    /// <param name="name">parameter name or <c>null</c> if none is provided.</param>
    /// <param name="access">the parameter's access flags, only <c>ACC_FINAL</c>, <c>ACC_SYNTHETIC</c>
    /// or/and <c>ACC_MANDATED</c> are allowed (see <see cref="Opcodes"/>).</param>
    public virtual void VisitParameter(string? name, int access)
    {
        if (Api < Opcodes.ASM5)
        {
            throw new NotSupportedException(REQUIRES_ASM5);
        }
        if (Mv != null)
        {
            Mv.VisitParameter(name, access);
        }
    }

    /// <summary>
    /// Visits the default value of this annotation interface method.
    /// </summary>
    /// <returns>a visitor to the visit the actual default value of this annotation interface method,
    /// or <c>null</c> if this visitor is not interested in visiting this default value. The 'name'
    /// parameters passed to the methods of this annotation visitor are ignored. Moreover, exactly
    /// one visit method must be called on this annotation visitor, followed by visitEnd.</returns>
    public virtual AnnotationVisitor? VisitAnnotationDefault()
    {
        if (Mv != null)
        {
            return Mv.VisitAnnotationDefault();
        }
        return null;
    }

    /// <summary>
    /// Visits an annotation of this method.
    /// </summary>
    /// <param name="descriptor">the class descriptor of the annotation class.</param>
    /// <param name="visible"><c>true</c> if the annotation is visible at runtime.</param>
    /// <returns>a visitor to visit the annotation values, or <c>null</c> if this visitor is not
    /// interested in visiting this annotation.</returns>
    public virtual AnnotationVisitor? VisitAnnotation(string? descriptor, bool visible)
    {
        if (Mv != null)
        {
            return Mv.VisitAnnotation(descriptor, visible);
        }
        return null;
    }

    /// <summary>
    /// Visits an annotation on a type in the method signature.
    /// </summary>
    /// <param name="typeRef">a reference to the annotated type. The sort of this type reference must
    /// be <see cref="TypeReference.METHOD_TYPE_PARAMETER"/>,
    /// <see cref="TypeReference.METHOD_TYPE_PARAMETER_BOUND"/>,
    /// <see cref="TypeReference.METHOD_RETURN"/>, <see cref="TypeReference.METHOD_RECEIVER"/>,
    /// <see cref="TypeReference.METHOD_FORMAL_PARAMETER"/> or <see cref="TypeReference.THROWS"/>.
    /// See <see cref="TypeReference"/>.</param>
    /// <param name="typePath">the path to the annotated type argument, wildcard bound, array element
    /// type, or static inner type within 'typeRef'. May be <c>null</c> if the annotation targets
    /// 'typeRef' as a whole.</param>
    /// <param name="descriptor">the class descriptor of the annotation class.</param>
    /// <param name="visible"><c>true</c> if the annotation is visible at runtime.</param>
    /// <returns>a visitor to visit the annotation values, or <c>null</c> if this visitor is not
    /// interested in visiting this annotation.</returns>
    public virtual AnnotationVisitor? VisitTypeAnnotation(
        int typeRef, TypePath? typePath, string? descriptor, bool visible) // TODO: TypePath
    {
        if (Api < Opcodes.ASM5)
        {
            throw new NotSupportedException(REQUIRES_ASM5);
        }
        if (Mv != null)
        {
            return Mv.VisitTypeAnnotation(typeRef, typePath, descriptor, visible);
        }
        return null;
    }

    /// <summary>
    /// Visits the number of method parameters that can have annotations. By default (i.e. when this
    /// method is not called), all the method parameters defined by the method descriptor can have
    /// annotations.
    /// </summary>
    /// <param name="parameterCount">the number of method parameters than can have annotations. This
    /// number must be less or equal than the number of parameter types in the method descriptor. It
    /// can be strictly less when a method has synthetic parameters and when these parameters are
    /// ignored when computing parameter indices for the purpose of parameter annotations (see
    /// https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.7.18).</param>
    /// <param name="visible"><c>true</c> to define the number of method parameters that can have
    /// annotations visible at runtime, <c>false</c> to define the number of method parameters that
    /// can have annotations invisible at runtime.</param>
    public virtual void VisitAnnotableParameterCount(int parameterCount, bool visible)
    {
        if (Mv != null)
        {
            Mv.VisitAnnotableParameterCount(parameterCount, visible);
        }
    }

    /// <summary>
    /// Visits an annotation of a parameter this method.
    /// </summary>
    /// <param name="parameter">the parameter index. This index must be strictly smaller than the number
    /// of parameters in the method descriptor, and strictly smaller than the parameter count
    /// specified in <see cref="VisitAnnotableParameterCount"/>. Important note: <i>a parameter index i
    /// is not required to correspond to the i'th parameter descriptor in the method descriptor</i>, in
    /// particular in case of synthetic parameters (see
    /// https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.7.18).</param>
    /// <param name="descriptor">the class descriptor of the annotation class.</param>
    /// <param name="visible"><c>true</c> if the annotation is visible at runtime.</param>
    /// <returns>a visitor to visit the annotation values, or <c>null</c> if this visitor is not
    /// interested in visiting this annotation.</returns>
    public virtual AnnotationVisitor? VisitParameterAnnotation(
        int parameter, string? descriptor, bool visible)
    {
        if (Mv != null)
        {
            return Mv.VisitParameterAnnotation(parameter, descriptor, visible);
        }
        return null;
    }

    /// <summary>
    /// Visits a non standard attribute of this method.
    /// </summary>
    /// <param name="attribute">an attribute.</param>
    public virtual void VisitAttribute(Attribute? attribute) // TODO: Attribute
    {
        if (Mv != null)
        {
            Mv.VisitAttribute(attribute);
        }
    }

    /// <summary>Starts the visit of the method's code, if any (i.e. non abstract method).</summary>
    public virtual void VisitCode()
    {
        if (Mv != null)
        {
            Mv.VisitCode();
        }
    }

    /// <summary>
    /// Visits the current state of the local variables and operand stack elements. This method must(*)
    /// be called <i>just before</i> any instruction <b>i</b> that follows an unconditional branch
    /// instruction such as GOTO or THROW, that is the target of a jump instruction, or that starts an
    /// exception handler block. The visited types must describe the values of the local variables and
    /// of the operand stack elements <i>just before</i> <b>i</b> is executed.
    /// <para>
    /// (*) this is mandatory only for classes whose version is greater than or equal to
    /// <see cref="Opcodes.V1_6"/>.
    /// </para>
    /// <para>
    /// The frames of a method must be given either in expanded form, or in compressed form (all frames
    /// must use the same format, i.e. you must not mix expanded and compressed frames within a single
    /// method):
    /// </para>
    /// <list type="ul">
    ///   <item>In expanded form, all frames must have the F_NEW type.</item>
    ///   <item>In compressed form, frames are basically "deltas" from the state of the previous frame:
    ///       <list type="bullet">
    ///         <item><see cref="Opcodes.F_SAME"/> representing frame with exactly the same locals as the
    ///             previous frame and with the empty stack.</item>
    ///         <item><see cref="Opcodes.F_SAME1"/> representing frame with exactly the same locals as
    ///             the previous frame and with single value on the stack ( <c>numStack</c> is 1 and
    ///             <c>stack[0]</c> contains value for the type of the stack item).</item>
    ///         <item><see cref="Opcodes.F_APPEND"/> representing frame with current locals are the same
    ///             as the locals in the previous frame, except that additional locals are defined
    ///             (<c>numLocal</c> is 1, 2 or 3 and <c>local</c> elements contains values representing
    ///             added types).</item>
    ///         <item><see cref="Opcodes.F_CHOP"/> representing frame with current locals are the same
    ///             as the locals in the previous frame, except that the last 1-3 locals are absent and
    ///             with the empty stack (<c>numLocal</c> is 1, 2 or 3).</item>
    ///         <item><see cref="Opcodes.F_FULL"/> representing complete frame data.</item>
    ///       </list>
    ///   </item>
    /// </list>
    /// <para>
    /// In both cases the first frame, corresponding to the method's parameters and access flags, is
    /// implicit and must not be visited. Also, it is illegal to visit two or more frames for the same
    /// code location (i.e., at least one instruction must be visited between two calls to visitFrame).
    /// </para>
    /// </summary>
    /// <param name="type">the type of this stack map frame. Must be <see cref="Opcodes.F_NEW"/> for
    /// expanded frames, or <see cref="Opcodes.F_FULL"/>, <see cref="Opcodes.F_APPEND"/>,
    /// <see cref="Opcodes.F_CHOP"/>, <see cref="Opcodes.F_SAME"/> or <see cref="Opcodes.F_APPEND"/>,
    /// <see cref="Opcodes.F_SAME1"/> for compressed frames.</param>
    /// <param name="numLocal">the number of local variables in the visited frame. Long and double
    /// values count for one variable.</param>
    /// <param name="local">the local variable types in this frame. This array must not be modified.
    /// Primitive types are represented by <see cref="Opcodes.TOP"/>, <see cref="Opcodes.INTEGER"/>,
    /// <see cref="Opcodes.FLOAT"/>, <see cref="Opcodes.LONG"/>, <see cref="Opcodes.DOUBLE"/>,
    /// <see cref="Opcodes.NULL"/> or <see cref="Opcodes.UNINITIALIZED_THIS"/> (long and double are
    /// represented by a single element). Reference types are represented by String objects
    /// (representing internal names, see <see cref="Type.GetInternalName()"/>), and uninitialized types
    /// by Label objects (this label designates the NEW instruction that created this uninitialized
    /// value).</param>
    /// <param name="numStack">the number of operand stack elements in the visited frame. Long and
    /// double values count for one stack element.</param>
    /// <param name="stack">the operand stack types in this frame. This array must not be modified. Its
    /// content has the same format as the "local" array.</param>
    /// <exception cref="InvalidOperationException">if a frame is visited just after another one,
    /// without any instruction between the two (unless this frame is a Opcodes#F_SAME frame, in which
    /// case it is silently ignored).</exception>
    public virtual void VisitFrame(int type, int numLocal, object?[]? local, int numStack, object?[]? stack)
    {
        if (Mv != null)
        {
            Mv.VisitFrame(type, numLocal, local, numStack, stack);
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Normal instructions
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Visits a zero operand instruction.
    /// </summary>
    /// <param name="opcode">the opcode of the instruction to be visited. This opcode is either NOP,
    /// ACONST_NULL, ICONST_M1, ICONST_0, ICONST_1, ICONST_2, ICONST_3, ICONST_4, ICONST_5, LCONST_0,
    /// LCONST_1, FCONST_0, FCONST_1, FCONST_2, DCONST_0, DCONST_1, IALOAD, LALOAD, FALOAD, DALOAD,
    /// AALOAD, BALOAD, CALOAD, SALOAD, IASTORE, LASTORE, FASTORE, DASTORE, AASTORE, BASTORE, CASTORE,
    /// SASTORE, POP, POP2, DUP, DUP_X1, DUP_X2, DUP2, DUP2_X1, DUP2_X2, SWAP, IADD, LADD, FADD, DADD,
    /// ISUB, LSUB, FSUB, DSUB, IMUL, LMUL, FMUL, DMUL, IDIV, LDIV, FDIV, DDIV, IREM, LREM, FREM, DREM,
    /// INEG, LNEG, FNEG, DNEG, ISHL, LSHL, ISHR, LSHR, IUSHR, LUSHR, IAND, LAND, IOR, LOR, IXOR, LXOR,
    /// I2L, I2F, I2D, L2I, L2F, L2D, F2I, F2L, F2D, D2I, D2L, D2F, I2B, I2C, I2S, LCMP, FCMPL, FCMPG,
    /// DCMPL, DCMPG, IRETURN, LRETURN, FRETURN, DRETURN, ARETURN, RETURN, ARRAYLENGTH, ATHROW,
    /// MONITORENTER, or MONITOREXIT.</param>
    public virtual void VisitInsn(int opcode)
    {
        if (Mv != null)
        {
            Mv.VisitInsn(opcode);
        }
    }

    /// <summary>
    /// Visits an instruction with a single int operand.
    /// </summary>
    /// <param name="opcode">the opcode of the instruction to be visited. This opcode is either BIPUSH,
    /// SIPUSH or NEWARRAY.</param>
    /// <param name="operand">the operand of the instruction to be visited.<br>
    /// When opcode is BIPUSH, operand value should be between <see cref="sbyte.MinValue"/> and
    /// <see cref="sbyte.MaxValue"/>.<br>
    /// When opcode is SIPUSH, operand value should be between <see cref="short.MinValue"/> and
    /// <see cref="short.MaxValue"/>.<br>
    /// When opcode is NEWARRAY, operand value should be one of <see cref="Opcodes.T_BOOLEAN"/>,
    /// <see cref="Opcodes.T_CHAR"/>, <see cref="Opcodes.T_FLOAT"/>, <see cref="Opcodes.T_DOUBLE"/>,
    /// <see cref="Opcodes.T_BYTE"/>, <see cref="Opcodes.T_SHORT"/>, <see cref="Opcodes.T_INT"/> or
    /// <see cref="Opcodes.T_LONG"/>.</param>
    public virtual void VisitIntInsn(int opcode, int operand)
    {
        if (Mv != null)
        {
            Mv.VisitIntInsn(opcode, operand);
        }
    }

    /// <summary>
    /// Visits a local variable instruction. A local variable instruction is an instruction that loads
    /// or stores the value of a local variable.
    /// </summary>
    /// <param name="opcode">the opcode of the local variable instruction to be visited. This opcode is
    /// either ILOAD, LLOAD, FLOAD, DLOAD, ALOAD, ISTORE, LSTORE, FSTORE, DSTORE, ASTORE or RET.</param>
    /// <param name="varIndex">the operand of the instruction to be visited. This operand is the index
    /// of a local variable.</param>
    public virtual void VisitVarInsn(int opcode, int varIndex)
    {
        if (Mv != null)
        {
            Mv.VisitVarInsn(opcode, varIndex);
        }
    }

    /// <summary>
    /// Visits a type instruction. A type instruction is an instruction that takes the internal name of
    /// a class as parameter (see <see cref="Type.GetInternalName()"/>).
    /// </summary>
    /// <param name="opcode">the opcode of the type instruction to be visited. This opcode is either
    /// NEW, ANEWARRAY, CHECKCAST or INSTANCEOF.</param>
    /// <param name="type">the operand of the instruction to be visited. This operand must be the
    /// internal name of an object or array class (see <see cref="Type.GetInternalName()"/>).</param>
    public virtual void VisitTypeInsn(int opcode, string? type)
    {
        if (Mv != null)
        {
            Mv.VisitTypeInsn(opcode, type);
        }
    }

    /// <summary>
    /// Visits a field instruction. A field instruction is an instruction that loads or stores the
    /// value of a field of an object.
    /// </summary>
    /// <param name="opcode">the opcode of the type instruction to be visited. This opcode is either
    /// GETSTATIC, PUTSTATIC, GETFIELD or PUTFIELD.</param>
    /// <param name="owner">the internal name of the field's owner class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="name">the field's name.</param>
    /// <param name="descriptor">the field's descriptor (see <see cref="Type"/>).</param>
    public virtual void VisitFieldInsn(int opcode, string? owner, string? name, string? descriptor)
    {
        if (Mv != null)
        {
            Mv.VisitFieldInsn(opcode, owner, name, descriptor);
        }
    }

    /// <summary>
    /// Visits a method instruction. A method instruction is an instruction that invokes a method.
    /// </summary>
    /// <param name="opcode">the opcode of the type instruction to be visited. This opcode is either
    /// INVOKEVIRTUAL, INVOKESPECIAL, INVOKESTATIC or INVOKEINTERFACE.</param>
    /// <param name="owner">the internal name of the method's owner class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="name">the method's name.</param>
    /// <param name="descriptor">the method's descriptor (see <see cref="Type"/>).</param>
    [Obsolete("use VisitMethodInsn(int, string, string, string, bool) instead.")]
    public virtual void VisitMethodInsn(int opcode, string? owner, string? name, string? descriptor)
    {
        int opcodeAndSource = opcode | (Api < Opcodes.ASM5 ? Opcodes.SOURCE_DEPRECATED : 0);
#pragma warning disable CS0618
        VisitMethodInsn(opcodeAndSource, owner, name, descriptor, opcode == Opcodes.INVOKEINTERFACE);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Visits a method instruction. A method instruction is an instruction that invokes a method.
    /// </summary>
    /// <param name="opcode">the opcode of the type instruction to be visited. This opcode is either
    /// INVOKEVIRTUAL, INVOKESPECIAL, INVOKESTATIC or INVOKEINTERFACE.</param>
    /// <param name="owner">the internal name of the method's owner class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="name">the method's name.</param>
    /// <param name="descriptor">the method's descriptor (see <see cref="Type"/>).</param>
    /// <param name="isInterface">if the method's owner class is an interface.</param>
    public virtual void VisitMethodInsn(
        int opcode, string? owner, string? name, string? descriptor, bool isInterface)
    {
        if (Api < Opcodes.ASM5 && (opcode & Opcodes.SOURCE_DEPRECATED) == 0)
        {
            if (isInterface != (opcode == Opcodes.INVOKEINTERFACE))
            {
                throw new NotSupportedException("INVOKESPECIAL/STATIC on interfaces requires ASM5");
            }
#pragma warning disable CS0618
            VisitMethodInsn(opcode, owner, name, descriptor);
#pragma warning restore CS0618
            return;
        }
        if (Mv != null)
        {
            Mv.VisitMethodInsn(opcode & ~Opcodes.SOURCE_MASK, owner, name, descriptor, isInterface);
        }
    }

    /// <summary>
    /// Visits an invokedynamic instruction.
    /// </summary>
    /// <param name="name">the method's name.</param>
    /// <param name="descriptor">the method's descriptor (see <see cref="Type"/>).</param>
    /// <param name="bootstrapMethodHandle">the bootstrap method.</param>
    /// <param name="bootstrapMethodArguments">the bootstrap method constant arguments. Each argument
    /// must be an <see cref="int"/>, <see cref="float"/>, <see cref="long"/>, <see cref="double"/>,
    /// <see cref="string"/>, <see cref="Type"/>, <see cref="Handle"/> or
    /// <see cref="ConstantDynamic"/> value. This method is allowed to modify the content of the array
    /// so a caller should expect that this array may change.</param>
    public virtual void VisitInvokeDynamicInsn(
        string? name,
        string? descriptor,
        Handle? bootstrapMethodHandle, // TODO: Handle
        params object?[]? bootstrapMethodArguments)
    {
        if (Api < Opcodes.ASM5)
        {
            throw new NotSupportedException(REQUIRES_ASM5);
        }
        if (Mv != null)
        {
            Mv.VisitInvokeDynamicInsn(name, descriptor, bootstrapMethodHandle, bootstrapMethodArguments);
        }
    }

    /// <summary>
    /// Visits a jump instruction. A jump instruction is an instruction that may jump to another
    /// instruction.
    /// </summary>
    /// <param name="opcode">the opcode of the type instruction to be visited. This opcode is either
    /// IFEQ, IFNE, IFLT, IFGE, IFGT, IFLE, IF_ICMPEQ, IF_ICMPNE, IF_ICMPLT, IF_ICMPGE, IF_ICMPGT,
    /// IF_ICMPLE, IF_ACMPEQ, IF_ACMPNE, GOTO, JSR, IFNULL or IFNONNULL.</param>
    /// <param name="label">the operand of the instruction to be visited. This operand is a label that
    /// designates the instruction to which the jump instruction may jump.</param>
    public virtual void VisitJumpInsn(int opcode, Label label)
    {
        if (Mv != null)
        {
            Mv.VisitJumpInsn(opcode, label);
        }
    }

    /// <summary>
    /// Visits a label. A label designates the instruction that will be visited just after it.
    /// </summary>
    /// <param name="label">a <see cref="Label"/> object.</param>
    public virtual void VisitLabel(Label label)
    {
        if (Mv != null)
        {
            Mv.VisitLabel(label);
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Special instructions
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Visits a LDC instruction. Note that new constant types may be added in future versions of the
    /// Java Virtual Machine. To easily detect new constant types, implementations of this method
    /// should check for unexpected constant types, like this:
    /// <code>
    /// if (cst is int) {
    ///     // ...
    /// } else if (cst is float) {
    ///     // ...
    /// } else if (cst is long) {
    ///     // ...
    /// } else if (cst is double) {
    ///     // ...
    /// } else if (cst is string) {
    ///     // ...
    /// } else if (cst is Type t) {
    ///     int sort = t.GetSort();
    ///     if (sort == Type.OBJECT) {
    ///         // ...
    ///     } else if (sort == Type.ARRAY) {
    ///         // ...
    ///     } else if (sort == Type.METHOD) {
    ///         // ...
    ///     } else {
    ///         // throw an exception
    ///     }
    /// } else if (cst is Handle) {
    ///     // ...
    /// } else if (cst is ConstantDynamic) {
    ///     // ...
    /// } else {
    ///     // throw an exception
    /// }
    /// </code>
    /// </summary>
    /// <param name="value">the constant to be loaded on the stack. This parameter must be a non null
    /// <see cref="int"/>, a <see cref="float"/>, a <see cref="long"/>, a <see cref="double"/>,
    /// a <see cref="string"/>, a <see cref="Type"/> of OBJECT or ARRAY sort for <c>.class</c>
    /// constants, for classes whose version is 49, a <see cref="Type"/> of METHOD sort for MethodType,
    /// a <see cref="Handle"/> for MethodHandle constants, for classes whose version is 51 or
    /// a <see cref="ConstantDynamic"/> for a constant dynamic for classes whose version is 55.</param>
    public virtual void VisitLdcInsn(object? value)
    {
        if (Api < Opcodes.ASM5
            && (value is Handle // TODO: Handle
                || (value is Type type && type.GetSort() == Type.METHOD)))
        {
            throw new NotSupportedException(REQUIRES_ASM5);
        }
        if (Api < Opcodes.ASM7 && value is ConstantDynamic) // TODO: ConstantDynamic
        {
            throw new NotSupportedException("This feature requires ASM7");
        }
        if (Mv != null)
        {
            Mv.VisitLdcInsn(value);
        }
    }

    /// <summary>
    /// Visits an IINC instruction.
    /// </summary>
    /// <param name="varIndex">index of the local variable to be incremented.</param>
    /// <param name="increment">amount to increment the local variable by.</param>
    public virtual void VisitIincInsn(int varIndex, int increment)
    {
        if (Mv != null)
        {
            Mv.VisitIincInsn(varIndex, increment);
        }
    }

    /// <summary>
    /// Visits a TABLESWITCH instruction.
    /// </summary>
    /// <param name="min">the minimum key value.</param>
    /// <param name="max">the maximum key value.</param>
    /// <param name="dflt">beginning of the default handler block.</param>
    /// <param name="labels">beginnings of the handler blocks. <c>labels[i]</c> is the beginning of the
    /// handler block for the <c>min + i</c> key.</param>
    public virtual void VisitTableSwitchInsn(int min, int max, Label dflt, params Label[]? labels)
    {
        if (Mv != null)
        {
            Mv.VisitTableSwitchInsn(min, max, dflt, labels);
        }
    }

    /// <summary>
    /// Visits a LOOKUPSWITCH instruction.
    /// </summary>
    /// <param name="dflt">beginning of the default handler block.</param>
    /// <param name="keys">the values of the keys. Keys must be sorted in increasing order.</param>
    /// <param name="labels">beginnings of the handler blocks. <c>labels[i]</c> is the beginning of the
    /// handler block for the <c>keys[i]</c> key.</param>
    public virtual void VisitLookupSwitchInsn(Label dflt, int[]? keys, Label[]? labels)
    {
        if (Mv != null)
        {
            Mv.VisitLookupSwitchInsn(dflt, keys, labels);
        }
    }

    /// <summary>
    /// Visits a MULTIANEWARRAY instruction.
    /// </summary>
    /// <param name="descriptor">an array type descriptor (see <see cref="Type"/>).</param>
    /// <param name="numDimensions">the number of dimensions of the array to allocate.</param>
    public virtual void VisitMultiANewArrayInsn(string? descriptor, int numDimensions)
    {
        if (Mv != null)
        {
            Mv.VisitMultiANewArrayInsn(descriptor, numDimensions);
        }
    }

    /// <summary>
    /// Visits an annotation on an instruction. This method must be called just <i>after</i> the
    /// annotated instruction. It can be called several times for the same instruction.
    /// </summary>
    /// <param name="typeRef">a reference to the annotated type. The sort of this type reference must
    /// be <see cref="TypeReference.INSTANCEOF"/>, <see cref="TypeReference.NEW"/>,
    /// <see cref="TypeReference.CONSTRUCTOR_REFERENCE"/>, <see cref="TypeReference.METHOD_REFERENCE"/>,
    /// <see cref="TypeReference.CAST"/>,
    /// <see cref="TypeReference.CONSTRUCTOR_INVOCATION_TYPE_ARGUMENT"/>,
    /// <see cref="TypeReference.METHOD_INVOCATION_TYPE_ARGUMENT"/>,
    /// <see cref="TypeReference.CONSTRUCTOR_REFERENCE_TYPE_ARGUMENT"/>, or
    /// <see cref="TypeReference.METHOD_REFERENCE_TYPE_ARGUMENT"/>. See
    /// <see cref="TypeReference"/>.</param>
    /// <param name="typePath">the path to the annotated type argument, wildcard bound, array element
    /// type, or static inner type within 'typeRef'. May be <c>null</c> if the annotation targets
    /// 'typeRef' as a whole.</param>
    /// <param name="descriptor">the class descriptor of the annotation class.</param>
    /// <param name="visible"><c>true</c> if the annotation is visible at runtime.</param>
    /// <returns>a visitor to visit the annotation values, or <c>null</c> if this visitor is not
    /// interested in visiting this annotation.</returns>
    public virtual AnnotationVisitor? VisitInsnAnnotation(
        int typeRef, TypePath? typePath, string? descriptor, bool visible) // TODO: TypePath
    {
        if (Api < Opcodes.ASM5)
        {
            throw new NotSupportedException(REQUIRES_ASM5);
        }
        if (Mv != null)
        {
            return Mv.VisitInsnAnnotation(typeRef, typePath, descriptor, visible);
        }
        return null;
    }

    // -----------------------------------------------------------------------------------------------
    // Exceptions table entries, debug information, max stack and max locals
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Visits a try catch block.
    /// </summary>
    /// <param name="start">the beginning of the exception handler's scope (inclusive).</param>
    /// <param name="end">the end of the exception handler's scope (exclusive).</param>
    /// <param name="handler">the beginning of the exception handler's code.</param>
    /// <param name="type">the internal name of the type of exceptions handled by the handler (see
    /// <see cref="Type.GetInternalName()"/>), or <c>null</c> to catch any exceptions (for "finally"
    /// blocks).</param>
    /// <exception cref="ArgumentException">if one of the labels has already been visited by this
    /// visitor (by the <see cref="VisitLabel"/> method).</exception>
    public virtual void VisitTryCatchBlock(Label start, Label end, Label handler, string? type)
    {
        if (Mv != null)
        {
            Mv.VisitTryCatchBlock(start, end, handler, type);
        }
    }

    /// <summary>
    /// Visits an annotation on an exception handler type. This method must be called <i>after</i> the
    /// <see cref="VisitTryCatchBlock"/> for the annotated exception handler. It can be called several
    /// times for the same exception handler.
    /// </summary>
    /// <param name="typeRef">a reference to the annotated type. The sort of this type reference must
    /// be <see cref="TypeReference.EXCEPTION_PARAMETER"/>. See <see cref="TypeReference"/>.</param>
    /// <param name="typePath">the path to the annotated type argument, wildcard bound, array element
    /// type, or static inner type within 'typeRef'. May be <c>null</c> if the annotation targets
    /// 'typeRef' as a whole.</param>
    /// <param name="descriptor">the class descriptor of the annotation class.</param>
    /// <param name="visible"><c>true</c> if the annotation is visible at runtime.</param>
    /// <returns>a visitor to visit the annotation values, or <c>null</c> if this visitor is not
    /// interested in visiting this annotation.</returns>
    public virtual AnnotationVisitor? VisitTryCatchAnnotation(
        int typeRef, TypePath? typePath, string? descriptor, bool visible) // TODO: TypePath
    {
        if (Api < Opcodes.ASM5)
        {
            throw new NotSupportedException(REQUIRES_ASM5);
        }
        if (Mv != null)
        {
            return Mv.VisitTryCatchAnnotation(typeRef, typePath, descriptor, visible);
        }
        return null;
    }

    /// <summary>
    /// Visits a local variable declaration.
    /// </summary>
    /// <param name="name">the name of a local variable.</param>
    /// <param name="descriptor">the type descriptor of this local variable.</param>
    /// <param name="signature">the type signature of this local variable. May be <c>null</c> if the
    /// local variable type does not use generic types.</param>
    /// <param name="start">the first instruction corresponding to the scope of this local variable
    /// (inclusive).</param>
    /// <param name="end">the last instruction corresponding to the scope of this local variable
    /// (exclusive).</param>
    /// <param name="index">the local variable's index.</param>
    /// <exception cref="ArgumentException">if one of the labels has not already been visited by this
    /// visitor (by the <see cref="VisitLabel"/> method).</exception>
    public virtual void VisitLocalVariable(
        string? name,
        string? descriptor,
        string? signature,
        Label start,
        Label end,
        int index)
    {
        if (Mv != null)
        {
            Mv.VisitLocalVariable(name, descriptor, signature, start, end, index);
        }
    }

    /// <summary>
    /// Visits an annotation on a local variable type.
    /// </summary>
    /// <param name="typeRef">a reference to the annotated type. The sort of this type reference must
    /// be <see cref="TypeReference.LOCAL_VARIABLE"/> or
    /// <see cref="TypeReference.RESOURCE_VARIABLE"/>. See <see cref="TypeReference"/>.</param>
    /// <param name="typePath">the path to the annotated type argument, wildcard bound, array element
    /// type, or static inner type within 'typeRef'. May be <c>null</c> if the annotation targets
    /// 'typeRef' as a whole.</param>
    /// <param name="start">the fist instructions corresponding to the continuous ranges that make the
    /// scope of this local variable (inclusive).</param>
    /// <param name="end">the last instructions corresponding to the continuous ranges that make the
    /// scope of this local variable (exclusive). This array must have the same size as the 'start'
    /// array.</param>
    /// <param name="index">the local variable's index in each range. This array must have the same
    /// size as the 'start' array.</param>
    /// <param name="descriptor">the class descriptor of the annotation class.</param>
    /// <param name="visible"><c>true</c> if the annotation is visible at runtime.</param>
    /// <returns>a visitor to visit the annotation values, or <c>null</c> if this visitor is not
    /// interested in visiting this annotation.</returns>
    public virtual AnnotationVisitor? VisitLocalVariableAnnotation(
        int typeRef,
        TypePath? typePath, // TODO: TypePath
        Label[]? start,
        Label[]? end,
        int[]? index,
        string? descriptor,
        bool visible)
    {
        if (Api < Opcodes.ASM5)
        {
            throw new NotSupportedException(REQUIRES_ASM5);
        }
        if (Mv != null)
        {
            return Mv.VisitLocalVariableAnnotation(typeRef, typePath, start, end, index, descriptor, visible);
        }
        return null;
    }

    /// <summary>
    /// Visits a line number declaration.
    /// </summary>
    /// <param name="line">a line number. This number refers to the source file from which the class
    /// was compiled.</param>
    /// <param name="start">the first instruction corresponding to this line number.</param>
    /// <exception cref="ArgumentException">if <c>start</c> has not already been visited by this
    /// visitor (by the <see cref="VisitLabel"/> method).</exception>
    public virtual void VisitLineNumber(int line, Label start)
    {
        if (Mv != null)
        {
            Mv.VisitLineNumber(line, start);
        }
    }

    /// <summary>
    /// Visits the maximum stack size and the maximum number of local variables of the method.
    /// </summary>
    /// <param name="maxStack">maximum stack size of the method.</param>
    /// <param name="maxLocals">maximum number of local variables for the method.</param>
    public virtual void VisitMaxs(int maxStack, int maxLocals)
    {
        if (Mv != null)
        {
            Mv.VisitMaxs(maxStack, maxLocals);
        }
    }

    /// <summary>
    /// Visits the end of the method. This method, which is the last one to be called, is used to
    /// inform the visitor that all the annotations and attributes of the method have been visited.
    /// </summary>
    public virtual void VisitEnd()
    {
        if (Mv != null)
        {
            Mv.VisitEnd();
        }
    }
}
