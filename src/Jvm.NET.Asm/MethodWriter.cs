// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

namespace Jvm.NET.Asm;

/// <summary>
/// A <see cref="MethodVisitor"/> that generates a corresponding 'method_info' structure, as defined in the
/// Java Virtual Machine Specification (JVMS).
/// </summary>
/// <seealso href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.6">JVMS 4.6</seealso>
internal sealed class MethodWriter : MethodVisitor
{
    /// <summary>Indicates that nothing must be computed.</summary>
    internal const int COMPUTE_NOTHING = 0;

    /// <summary>
    /// Indicates that the maximum stack size and the maximum number of local variables must be
    /// computed, from scratch.
    /// </summary>
    internal const int COMPUTE_MAX_STACK_AND_LOCAL = 1;

    /// <summary>
    /// Indicates that the maximum stack size and the maximum number of local variables must be
    /// computed, from the existing stack map frames. This can be done more efficiently than with the
    /// control flow graph algorithm used for <see cref="COMPUTE_MAX_STACK_AND_LOCAL"/>, by using a linear
    /// scan of the bytecode instructions.
    /// </summary>
    internal const int COMPUTE_MAX_STACK_AND_LOCAL_FROM_FRAMES = 2;

    /// <summary>
    /// Indicates that the stack map frames of type F_INSERT must be computed. The other frames are not
    /// computed. They should all be of type F_NEW and should be sufficient to compute the content of
    /// the F_INSERT frames, together with the bytecode instructions between a F_NEW and a F_INSERT
    /// frame - and without any knowledge of the type hierarchy (by definition of F_INSERT).
    /// </summary>
    internal const int COMPUTE_INSERTED_FRAMES = 3;

    /// <summary>
    /// Indicates that all the stack map frames must be computed. In this case the maximum stack size
    /// and the maximum number of local variables is also computed.
    /// </summary>
    internal const int COMPUTE_ALL_FRAMES = 4;

    /// <summary>Indicates that <see cref="STACK_SIZE_DELTA"/> is not applicable (not constant or never used).</summary>
    private const int NA = 0;

    /// <summary>
    /// The stack size variation corresponding to each JVM opcode. The stack size variation for opcode
    /// 'o' is given by the array element at index 'o'.
    /// </summary>
    /// <seealso href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-6.html">JVMS 6</seealso>
    private static readonly int[] STACK_SIZE_DELTA = {
        0, // nop = 0 (0x0)
        1, // aconst_null = 1 (0x1)
        1, // iconst_m1 = 2 (0x2)
        1, // iconst_0 = 3 (0x3)
        1, // iconst_1 = 4 (0x4)
        1, // iconst_2 = 5 (0x5)
        1, // iconst_3 = 6 (0x6)
        1, // iconst_4 = 7 (0x7)
        1, // iconst_5 = 8 (0x8)
        2, // lconst_0 = 9 (0x9)
        2, // lconst_1 = 10 (0xa)
        1, // fconst_0 = 11 (0xb)
        1, // fconst_1 = 12 (0xc)
        1, // fconst_2 = 13 (0xd)
        2, // dconst_0 = 14 (0xe)
        2, // dconst_1 = 15 (0xf)
        1, // bipush = 16 (0x10)
        1, // sipush = 17 (0x11)
        1, // ldc = 18 (0x12)
        NA, // ldc_w = 19 (0x13)
        NA, // ldc2_w = 20 (0x14)
        1, // iload = 21 (0x15)
        2, // lload = 22 (0x16)
        1, // fload = 23 (0x17)
        2, // dload = 24 (0x18)
        1, // aload = 25 (0x19)
        NA, // iload_0 = 26 (0x1a)
        NA, // iload_1 = 27 (0x1b)
        NA, // iload_2 = 28 (0x1c)
        NA, // iload_3 = 29 (0x1d)
        NA, // lload_0 = 30 (0x1e)
        NA, // lload_1 = 31 (0x1f)
        NA, // lload_2 = 32 (0x20)
        NA, // lload_3 = 33 (0x21)
        NA, // fload_0 = 34 (0x22)
        NA, // fload_1 = 35 (0x23)
        NA, // fload_2 = 36 (0x24)
        NA, // fload_3 = 37 (0x25)
        NA, // dload_0 = 38 (0x26)
        NA, // dload_1 = 39 (0x27)
        NA, // dload_2 = 40 (0x28)
        NA, // dload_3 = 41 (0x29)
        NA, // aload_0 = 42 (0x2a)
        NA, // aload_1 = 43 (0x2b)
        NA, // aload_2 = 44 (0x2c)
        NA, // aload_3 = 45 (0x2d)
        -1, // iaload = 46 (0x2e)
        0, // laload = 47 (0x2f)
        -1, // faload = 48 (0x30)
        0, // daload = 49 (0x31)
        -1, // aaload = 50 (0x32)
        -1, // baload = 51 (0x33)
        -1, // caload = 52 (0x34)
        -1, // saload = 53 (0x35)
        -1, // istore = 54 (0x36)
        -2, // lstore = 55 (0x37)
        -1, // fstore = 56 (0x38)
        -2, // dstore = 57 (0x39)
        -1, // astore = 58 (0x3a)
        NA, // istore_0 = 59 (0x3b)
        NA, // istore_1 = 60 (0x3c)
        NA, // istore_2 = 61 (0x3d)
        NA, // istore_3 = 62 (0x3e)
        NA, // lstore_0 = 63 (0x3f)
        NA, // lstore_1 = 64 (0x40)
        NA, // lstore_2 = 65 (0x41)
        NA, // lstore_3 = 66 (0x42)
        NA, // fstore_0 = 67 (0x43)
        NA, // fstore_1 = 68 (0x44)
        NA, // fstore_2 = 69 (0x45)
        NA, // fstore_3 = 70 (0x46)
        NA, // dstore_0 = 71 (0x47)
        NA, // dstore_1 = 72 (0x48)
        NA, // dstore_2 = 73 (0x49)
        NA, // dstore_3 = 74 (0x4a)
        NA, // astore_0 = 75 (0x4b)
        NA, // astore_1 = 76 (0x4c)
        NA, // astore_2 = 77 (0x4d)
        NA, // astore_3 = 78 (0x4e)
        -3, // iastore = 79 (0x4f)
        -4, // lastore = 80 (0x50)
        -3, // fastore = 81 (0x51)
        -4, // dastore = 82 (0x52)
        -3, // aastore = 83 (0x53)
        -3, // bastore = 84 (0x54)
        -3, // castore = 85 (0x55)
        -3, // sastore = 86 (0x56)
        -1, // pop = 87 (0x57)
        -2, // pop2 = 88 (0x58)
        1, // dup = 89 (0x59)
        1, // dup_x1 = 90 (0x5a)
        1, // dup_x2 = 91 (0x5b)
        2, // dup2 = 92 (0x5c)
        2, // dup2_x1 = 93 (0x5d)
        2, // dup2_x2 = 94 (0x5e)
        0, // swap = 95 (0x5f)
        -1, // iadd = 96 (0x60)
        -2, // ladd = 97 (0x61)
        -1, // fadd = 98 (0x62)
        -2, // dadd = 99 (0x63)
        -1, // isub = 100 (0x64)
        -2, // lsub = 101 (0x65)
        -1, // fsub = 102 (0x66)
        -2, // dsub = 103 (0x67)
        -1, // imul = 104 (0x68)
        -2, // lmul = 105 (0x69)
        -1, // fmul = 106 (0x6a)
        -2, // dmul = 107 (0x6b)
        -1, // idiv = 108 (0x6c)
        -2, // ldiv = 109 (0x6d)
        -1, // fdiv = 110 (0x6e)
        -2, // ddiv = 111 (0x6f)
        -1, // irem = 112 (0x70)
        -2, // lrem = 113 (0x71)
        -1, // frem = 114 (0x72)
        -2, // drem = 115 (0x73)
        0, // ineg = 116 (0x74)
        0, // lneg = 117 (0x75)
        0, // fneg = 118 (0x76)
        0, // dneg = 119 (0x77)
        -1, // ishl = 120 (0x78)
        -1, // lshl = 121 (0x79)
        -1, // ishr = 122 (0x7a)
        -1, // lshr = 123 (0x7b)
        -1, // iushr = 124 (0x7c)
        -1, // lushr = 125 (0x7d)
        -1, // iand = 126 (0x7e)
        -2, // land = 127 (0x7f)
        -1, // ior = 128 (0x80)
        -2, // lor = 129 (0x81)
        -1, // ixor = 130 (0x82)
        -2, // lxor = 131 (0x83)
        0, // iinc = 132 (0x84)
        1, // i2l = 133 (0x85)
        0, // i2f = 134 (0x86)
        1, // i2d = 135 (0x87)
        -1, // l2i = 136 (0x88)
        -1, // l2f = 137 (0x89)
        0, // l2d = 138 (0x8a)
        0, // f2i = 139 (0x8b)
        1, // f2l = 140 (0x8c)
        1, // f2d = 141 (0x8d)
        -1, // d2i = 142 (0x8e)
        0, // d2l = 143 (0x8f)
        -1, // d2f = 144 (0x90)
        0, // i2b = 145 (0x91)
        0, // i2c = 146 (0x92)
        0, // i2s = 147 (0x93)
        -3, // lcmp = 148 (0x94)
        -1, // fcmpl = 149 (0x95)
        -1, // fcmpg = 150 (0x96)
        -3, // dcmpl = 151 (0x97)
        -3, // dcmpg = 152 (0x98)
        -1, // ifeq = 153 (0x99)
        -1, // ifne = 154 (0x9a)
        -1, // iflt = 155 (0x9b)
        -1, // ifge = 156 (0x9c)
        -1, // ifgt = 157 (0x9d)
        -1, // ifle = 158 (0x9e)
        -2, // if_icmpeq = 159 (0x9f)
        -2, // if_icmpne = 160 (0xa0)
        -2, // if_icmplt = 161 (0xa1)
        -2, // if_icmpge = 162 (0xa2)
        -2, // if_icmpgt = 163 (0xa3)
        -2, // if_icmple = 164 (0xa4)
        -2, // if_acmpeq = 165 (0xa5)
        -2, // if_acmpne = 166 (0xa6)
        0, // goto = 167 (0xa7)
        1, // jsr = 168 (0xa8)
        0, // ret = 169 (0xa9)
        -1, // tableswitch = 170 (0xaa)
        -1, // lookupswitch = 171 (0xab)
        -1, // ireturn = 172 (0xac)
        -2, // lreturn = 173 (0xad)
        -1, // freturn = 174 (0xae)
        -2, // dreturn = 175 (0xaf)
        -1, // areturn = 176 (0xb0)
        0, // return = 177 (0xb1)
        NA, // getstatic = 178 (0xb2)
        NA, // putstatic = 179 (0xb3)
        NA, // getfield = 180 (0xb4)
        NA, // putfield = 181 (0xb5)
        NA, // invokevirtual = 182 (0xb6)
        NA, // invokespecial = 183 (0xb7)
        NA, // invokestatic = 184 (0xb8)
        NA, // invokeinterface = 185 (0xb9)
        NA, // invokedynamic = 186 (0xba)
        1, // new = 187 (0xbb)
        0, // newarray = 188 (0xbc)
        0, // anewarray = 189 (0xbd)
        0, // arraylength = 190 (0xbe)
        NA, // athrow = 191 (0xbf)
        0, // checkcast = 192 (0xc0)
        0, // instanceof = 193 (0xc1)
        -1, // monitorenter = 194 (0xc2)
        -1, // monitorexit = 195 (0xc3)
        NA, // wide = 196 (0xc4)
        NA, // multianewarray = 197 (0xc5)
        -1, // ifnull = 198 (0xc6)
        -1, // ifnonnull = 199 (0xc7)
        NA, // goto_w = 200 (0xc8)
        NA, // jsr_w = 201 (0xc9)
    };

    /// <summary>Where the constants used in this MethodWriter must be stored.</summary>
    private readonly SymbolTable symbolTable;

    // Note: fields are ordered as in the method_info structure, and those related to attributes are
    // ordered as in Section 4.7 of the JVMS.

    /// <summary>
    /// The access_flags field of the method_info JVMS structure. This field can contain ASM specific
    /// access flags, such as <see cref="Opcodes.ACC_DEPRECATED"/>, which are removed when generating the
    /// ClassFile structure.
    /// </summary>
    private readonly int accessFlags;

    /// <summary>The name_index field of the method_info JVMS structure.</summary>
    private readonly int nameIndex;

    /// <summary>The name of this method.</summary>
    private readonly string name;

    /// <summary>The descriptor_index field of the method_info JVMS structure.</summary>
    private readonly int descriptorIndex;

    /// <summary>The descriptor of this method.</summary>
    private readonly string descriptor;

    // Code attribute fields and sub attributes:

    /// <summary>The max_stack field of the Code attribute.</summary>
    private int maxStack;

    /// <summary>The max_locals field of the Code attribute.</summary>
    private int maxLocals;

    /// <summary>The 'code' field of the Code attribute.</summary>
    private readonly ByteVector code = new ByteVector();

    /// <summary>
    /// The first element in the exception handler list (used to generate the exception_table of the
    /// Code attribute). The next ones can be accessed with the <see cref="Handler.NextHandler"/> field. May
    /// be <c>null</c>.
    /// </summary>
    private Handler? firstHandler;

    /// <summary>
    /// The last element in the exception handler list (used to generate the exception_table of the
    /// Code attribute). The next ones can be accessed with the <see cref="Handler.NextHandler"/> field. May
    /// be <c>null</c>.
    /// </summary>
    private Handler? lastHandler;

    /// <summary>The line_number_table_length field of the LineNumberTable code attribute.</summary>
    private int lineNumberTableLength;

    /// <summary>The line_number_table array of the LineNumberTable code attribute, or <c>null</c>.</summary>
    private ByteVector? lineNumberTable;

    /// <summary>The local_variable_table_length field of the LocalVariableTable code attribute.</summary>
    private int localVariableTableLength;

    /// <summary>The local_variable_table array of the LocalVariableTable code attribute, or <c>null</c>.</summary>
    private ByteVector? localVariableTable;

    /// <summary>The local_variable_type_table_length field of the LocalVariableTypeTable code attribute.</summary>
    private int localVariableTypeTableLength;

    /// <summary>
    /// The local_variable_type_table array of the LocalVariableTypeTable code attribute, or
    /// <c>null</c>.
    /// </summary>
    private ByteVector? localVariableTypeTable;

    /// <summary>The number_of_entries field of the StackMapTable code attribute.</summary>
    private int stackMapTableNumberOfEntries;

    /// <summary>The 'entries' array of the StackMapTable code attribute.</summary>
    private ByteVector? stackMapTableEntries;

    /// <summary>
    /// The last runtime visible type annotation of the Code attribute. The previous ones can be
    /// accessed with the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastCodeRuntimeVisibleTypeAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime invisible type annotation of the Code attribute. The previous ones can be
    /// accessed with the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastCodeRuntimeInvisibleTypeAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The first non standard attribute of the Code attribute. The next ones can be accessed with the
    /// <see cref="Attribute.nextAttribute"/> field. May be <c>null</c>.
    /// </summary>
    private Attribute? firstCodeAttribute;

    // Other method_info attributes:

    /// <summary>The number_of_exceptions field of the Exceptions attribute.</summary>
    private readonly int numberOfExceptions;

    /// <summary>The exception_index_table array of the Exceptions attribute, or <c>null</c>.</summary>
    private readonly int[]? exceptionIndexTable;

    /// <summary>The signature_index field of the Signature attribute.</summary>
    private readonly int signatureIndex;

    /// <summary>
    /// The last runtime visible annotation of this method. The previous ones can be accessed with
    /// the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeVisibleAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime invisible annotation of this method. The previous ones can be accessed with
    /// the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeInvisibleAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>The number of method parameters that can have runtime visible annotations, or 0.</summary>
    private int visibleAnnotableParameterCount;

    /// <summary>
    /// The runtime visible parameter annotations of this method. Each array element contains the last
    /// annotation of a parameter (which can be <c>null</c> - the previous ones can be accessed
    /// with the <c>AnnotationWriter.previousAnnotation</c> field). May be <c>null</c>.
    /// </summary>
    private object?[]? lastRuntimeVisibleParameterAnnotations; // TODO: Replace with AnnotationWriter when ported

    /// <summary>The number of method parameters that can have runtime visible annotations, or 0.</summary>
    private int invisibleAnnotableParameterCount;

    /// <summary>
    /// The runtime invisible parameter annotations of this method. Each array element contains the
    /// last annotation of a parameter (which can be <c>null</c> - the previous ones can be
    /// accessed with the <c>AnnotationWriter.previousAnnotation</c> field). May be <c>null</c>.
    /// </summary>
    private object?[]? lastRuntimeInvisibleParameterAnnotations; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime visible type annotation of this method. The previous ones can be accessed with
    /// the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeVisibleTypeAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime invisible type annotation of this method. The previous ones can be accessed
    /// with the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeInvisibleTypeAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>The default_value field of the AnnotationDefault attribute, or <c>null</c>.</summary>
    private ByteVector? defaultValue;

    /// <summary>The parameters_count field of the MethodParameters attribute.</summary>
    private int parametersCount;

    /// <summary>The 'parameters' array of the MethodParameters attribute, or <c>null</c>.</summary>
    private ByteVector? parameters;

    /// <summary>
    /// The first non standard attribute of this method. The next ones can be accessed with the
    /// <see cref="Attribute.nextAttribute"/> field. May be <c>null</c>.
    /// </summary>
    private Attribute? firstAttribute;

    // -----------------------------------------------------------------------------------------------
    // Fields used to compute the maximum stack size and number of locals, and the stack map frames
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Indicates what must be computed. Must be one of <see cref="COMPUTE_ALL_FRAMES"/>,
    /// <see cref="COMPUTE_INSERTED_FRAMES"/>, <see cref="COMPUTE_MAX_STACK_AND_LOCAL_FROM_FRAMES"/>,
    /// <see cref="COMPUTE_MAX_STACK_AND_LOCAL"/> or <see cref="COMPUTE_NOTHING"/>.
    /// </summary>
    private readonly int compute;

    /// <summary>
    /// The first basic block of the method. The next ones (in bytecode offset order) can be accessed
    /// with the <see cref="Label.NextBasicBlock"/> field.
    /// </summary>
    private Label? firstBasicBlock;

    /// <summary>
    /// The last basic block of the method (in bytecode offset order). This field is updated each time
    /// a basic block is encountered, and is used to append it at the end of the basic block list.
    /// </summary>
    private Label? lastBasicBlock;

    /// <summary>
    /// The current basic block, i.e. the basic block of the last visited instruction.
    /// </summary>
    private Label? currentBasicBlock;

    /// <summary>
    /// The relative stack size after the last visited instruction.
    /// </summary>
    private int relativeStackSize;

    /// <summary>
    /// The maximum relative stack size after the last visited instruction.
    /// </summary>
    private int maxRelativeStackSize;

    /// <summary>The number of local variables in the last visited stack map frame.</summary>
    private int currentLocals;

    /// <summary>The bytecode offset of the last frame that was written in <see cref="stackMapTableEntries"/>.</summary>
    private int previousFrameOffset;

    /// <summary>
    /// The last frame that was written in <see cref="stackMapTableEntries"/>. This field has the same
    /// format as <see cref="currentFrame"/>.
    /// </summary>
    private int[]? previousFrame;

    /// <summary>
    /// The current stack map frame.
    /// </summary>
    private int[]? currentFrame;

    /// <summary>Whether this method contains subroutines.</summary>
    private bool hasSubroutines;

    // -----------------------------------------------------------------------------------------------
    // Other miscellaneous status fields
    // -----------------------------------------------------------------------------------------------

    /// <summary>Whether the bytecode of this method contains ASM specific instructions.</summary>
    private bool hasAsmInstructions;

    /// <summary>
    /// The start offset of the last visited instruction.
    /// </summary>
    private int lastBytecodeOffset;

    /// <summary>
    /// The offset in bytes in <see cref="SymbolTable.GetSource"/> from which the method_info for this method
    /// (excluding its first 6 bytes) must be copied, or 0.
    /// </summary>
    private int sourceOffset;

    /// <summary>
    /// The length in bytes in <see cref="SymbolTable.GetSource"/> which must be copied to get the
    /// method_info for this method (excluding its first 6 bytes for access_flags, name_index and
    /// descriptor_index).
    /// </summary>
    private int sourceLength;

    // -----------------------------------------------------------------------------------------------
    // Constructor and accessors
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Constructs a new <see cref="MethodWriter"/>.
    /// </summary>
    /// <param name="symbolTable">where the constants used in this MethodWriter must be stored.</param>
    /// <param name="access">the method's access flags (see <see cref="Opcodes"/>).</param>
    /// <param name="name">the method's name.</param>
    /// <param name="descriptor">the method's descriptor (see <see cref="Type"/>).</param>
    /// <param name="signature">the method's signature. May be <c>null</c>.</param>
    /// <param name="exceptions">the internal names of the method's exceptions. May be <c>null</c>.</param>
    /// <param name="compute">indicates what must be computed (see <see cref="compute"/>).</param>
    internal MethodWriter(
        SymbolTable symbolTable,
        int access,
        string name,
        string descriptor,
        string? signature,
        string[]? exceptions,
        int compute)
        : base(Opcodes.ASM9)
    {
        this.symbolTable = symbolTable;
        this.accessFlags = "<init>".Equals(name) ? access | Constants.ACC_CONSTRUCTOR : access;
        this.nameIndex = symbolTable.AddConstantUtf8(name);
        this.name = name;
        this.descriptorIndex = symbolTable.AddConstantUtf8(descriptor);
        this.descriptor = descriptor;
        this.signatureIndex = signature == null ? 0 : symbolTable.AddConstantUtf8(signature);
        if (exceptions != null && exceptions.Length > 0)
        {
            numberOfExceptions = exceptions.Length;
            this.exceptionIndexTable = new int[numberOfExceptions];
            for (int i = 0; i < numberOfExceptions; ++i)
            {
                this.exceptionIndexTable[i] = symbolTable.AddConstantClass(exceptions[i]).Index;
            }
        }
        else
        {
            numberOfExceptions = 0;
            this.exceptionIndexTable = null;
        }
        this.compute = compute;
        if (compute != COMPUTE_NOTHING)
        {
            // Update maxLocals and currentLocals.
            int argumentsSize = Type.GetArgumentsAndReturnSizes(descriptor) >> 2;
            if ((access & Opcodes.ACC_STATIC) != 0)
            {
                --argumentsSize;
            }
            maxLocals = argumentsSize;
            currentLocals = argumentsSize;
            // Create and visit the label for the first basic block.
            firstBasicBlock = new Label();
            VisitLabel(firstBasicBlock);
        }
    }

    internal bool HasFrames()
    {
        return stackMapTableNumberOfEntries > 0;
    }

    internal bool HasAsmInstructions()
    {
        return hasAsmInstructions;
    }

    // -----------------------------------------------------------------------------------------------
    // Linked list pointer
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// The next MethodWriter in the linked list of methods, accessed via the protected
    /// <see cref="MethodVisitor.Mv"/> field. Used by <see cref="ClassWriter"/> to chain methods.
    /// </summary>
    internal MethodWriter? NextMethod
    {
        get => (MethodWriter?)Mv;
        set => Mv = value;
    }

    // -----------------------------------------------------------------------------------------------
    // Implementation of the MethodVisitor abstract class
    // -----------------------------------------------------------------------------------------------

    public override void VisitParameter(string? name, int access)
    {
        if (parameters == null)
        {
            parameters = new ByteVector();
        }
        ++parametersCount;
        parameters!.PutShort(name == null ? 0 : symbolTable.AddConstantUtf8(name)).PutShort(access);
    }

    public override AnnotationVisitor? VisitAnnotationDefault()
    {
        // TODO: port AnnotationWriter — annotations are dropped for now
        return null;
    }

    public override AnnotationVisitor? VisitAnnotation(string? descriptor, bool visible)
    {
        // TODO: Replace with AnnotationWriter when ported
        return null; // TODO: port AnnotationWriter — annotations are dropped for now
    }

    public override AnnotationVisitor? VisitTypeAnnotation(
        int typeRef, TypePath? typePath, string? descriptor, bool visible)
    {
        // TODO: Replace with AnnotationWriter when ported
        return null; // TODO: port AnnotationWriter — annotations are dropped for now
    }

    public override void VisitAnnotableParameterCount(int parameterCount, bool visible)
    {
        if (visible)
        {
            visibleAnnotableParameterCount = parameterCount;
        }
        else
        {
            invisibleAnnotableParameterCount = parameterCount;
        }
    }

    public override AnnotationVisitor? VisitParameterAnnotation(
        int parameter, string? annotationDescriptor, bool visible)
    {
        // TODO: Replace with AnnotationWriter when ported
        return null; // TODO: port AnnotationWriter — annotations are dropped for now
    }

    public override void VisitAttribute(Attribute? attribute)
    {
        // Store the attributes in the reverse order of their visit by this method.
        if (attribute!.IsCodeAttribute())
        {
            attribute.nextAttribute = firstCodeAttribute;
            firstCodeAttribute = attribute;
        }
        else
        {
            attribute.nextAttribute = firstAttribute;
            firstAttribute = attribute;
        }
    }

    public override void VisitCode()
    {
        // Nothing to do.
    }

    public override void VisitFrame(
        int type,
        int numLocal,
        object?[]? local,
        int numStack,
        object?[]? stack)
    {
        if (compute == COMPUTE_ALL_FRAMES)
        {
            return;
        }

        if (compute == COMPUTE_INSERTED_FRAMES)
        {
            // TODO: CurrentFrame not ported yet.
            throw new NotImplementedException("CurrentFrame not ported yet.");
        }
        else if (type == Opcodes.F_NEW)
        {
            if (previousFrame == null)
            {
                int argumentsSize = Type.GetArgumentsAndReturnSizes(descriptor) >> 2;
                Frame implicitFirstFrame = new Frame(new Label());
                implicitFirstFrame.SetInputFrameFromDescriptor(
                    symbolTable, accessFlags, descriptor, argumentsSize);
                implicitFirstFrame.Accept(this);
            }
            currentLocals = numLocal;
            int frameIndex = VisitFrameStart(code.Length, numLocal, numStack);
            for (int i = 0; i < numLocal; ++i)
            {
                currentFrame![frameIndex++] = Frame.GetAbstractTypeFromApiFormat(symbolTable, local![i]!);
            }
            for (int i = 0; i < numStack; ++i)
            {
                currentFrame![frameIndex++] = Frame.GetAbstractTypeFromApiFormat(symbolTable, stack![i]!);
            }
            VisitFrameEnd();
        }
        else
        {
            if (symbolTable.GetMajorVersion() < Opcodes.V1_6)
            {
                throw new ArgumentException("Class versions V1_5 or less must use F_NEW frames.");
            }
            int offsetDelta;
            if (stackMapTableEntries == null)
            {
                stackMapTableEntries = new ByteVector();
                offsetDelta = code.Length;
            }
            else
            {
                offsetDelta = code.Length - previousFrameOffset - 1;
                if (offsetDelta < 0)
                {
                    if (type == Opcodes.F_SAME)
                    {
                        return;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }

            switch (type)
            {
                case Opcodes.F_FULL:
                    currentLocals = numLocal;
                    stackMapTableEntries!.PutByte(Frame.FULL_FRAME).PutShort(offsetDelta).PutShort(numLocal);
                    for (int i = 0; i < numLocal; ++i)
                    {
                        PutFrameType(local![i]!);
                    }
                    stackMapTableEntries.PutShort(numStack);
                    for (int i = 0; i < numStack; ++i)
                    {
                        PutFrameType(stack![i]!);
                    }
                    break;
                case Opcodes.F_APPEND:
                    currentLocals += numLocal;
                    stackMapTableEntries!.PutByte(Frame.SAME_FRAME_EXTENDED + numLocal).PutShort(offsetDelta);
                    for (int i = 0; i < numLocal; ++i)
                    {
                        PutFrameType(local![i]!);
                    }
                    break;
                case Opcodes.F_CHOP:
                    currentLocals -= numLocal;
                    stackMapTableEntries!.PutByte(Frame.SAME_FRAME_EXTENDED - numLocal).PutShort(offsetDelta);
                    break;
                case Opcodes.F_SAME:
                    if (offsetDelta < 64)
                    {
                        stackMapTableEntries!.PutByte(offsetDelta);
                    }
                    else
                    {
                        stackMapTableEntries!.PutByte(Frame.SAME_FRAME_EXTENDED).PutShort(offsetDelta);
                    }
                    break;
                case Opcodes.F_SAME1:
                    if (offsetDelta < 64)
                    {
                        stackMapTableEntries!.PutByte(Frame.SAME_LOCALS_1_STACK_ITEM_FRAME + offsetDelta);
                    }
                    else
                    {
                        stackMapTableEntries!
                            .PutByte(Frame.SAME_LOCALS_1_STACK_ITEM_FRAME_EXTENDED)
                            .PutShort(offsetDelta);
                    }
                    PutFrameType(stack![0]!);
                    break;
                default:
                    throw new ArgumentException();
            }

            previousFrameOffset = code.Length;
            ++stackMapTableNumberOfEntries;
        }

        if (compute == COMPUTE_MAX_STACK_AND_LOCAL_FROM_FRAMES)
        {
            relativeStackSize = numStack;
            for (int i = 0; i < numStack; ++i)
            {
                if (stack![i] is int intValue && (intValue == Opcodes.LONG || intValue == Opcodes.DOUBLE))
                {
                    relativeStackSize++;
                }
            }
            if (relativeStackSize > maxRelativeStackSize)
            {
                maxRelativeStackSize = relativeStackSize;
            }
        }

        maxStack = Math.Max(maxStack, numStack);
        maxLocals = Math.Max(maxLocals, currentLocals);
    }

    public override void VisitInsn(int opcode)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        code.PutByte(opcode);
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(opcode, 0, null!, null!);
            }
            else
            {
                int size = relativeStackSize + STACK_SIZE_DELTA[opcode];
                if (size > maxRelativeStackSize)
                {
                    maxRelativeStackSize = size;
                }
                relativeStackSize = size;
            }
            if ((opcode >= Opcodes.IRETURN && opcode <= Opcodes.RETURN) || opcode == Opcodes.ATHROW)
            {
                EndCurrentBasicBlockWithNoSuccessor();
            }
        }
    }

    public override void VisitIntInsn(int opcode, int operand)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        if (opcode == Opcodes.SIPUSH)
        {
            code.Put12(opcode, operand);
        }
        else
        { // BIPUSH or NEWARRAY
            code.Put11(opcode, operand);
        }
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(opcode, operand, null!, null!);
            }
            else if (opcode != Opcodes.NEWARRAY)
            {
                // The stack size delta is 1 for BIPUSH or SIPUSH, and 0 for NEWARRAY.
                int size = relativeStackSize + 1;
                if (size > maxRelativeStackSize)
                {
                    maxRelativeStackSize = size;
                }
                relativeStackSize = size;
            }
        }
    }

    public override void VisitVarInsn(int opcode, int varIndex)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        if (varIndex < 4 && opcode != Opcodes.RET)
        {
            int optimizedOpcode;
            if (opcode < Opcodes.ISTORE)
            {
                optimizedOpcode = Constants.ILOAD_0 + ((opcode - Opcodes.ILOAD) << 2) + varIndex;
            }
            else
            {
                optimizedOpcode = Constants.ISTORE_0 + ((opcode - Opcodes.ISTORE) << 2) + varIndex;
            }
            code.PutByte(optimizedOpcode);
        }
        else if (varIndex >= 256)
        {
            code.PutByte(Constants.WIDE).Put12(opcode, varIndex);
        }
        else
        {
            code.Put11(opcode, varIndex);
        }
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(opcode, varIndex, null!, null!);
            }
            else
            {
                if (opcode == Opcodes.RET)
                {
                    // No stack size delta.
                    currentBasicBlock.Flags |= Label.FLAG_SUBROUTINE_END;
                    currentBasicBlock.OutputStackSize = (short)relativeStackSize;
                    EndCurrentBasicBlockWithNoSuccessor();
                }
                else
                { // xLOAD or xSTORE
                    int size = relativeStackSize + STACK_SIZE_DELTA[opcode];
                    if (size > maxRelativeStackSize)
                    {
                        maxRelativeStackSize = size;
                    }
                    relativeStackSize = size;
                }
            }
        }
        if (compute != COMPUTE_NOTHING)
        {
            int currentMaxLocals;
            if (opcode == Opcodes.LLOAD
                || opcode == Opcodes.DLOAD
                || opcode == Opcodes.LSTORE
                || opcode == Opcodes.DSTORE)
            {
                currentMaxLocals = varIndex + 2;
            }
            else
            {
                currentMaxLocals = varIndex + 1;
            }
            if (currentMaxLocals > maxLocals)
            {
                maxLocals = currentMaxLocals;
            }
        }
        if (opcode >= Opcodes.ISTORE && compute == COMPUTE_ALL_FRAMES && firstHandler != null)
        {
            // If there are exception handler blocks, each instruction within a handler range is, in
            // theory, a basic block. As a consequence, the local variable types at the beginning of the
            // handler block should be the merge of the local variable types at all the instructions
            // within the handler range. However, instead of creating a basic block for each instruction,
            // we can get the same result in a more efficient way. Namely, by starting a new basic block
            // after each xSTORE instruction, which is what we do here.
            VisitLabel(new Label());
        }
    }

    public override void VisitTypeInsn(int opcode, string? type)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        Symbol typeSymbol = symbolTable.AddConstantClass(type!);
        code.Put12(opcode, typeSymbol.Index);
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(opcode, lastBytecodeOffset, typeSymbol, symbolTable);
            }
            else if (opcode == Opcodes.NEW)
            {
                // The stack size delta is 1 for NEW, and 0 for ANEWARRAY, CHECKCAST, or INSTANCEOF.
                int size = relativeStackSize + 1;
                if (size > maxRelativeStackSize)
                {
                    maxRelativeStackSize = size;
                }
                relativeStackSize = size;
            }
        }
    }

    public override void VisitFieldInsn(int opcode, string? owner, string? name, string? descriptor)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        Symbol fieldrefSymbol = symbolTable.AddConstantFieldref(owner!, name!, descriptor!);
        code.Put12(opcode, fieldrefSymbol.Index);
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(opcode, 0, fieldrefSymbol, symbolTable);
            }
            else
            {
                int size;
                char firstDescChar = descriptor![0];
                switch (opcode)
                {
                    case Opcodes.GETSTATIC:
                        size = relativeStackSize + (firstDescChar == 'D' || firstDescChar == 'J' ? 2 : 1);
                        break;
                    case Opcodes.PUTSTATIC:
                        size = relativeStackSize + (firstDescChar == 'D' || firstDescChar == 'J' ? -2 : -1);
                        break;
                    case Opcodes.GETFIELD:
                        size = relativeStackSize + (firstDescChar == 'D' || firstDescChar == 'J' ? 1 : 0);
                        break;
                    case Opcodes.PUTFIELD:
                    default:
                        size = relativeStackSize + (firstDescChar == 'D' || firstDescChar == 'J' ? -3 : -2);
                        break;
                }
                if (size > maxRelativeStackSize)
                {
                    maxRelativeStackSize = size;
                }
                relativeStackSize = size;
            }
        }
    }

    public override void VisitMethodInsn(
        int opcode, string? owner, string? name, string? descriptor, bool isInterface)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        Symbol methodrefSymbol = symbolTable.AddConstantMethodref(owner!, name!, descriptor!, isInterface);
        if (opcode == Opcodes.INVOKEINTERFACE)
        {
            code.Put12(Opcodes.INVOKEINTERFACE, methodrefSymbol.Index)
                .Put11(methodrefSymbol.GetArgumentsAndReturnSizes() >> 2, 0);
        }
        else
        {
            code.Put12(opcode, methodrefSymbol.Index);
        }
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(opcode, 0, methodrefSymbol, symbolTable);
            }
            else
            {
                int argumentsAndReturnSize = methodrefSymbol.GetArgumentsAndReturnSizes();
                int stackSizeDelta = (argumentsAndReturnSize & 3) - (argumentsAndReturnSize >> 2);
                int size;
                if (opcode == Opcodes.INVOKESTATIC)
                {
                    size = relativeStackSize + stackSizeDelta + 1;
                }
                else
                {
                    size = relativeStackSize + stackSizeDelta;
                }
                if (size > maxRelativeStackSize)
                {
                    maxRelativeStackSize = size;
                }
                relativeStackSize = size;
            }
        }
    }

    public override void VisitInvokeDynamicInsn(
        string? name,
        string? descriptor,
        Handle? bootstrapMethodHandle,
        params object?[]? bootstrapMethodArguments)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        Symbol invokeDynamicSymbol =
            symbolTable.AddConstantInvokeDynamic(
                name!, descriptor!, bootstrapMethodHandle!, bootstrapMethodArguments!);
        code.Put12(Opcodes.INVOKEDYNAMIC, invokeDynamicSymbol.Index);
        code.PutShort(0);
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(Opcodes.INVOKEDYNAMIC, 0, invokeDynamicSymbol, symbolTable);
            }
            else
            {
                int argumentsAndReturnSize = invokeDynamicSymbol.GetArgumentsAndReturnSizes();
                int stackSizeDelta = (argumentsAndReturnSize & 3) - (argumentsAndReturnSize >> 2) + 1;
                int size = relativeStackSize + stackSizeDelta;
                if (size > maxRelativeStackSize)
                {
                    maxRelativeStackSize = size;
                }
                relativeStackSize = size;
            }
        }
    }

    public override void VisitJumpInsn(int opcode, Label label)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        // Compute the 'base' opcode, i.e. GOTO or JSR if opcode is GOTO_W or JSR_W, otherwise opcode.
        int baseOpcode =
            opcode >= Constants.GOTO_W ? opcode - Constants.WIDE_JUMP_OPCODE_DELTA : opcode;
        bool nextInsnIsJumpTarget = false;
        if ((label.Flags & Label.FLAG_RESOLVED) != 0
            && label.BytecodeOffset - code.Length < short.MinValue)
        {
            // Case of a backward jump with an offset < -32768.
            if (baseOpcode == Opcodes.GOTO)
            {
                code.PutByte(Constants.GOTO_W);
            }
            else if (baseOpcode == Opcodes.JSR)
            {
                code.PutByte(Constants.JSR_W);
            }
            else
            {
                // Put the "opposite" opcode of baseOpcode.
                code.PutByte(baseOpcode >= Opcodes.IFNULL ? baseOpcode ^ 1 : ((baseOpcode + 1) ^ 1) - 1);
                code.PutShort(8);
                code.PutByte(Constants.ASM_GOTO_W);
                hasAsmInstructions = true;
                nextInsnIsJumpTarget = true;
            }
            label.Put(code, code.Length - 1, true);
        }
        else if (baseOpcode != opcode)
        {
            // Case of a GOTO_W or JSR_W specified by the user.
            code.PutByte(opcode);
            label.Put(code, code.Length - 1, true);
        }
        else
        {
            // Case of a jump with an offset >= -32768, or of a jump with an unknown offset.
            code.PutByte(baseOpcode);
            label.Put(code, code.Length - 1, false);
        }

        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            Label? nextBasicBlock = null;
            if (compute == COMPUTE_ALL_FRAMES)
            {
                currentBasicBlock.frame!.Execute(baseOpcode, 0, null!, null!);
                // Record the fact that 'label' is the target of a jump instruction.
                label.GetCanonicalInstance().Flags |= Label.FLAG_JUMP_TARGET;
                // Add 'label' as a successor of the current basic block.
                AddSuccessorToCurrentBasicBlock(Edge.JUMP, label);
                if (baseOpcode != Opcodes.GOTO)
                {
                    nextBasicBlock = new Label();
                }
            }
            else if (compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(baseOpcode, 0, null!, null!);
            }
            else if (compute == COMPUTE_MAX_STACK_AND_LOCAL_FROM_FRAMES)
            {
                // No need to update maxRelativeStackSize (the stack size delta is always negative).
                relativeStackSize += STACK_SIZE_DELTA[baseOpcode];
            }
            else
            {
                if (baseOpcode == Opcodes.JSR)
                {
                    if ((label.Flags & Label.FLAG_SUBROUTINE_START) == 0)
                    {
                        label.Flags |= Label.FLAG_SUBROUTINE_START;
                        hasSubroutines = true;
                    }
                    currentBasicBlock.Flags |= Label.FLAG_SUBROUTINE_CALLER;
                    AddSuccessorToCurrentBasicBlock(relativeStackSize + 1, label);
                    nextBasicBlock = new Label();
                }
                else
                {
                    // No need to update maxRelativeStackSize (the stack size delta is always negative).
                    relativeStackSize += STACK_SIZE_DELTA[baseOpcode];
                    AddSuccessorToCurrentBasicBlock(relativeStackSize, label);
                }
            }
            if (nextBasicBlock != null)
            {
                if (nextInsnIsJumpTarget)
                {
                    nextBasicBlock.Flags |= Label.FLAG_JUMP_TARGET;
                }
                VisitLabel(nextBasicBlock);
            }
            if (baseOpcode == Opcodes.GOTO)
            {
                EndCurrentBasicBlockWithNoSuccessor();
            }
        }
    }

    public override void VisitLabel(Label label)
    {
        // Resolve the forward references to this label, if any.
        hasAsmInstructions |= label.Resolve(code.Data, stackMapTableEntries, code.Length);
        // visitLabel starts a new basic block (except for debug only labels).
        if ((label.Flags & Label.FLAG_DEBUG_ONLY) != 0)
        {
            return;
        }
        if (compute == COMPUTE_ALL_FRAMES)
        {
            if (currentBasicBlock != null)
            {
                if (label.BytecodeOffset == currentBasicBlock.BytecodeOffset)
                {
                    currentBasicBlock.Flags |= (short)(label.Flags & Label.FLAG_JUMP_TARGET);
                    label.frame = currentBasicBlock.frame;
                    return;
                }
                // End the current basic block (with one new successor).
                AddSuccessorToCurrentBasicBlock(Edge.JUMP, label);
            }
            // Append 'label' at the end of the basic block list.
            if (lastBasicBlock != null)
            {
                if (label.BytecodeOffset == lastBasicBlock.BytecodeOffset)
                {
                    lastBasicBlock.Flags |= (short)(label.Flags & Label.FLAG_JUMP_TARGET);
                    label.frame = lastBasicBlock.frame;
                    currentBasicBlock = lastBasicBlock;
                    return;
                }
                lastBasicBlock.NextBasicBlock = label;
            }
            lastBasicBlock = label;
            // Make it the new current basic block.
            currentBasicBlock = label;
            label.frame = new Frame(label);
        }
        else if (compute == COMPUTE_INSERTED_FRAMES)
        {
            if (currentBasicBlock == null)
            {
                currentBasicBlock = label;
            }
            else
            {
                currentBasicBlock.frame!.owner = label;
            }
        }
        else if (compute == COMPUTE_MAX_STACK_AND_LOCAL)
        {
            if (currentBasicBlock != null)
            {
                // End the current basic block (with one new successor).
                currentBasicBlock.OutputStackMax = (short)maxRelativeStackSize;
                AddSuccessorToCurrentBasicBlock(relativeStackSize, label);
            }
            // Start a new current basic block, and reset the current and maximum relative stack sizes.
            currentBasicBlock = label;
            relativeStackSize = 0;
            maxRelativeStackSize = 0;
            // Append the new basic block at the end of the basic block list.
            if (lastBasicBlock != null)
            {
                lastBasicBlock.NextBasicBlock = label;
            }
            lastBasicBlock = label;
        }
        else if (compute == COMPUTE_MAX_STACK_AND_LOCAL_FROM_FRAMES && currentBasicBlock == null)
        {
            currentBasicBlock = label;
        }
    }

    public override void VisitLdcInsn(object? value)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        Symbol constantSymbol = symbolTable.AddConstant(value);
        int constantIndex = constantSymbol.Index;
        char firstDescriptorChar = '\0';
        bool isLongOrDouble =
            constantSymbol.Tag == Symbol.CONSTANT_LONG_TAG
            || constantSymbol.Tag == Symbol.CONSTANT_DOUBLE_TAG
            || (constantSymbol.Tag == Symbol.CONSTANT_DYNAMIC_TAG
                && ((firstDescriptorChar = constantSymbol.Value![0]) == 'J'
                    || firstDescriptorChar == 'D'));
        if (isLongOrDouble)
        {
            code.Put12(Constants.LDC2_W, constantIndex);
        }
        else if (constantIndex >= 256)
        {
            code.Put12(Constants.LDC_W, constantIndex);
        }
        else
        {
            code.Put11(Opcodes.LDC, constantIndex);
        }
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(Opcodes.LDC, 0, constantSymbol, symbolTable);
            }
            else
            {
                int size = relativeStackSize + (isLongOrDouble ? 2 : 1);
                if (size > maxRelativeStackSize)
                {
                    maxRelativeStackSize = size;
                }
                relativeStackSize = size;
            }
        }
    }

    public override void VisitIincInsn(int varIndex, int increment)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        if ((varIndex > 255) || (increment > 127) || (increment < -128))
        {
            code.PutByte(Constants.WIDE).Put12(Opcodes.IINC, varIndex).PutShort(increment);
        }
        else
        {
            code.PutByte(Opcodes.IINC).Put11(varIndex, increment);
        }
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null
            && (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES))
        {
            currentBasicBlock.frame!.Execute(Opcodes.IINC, varIndex, null!, null!);
        }
        if (compute != COMPUTE_NOTHING)
        {
            int currentMaxLocals = varIndex + 1;
            if (currentMaxLocals > maxLocals)
            {
                maxLocals = currentMaxLocals;
            }
        }
    }

    public override void VisitTableSwitchInsn(int min, int max, Label dflt, params Label[]? labels)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        code.PutByte(Opcodes.TABLESWITCH).PutByteArray(null, 0, (4 - code.Length % 4) % 4);
        dflt.Put(code, lastBytecodeOffset, true);
        code.PutInt(min).PutInt(max);
        Label[]? labelsArray = labels;
        if (labelsArray != null)
        {
            foreach (Label label in labelsArray)
            {
                label.Put(code, lastBytecodeOffset, true);
            }
        }
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        VisitSwitchInsn(dflt, labels ?? Array.Empty<Label>());
    }

    public override void VisitLookupSwitchInsn(Label dflt, int[]? keys, Label[]? labels)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        code.PutByte(Opcodes.LOOKUPSWITCH).PutByteArray(null, 0, (4 - code.Length % 4) % 4);
        dflt.Put(code, lastBytecodeOffset, true);
        code.PutInt(labels!.Length);
        for (int i = 0; i < labels.Length; ++i)
        {
            code.PutInt(keys![i]);
            labels[i].Put(code, lastBytecodeOffset, true);
        }
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        VisitSwitchInsn(dflt, labels);
    }

    private void VisitSwitchInsn(Label dflt, Label[] labels)
    {
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES)
            {
                currentBasicBlock.frame!.Execute(Opcodes.LOOKUPSWITCH, 0, null!, null!);
                // Add all the labels as successors of the current basic block.
                AddSuccessorToCurrentBasicBlock(Edge.JUMP, dflt);
                dflt.GetCanonicalInstance().Flags |= Label.FLAG_JUMP_TARGET;
                foreach (Label label in labels)
                {
                    AddSuccessorToCurrentBasicBlock(Edge.JUMP, label);
                    label.GetCanonicalInstance().Flags |= Label.FLAG_JUMP_TARGET;
                }
            }
            else if (compute == COMPUTE_MAX_STACK_AND_LOCAL)
            {
                // No need to update maxRelativeStackSize (the stack size delta is always negative).
                --relativeStackSize;
                // Add all the labels as successors of the current basic block.
                AddSuccessorToCurrentBasicBlock(relativeStackSize, dflt);
                foreach (Label label in labels)
                {
                    AddSuccessorToCurrentBasicBlock(relativeStackSize, label);
                }
            }
            // End the current basic block.
            EndCurrentBasicBlockWithNoSuccessor();
        }
    }

    public override void VisitMultiANewArrayInsn(string? descriptor, int numDimensions)
    {
        lastBytecodeOffset = code.Length;
        // Add the instruction to the bytecode of the method.
        Symbol descSymbol = symbolTable.AddConstantClass(descriptor!);
        code.Put12(Opcodes.MULTIANEWARRAY, descSymbol.Index).PutByte(numDimensions);
        // If needed, update the maximum stack size and number of locals, and stack map frames.
        if (currentBasicBlock != null)
        {
            if (compute == COMPUTE_ALL_FRAMES || compute == COMPUTE_INSERTED_FRAMES)
            {
                currentBasicBlock.frame!.Execute(
                    Opcodes.MULTIANEWARRAY, numDimensions, descSymbol, symbolTable);
            }
            else
            {
                // No need to update maxRelativeStackSize (the stack size delta is always negative).
                relativeStackSize += 1 - numDimensions;
            }
        }
    }

    public override AnnotationVisitor? VisitInsnAnnotation(
        int typeRef, TypePath? typePath, string? descriptor, bool visible)
    {
        // TODO: Replace with AnnotationWriter when ported
        return null; // TODO: port AnnotationWriter — annotations are dropped for now
    }

    public override void VisitTryCatchBlock(Label start, Label end, Label handler, string? type)
    {
        Handler newHandler =
            new Handler(
                start, end, handler, type != null ? symbolTable.AddConstantClass(type).Index : 0, type);
        if (firstHandler == null)
        {
            firstHandler = newHandler;
        }
        else
        {
            lastHandler!.NextHandler = newHandler;
        }
        lastHandler = newHandler;
    }

    public override AnnotationVisitor? VisitTryCatchAnnotation(
        int typeRef, TypePath? typePath, string? descriptor, bool visible)
    {
        // TODO: Replace with AnnotationWriter when ported
        return null; // TODO: port AnnotationWriter — annotations are dropped for now
    }

    public override void VisitLocalVariable(
        string? name,
        string? descriptor,
        string? signature,
        Label start,
        Label end,
        int index)
    {
        if (signature != null)
        {
            if (localVariableTypeTable == null)
            {
                localVariableTypeTable = new ByteVector();
            }
            ++localVariableTypeTableLength;
            localVariableTypeTable!
                .PutShort(start.BytecodeOffset)
                .PutShort(end.BytecodeOffset - start.BytecodeOffset)
                .PutShort(symbolTable.AddConstantUtf8(name!))
                .PutShort(symbolTable.AddConstantUtf8(signature))
                .PutShort(index);
        }
        if (localVariableTable == null)
        {
            localVariableTable = new ByteVector();
        }
        ++localVariableTableLength;
        localVariableTable!
            .PutShort(start.BytecodeOffset)
            .PutShort(end.BytecodeOffset - start.BytecodeOffset)
            .PutShort(symbolTable.AddConstantUtf8(name!))
            .PutShort(symbolTable.AddConstantUtf8(descriptor!))
            .PutShort(index);
        if (compute != COMPUTE_NOTHING)
        {
            char firstDescChar = descriptor![0];
            int currentMaxLocals = index + (firstDescChar == 'J' || firstDescChar == 'D' ? 2 : 1);
            if (currentMaxLocals > maxLocals)
            {
                maxLocals = currentMaxLocals;
            }
        }
    }

    public override AnnotationVisitor? VisitLocalVariableAnnotation(
        int typeRef,
        TypePath? typePath,
        Label[]? start,
        Label[]? end,
        int[]? index,
        string? descriptor,
        bool visible)
    {
        // TODO: Replace with AnnotationWriter when ported
        return null; // TODO: port AnnotationWriter — annotations are dropped for now
    }

    public override void VisitLineNumber(int line, Label start)
    {
        if (lineNumberTable == null)
        {
            lineNumberTable = new ByteVector();
        }
        ++lineNumberTableLength;
        lineNumberTable!.PutShort(start.BytecodeOffset);
        lineNumberTable.PutShort(line);
    }

    public override void VisitMaxs(int maxStack, int maxLocals)
    {
        if (compute == COMPUTE_ALL_FRAMES)
        {
            ComputeAllFrames();
        }
        else if (compute == COMPUTE_MAX_STACK_AND_LOCAL)
        {
            ComputeMaxStackAndLocal();
        }
        else if (compute == COMPUTE_MAX_STACK_AND_LOCAL_FROM_FRAMES)
        {
            this.maxStack = maxRelativeStackSize;
        }
        else
        {
            this.maxStack = maxStack;
            this.maxLocals = maxLocals;
        }
    }

    /// <summary>Computes all the stack map frames of the method, from scratch.</summary>
    private void ComputeAllFrames()
    {
        // TODO: ClassWriter not ported yet. This method depends on Frame.Accept and
        // SymbolTable.AddMergedType which require ClassWriter.GetCommonSuperClass.
        throw new NotImplementedException("ClassWriter not ported yet.");
    }

    /// <summary>Computes the maximum stack size of the method.</summary>
    private void ComputeMaxStackAndLocal()
    {
        // TODO: ClassWriter not ported yet. This method is part of the frame computation pipeline.
        throw new NotImplementedException("ClassWriter not ported yet.");
    }

    public override void VisitEnd()
    {
        // Nothing to do.
    }

    // -----------------------------------------------------------------------------------------------
    // Utility methods: control flow analysis algorithm
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds a successor to <see cref="currentBasicBlock"/> in the control flow graph.
    /// </summary>
    /// <param name="info">information about the control flow edge to be added.</param>
    /// <param name="successor">the successor block to be added to the current basic block.</param>
    private void AddSuccessorToCurrentBasicBlock(int info, Label successor)
    {
        currentBasicBlock!.OutgoingEdges = new Edge(info, successor, currentBasicBlock.OutgoingEdges);
    }

    /// <summary>
    /// Ends the current basic block. This method must be used in the case where the current basic
    /// block does not have any successor.
    /// </summary>
    private void EndCurrentBasicBlockWithNoSuccessor()
    {
        if (compute == COMPUTE_ALL_FRAMES)
        {
            Label nextBasicBlock = new Label();
            nextBasicBlock.frame = new Frame(nextBasicBlock);
            nextBasicBlock.Resolve(code.Data, stackMapTableEntries, code.Length);
            lastBasicBlock!.NextBasicBlock = nextBasicBlock;
            lastBasicBlock = nextBasicBlock;
            currentBasicBlock = null;
        }
        else if (compute == COMPUTE_MAX_STACK_AND_LOCAL)
        {
            currentBasicBlock!.OutputStackMax = (short)maxRelativeStackSize;
            currentBasicBlock = null;
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Utility methods: stack map frames
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Starts the visit of a new stack map frame, stored in <see cref="currentFrame"/>.
    /// </summary>
    /// <param name="offset">the bytecode offset of the instruction to which the frame corresponds.</param>
    /// <param name="numLocal">the number of local variables in the frame.</param>
    /// <param name="numStack">the number of stack elements in the frame.</param>
    /// <returns>the index of the next element to be written in this frame.</returns>
    internal int VisitFrameStart(int offset, int numLocal, int numStack)
    {
        int frameLength = 3 + numLocal + numStack;
        if (currentFrame == null || currentFrame.Length < frameLength)
        {
            currentFrame = new int[frameLength];
        }
        currentFrame[0] = offset;
        currentFrame[1] = numLocal;
        currentFrame[2] = numStack;
        return 3;
    }

    /// <summary>
    /// Sets an abstract type in <see cref="currentFrame"/>.
    /// </summary>
    /// <param name="frameIndex">the index of the element to be set in <see cref="currentFrame"/>.</param>
    /// <param name="abstractType">an abstract type.</param>
    internal void VisitAbstractType(int frameIndex, int abstractType)
    {
        currentFrame![frameIndex] = abstractType;
    }

    /// <summary>
    /// Ends the visit of <see cref="currentFrame"/> by writing it in the StackMapTable entries and by
    /// updating the StackMapTable number_of_entries (except if the current frame is the first one,
    /// which is implicit in StackMapTable). Then resets <see cref="currentFrame"/> to <c>null</c>.
    /// </summary>
    internal void VisitFrameEnd()
    {
        if (previousFrame != null)
        {
            if (stackMapTableEntries == null)
            {
                stackMapTableEntries = new ByteVector();
            }
            PutFrame();
            ++stackMapTableNumberOfEntries;
        }
        previousFrame = currentFrame;
        currentFrame = null;
    }

    /// <summary>Compresses and writes <see cref="currentFrame"/> in a new StackMapTable entry.</summary>
    private void PutFrame()
    {
        int numLocal = currentFrame![1];
        int numStack = currentFrame[2];
        if (symbolTable.GetMajorVersion() < Opcodes.V1_6)
        {
            // Generate a StackMap attribute entry, which are always uncompressed.
            stackMapTableEntries!.PutShort(currentFrame[0]).PutShort(numLocal);
            PutAbstractTypes(3, 3 + numLocal);
            stackMapTableEntries.PutShort(numStack);
            PutAbstractTypes(3 + numLocal, 3 + numLocal + numStack);
            return;
        }
        int offsetDelta =
            stackMapTableNumberOfEntries == 0
                ? currentFrame[0]
                : currentFrame[0] - previousFrame![0] - 1;
        int previousNumlocal = previousFrame![1];
        int numLocalDelta = numLocal - previousNumlocal;
        int type = Frame.FULL_FRAME;
        if (numStack == 0)
        {
            switch (numLocalDelta)
            {
                case -3:
                case -2:
                case -1:
                    type = Frame.CHOP_FRAME;
                    break;
                case 0:
                    type = offsetDelta < 64 ? Frame.SAME_FRAME : Frame.SAME_FRAME_EXTENDED;
                    break;
                case 1:
                case 2:
                case 3:
                    type = Frame.APPEND_FRAME;
                    break;
                default:
                    // Keep the FULL_FRAME type.
                    break;
            }
        }
        else if (numLocalDelta == 0 && numStack == 1)
        {
            type =
                offsetDelta < 63
                    ? Frame.SAME_LOCALS_1_STACK_ITEM_FRAME
                    : Frame.SAME_LOCALS_1_STACK_ITEM_FRAME_EXTENDED;
        }
        if (type != Frame.FULL_FRAME)
        {
            // Verify if locals are the same as in the previous frame.
            int frameIndex = 3;
            for (int i = 0; i < previousNumlocal && i < numLocal; i++)
            {
                if (currentFrame[frameIndex] != previousFrame[frameIndex])
                {
                    type = Frame.FULL_FRAME;
                    break;
                }
                frameIndex++;
            }
        }
        switch (type)
        {
            case Frame.SAME_FRAME:
                stackMapTableEntries!.PutByte(offsetDelta);
                break;
            case Frame.SAME_LOCALS_1_STACK_ITEM_FRAME:
                stackMapTableEntries!.PutByte(Frame.SAME_LOCALS_1_STACK_ITEM_FRAME + offsetDelta);
                PutAbstractTypes(3 + numLocal, 4 + numLocal);
                break;
            case Frame.SAME_LOCALS_1_STACK_ITEM_FRAME_EXTENDED:
                stackMapTableEntries!
                    .PutByte(Frame.SAME_LOCALS_1_STACK_ITEM_FRAME_EXTENDED)
                    .PutShort(offsetDelta);
                PutAbstractTypes(3 + numLocal, 4 + numLocal);
                break;
            case Frame.SAME_FRAME_EXTENDED:
                stackMapTableEntries!.PutByte(Frame.SAME_FRAME_EXTENDED).PutShort(offsetDelta);
                break;
            case Frame.CHOP_FRAME:
                stackMapTableEntries!
                    .PutByte(Frame.SAME_FRAME_EXTENDED + numLocalDelta)
                    .PutShort(offsetDelta);
                break;
            case Frame.APPEND_FRAME:
                stackMapTableEntries!
                    .PutByte(Frame.SAME_FRAME_EXTENDED + numLocalDelta)
                    .PutShort(offsetDelta);
                PutAbstractTypes(3 + previousNumlocal, 3 + numLocal);
                break;
            case Frame.FULL_FRAME:
            default:
                stackMapTableEntries!.PutByte(Frame.FULL_FRAME).PutShort(offsetDelta).PutShort(numLocal);
                PutAbstractTypes(3, 3 + numLocal);
                stackMapTableEntries.PutShort(numStack);
                PutAbstractTypes(3 + numLocal, 3 + numLocal + numStack);
                break;
        }
    }

    /// <summary>
    /// Puts some abstract types of <see cref="currentFrame"/> in <see cref="stackMapTableEntries"/>,
    /// using the JVMS verification_type_info format used in StackMapTable attributes.
    /// </summary>
    /// <param name="start">index of the first type in <see cref="currentFrame"/> to write.</param>
    /// <param name="end">index of last type in <see cref="currentFrame"/> to write (exclusive).</param>
    private void PutAbstractTypes(int start, int end)
    {
        for (int i = start; i < end; ++i)
        {
            Frame.PutAbstractType(symbolTable, currentFrame![i], stackMapTableEntries!);
        }
    }

    /// <summary>
    /// Puts the given public API frame element type in <see cref="stackMapTableEntries"/>, using the
    /// JVMS verification_type_info format used in StackMapTable attributes.
    /// </summary>
    /// <param name="type">a frame element type described using the same format as in
    /// <see cref="MethodVisitor.VisitFrame"/>.</param>
    private void PutFrameType(object type)
    {
        if (type is int intValue)
        {
            stackMapTableEntries!.PutByte(intValue);
        }
        else if (type is string str)
        {
            stackMapTableEntries!
                .PutByte(Frame.ITEM_OBJECT)
                .PutShort(symbolTable.AddConstantClass(str).Index);
        }
        else
        {
            stackMapTableEntries!.PutByte(Frame.ITEM_UNINITIALIZED);
            ((Label)type).Put(stackMapTableEntries);
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Utility methods
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns whether the attributes of this method can be copied from the attributes of the given
    /// method (assuming there is no method visitor between the given ClassReader and this
    /// MethodWriter).
    /// </summary>
    /// <param name="source">the source ClassReader from which the attributes of this method might be copied.</param>
    /// <param name="hasSyntheticAttribute">whether the method_info JVMS structure from which the attributes
    /// of this method might be copied contains a Synthetic attribute.</param>
    /// <param name="hasDeprecatedAttribute">whether the method_info JVMS structure from which the attributes
    /// of this method might be copied contains a Deprecated attribute.</param>
    /// <param name="descriptorIndex">the descriptor_index field of the method_info JVMS structure from which
    /// the attributes of this method might be copied.</param>
    /// <param name="signatureIndex">the constant pool index contained in the Signature attribute of the
    /// method_info JVMS structure from which the attributes of this method might be copied, or 0.</param>
    /// <param name="exceptionsOffset">the offset in 'source.b' of the Exceptions attribute of the method_info
    /// JVMS structure from which the attributes of this method might be copied, or 0.</param>
    /// <returns>whether the attributes of this method can be copied from the attributes of the
    /// method_info JVMS structure in 'source.b', between 'methodInfoOffset' and 'methodInfoOffset'
    /// + 'methodInfoLength'.</returns>
    internal bool CanCopyMethodAttributes(
        object? source,
        bool hasSyntheticAttribute,
        bool hasDeprecatedAttribute,
        int descriptorIndex,
        int signatureIndex,
        int exceptionsOffset)
    {
        // 属性复制优化未移植：返回 false 强制 ClassReader 走重新解析路径。
        return false;
    }

    /// <summary>
    /// Sets the source from which the attributes of this method will be copied.
    /// </summary>
    /// <param name="methodInfoOffset">the offset in 'symbolTable.GetSource()' of the method_info JVMS
    /// structure from which the attributes of this method will be copied.</param>
    /// <param name="methodInfoLength">the length in 'symbolTable.GetSource()' of the method_info JVMS
    /// structure from which the attributes of this method will be copied.</param>
    internal void SetMethodAttributesSource(int methodInfoOffset, int methodInfoLength)
    {
        // Don't copy the attributes yet, instead store their location in the source class reader so
        // they can be copied later, in PutMethodInfo. Note that we skip the 6 header bytes of the
        // method_info JVMS structure.
        this.sourceOffset = methodInfoOffset + 6;
        this.sourceLength = methodInfoLength - 6;
    }

    /// <summary>
    /// Returns the size of the method_info JVMS structure generated by this MethodWriter. Also add the
    /// names of the attributes of this method in the constant pool.
    /// </summary>
    /// <returns>the size in bytes of the method_info JVMS structure.</returns>
    internal int ComputeMethodInfoSize()
    {
        // If this method_info must be copied from an existing one, the size computation is trivial.
        if (sourceOffset != 0)
        {
            // sourceLength excludes the first 6 bytes for access_flags, name_index and descriptor_index.
            return 6 + sourceLength;
        }
        // 2 bytes each for access_flags, name_index, descriptor_index and attributes_count.
        int size = 8;
        // For ease of reference, we use here the same attribute order as in Section 4.7 of the JVMS.
        if (code.Length > 0)
        {
            if (code.Length > 65535)
            {
                throw new InvalidOperationException(
                    "Method too large: " + symbolTable.GetClassName() + "." + name + descriptor);
            }
            symbolTable.AddConstantUtf8(Constants.CODE);
            // The Code attribute has 6 header bytes, plus 2, 2, 4 and 2 bytes respectively for max_stack,
            // max_locals, code_length and attributes_count, plus the bytecode and the exception table.
            size += 16 + code.Length + Handler.GetExceptionTableSize(firstHandler);
            if (stackMapTableEntries != null)
            {
                bool useStackMapTable = symbolTable.GetMajorVersion() >= Opcodes.V1_6;
                symbolTable.AddConstantUtf8(useStackMapTable ? Constants.STACK_MAP_TABLE : "StackMap");
                // 6 header bytes and 2 bytes for number_of_entries.
                size += 8 + stackMapTableEntries.Length;
            }
            if (lineNumberTable != null)
            {
                symbolTable.AddConstantUtf8(Constants.LINE_NUMBER_TABLE);
                // 6 header bytes and 2 bytes for line_number_table_length.
                size += 8 + lineNumberTable.Length;
            }
            if (localVariableTable != null)
            {
                symbolTable.AddConstantUtf8(Constants.LOCAL_VARIABLE_TABLE);
                // 6 header bytes and 2 bytes for local_variable_table_length.
                size += 8 + localVariableTable.Length;
            }
            if (localVariableTypeTable != null)
            {
                symbolTable.AddConstantUtf8(Constants.LOCAL_VARIABLE_TYPE_TABLE);
                // 6 header bytes and 2 bytes for local_variable_type_table_length.
                size += 8 + localVariableTypeTable.Length;
            }
            // TODO: port AnnotationWriter — code type annotation checks skipped (fields always null)
            if (firstCodeAttribute != null)
            {
                size +=
                    firstCodeAttribute.ComputeAttributesSize(
                        symbolTable, code.Data, code.Length, maxStack, maxLocals);
            }
        }
        if (numberOfExceptions > 0)
        {
            symbolTable.AddConstantUtf8(Constants.EXCEPTIONS);
            size += 8 + 2 * numberOfExceptions;
        }
        size += Attribute.ComputeAttributesSize(symbolTable, accessFlags, signatureIndex);
        // TODO: port AnnotationWriter — annotation/parameter checks skipped (fields always null)
        if (defaultValue != null)
        {
            symbolTable.AddConstantUtf8(Constants.ANNOTATION_DEFAULT);
            size += 6 + defaultValue.Length;
        }
        if (parameters != null)
        {
            symbolTable.AddConstantUtf8(Constants.METHOD_PARAMETERS);
            // 6 header bytes and 1 byte for parameters_count.
            size += 7 + parameters.Length;
        }
        if (firstAttribute != null)
        {
            size += firstAttribute.ComputeAttributesSize(symbolTable);
        }
        return size;
    }

    /// <summary>
    /// Puts the content of the method_info JVMS structure generated by this MethodWriter into the
    /// given ByteVector.
    /// </summary>
    /// <param name="output">where the method_info structure must be put.</param>
    internal void PutMethodInfo(ByteVector output)
    {
        bool useSyntheticAttribute = symbolTable.GetMajorVersion() < Opcodes.V1_5;
        int mask = useSyntheticAttribute ? Opcodes.ACC_SYNTHETIC : 0;
        output.PutShort(accessFlags & ~mask).PutShort(nameIndex).PutShort(descriptorIndex);
        // If this method_info must be copied from an existing one, copy it now and return early.
        if (sourceOffset != 0)
        {
            // TODO: ClassReader not ported yet. Source copy requires ClassReader.classFileBuffer.
            throw new NotImplementedException("ClassReader not ported yet.");
        }
        // For ease of reference, we use here the same attribute order as in Section 4.7 of the JVMS.
        int attributeCount = 0;
        if (code.Length > 0)
        {
            ++attributeCount;
        }
        if (numberOfExceptions > 0)
        {
            ++attributeCount;
        }
        if ((accessFlags & Opcodes.ACC_SYNTHETIC) != 0 && useSyntheticAttribute)
        {
            ++attributeCount;
        }
        if (signatureIndex != 0)
        {
            ++attributeCount;
        }
        if ((accessFlags & Opcodes.ACC_DEPRECATED) != 0)
        {
            ++attributeCount;
        }
        // TODO: port AnnotationWriter — method annotation checks skipped (fields always null)
        if (defaultValue != null)
        {
            ++attributeCount;
        }
        if (parameters != null)
        {
            ++attributeCount;
        }
        if (firstAttribute != null)
        {
            attributeCount += firstAttribute.GetAttributeCount();
        }
        // For ease of reference, we use here the same attribute order as in Section 4.7 of the JVMS.
        output.PutShort(attributeCount);
        if (code.Length > 0)
        {
            // 2, 2, 4 and 2 bytes respectively for max_stack, max_locals, code_length and
            // attributes_count, plus the bytecode and the exception table.
            int size = 10 + code.Length + Handler.GetExceptionTableSize(firstHandler);
            int codeAttributeCount = 0;
            if (stackMapTableEntries != null)
            {
                // 6 header bytes and 2 bytes for number_of_entries.
                size += 8 + stackMapTableEntries.Length;
                ++codeAttributeCount;
            }
            if (lineNumberTable != null)
            {
                // 6 header bytes and 2 bytes for line_number_table_length.
                size += 8 + lineNumberTable.Length;
                ++codeAttributeCount;
            }
            if (localVariableTable != null)
            {
                // 6 header bytes and 2 bytes for local_variable_table_length.
                size += 8 + localVariableTable.Length;
                ++codeAttributeCount;
            }
            if (localVariableTypeTable != null)
            {
                // 6 header bytes and 2 bytes for local_variable_type_table_length.
                size += 8 + localVariableTypeTable.Length;
                ++codeAttributeCount;
            }
            // TODO: port AnnotationWriter — code type annotation checks skipped (fields always null)
            if (firstCodeAttribute != null)
            {
                size +=
                    firstCodeAttribute.ComputeAttributesSize(
                        symbolTable, code.Data, code.Length, maxStack, maxLocals);
                codeAttributeCount += firstCodeAttribute.GetAttributeCount();
            }
            output
                .PutShort(symbolTable.AddConstantUtf8(Constants.CODE))
                .PutInt(size)
                .PutShort(maxStack)
                .PutShort(maxLocals)
                .PutInt(code.Length)
                .PutByteArray(code.Data, 0, code.Length);
            Handler.PutExceptionTable(firstHandler, output);
            output.PutShort(codeAttributeCount);
            if (stackMapTableEntries != null)
            {
                bool useStackMapTable = symbolTable.GetMajorVersion() >= Opcodes.V1_6;
                output
                    .PutShort(
                        symbolTable.AddConstantUtf8(
                            useStackMapTable ? Constants.STACK_MAP_TABLE : "StackMap"))
                    .PutInt(2 + stackMapTableEntries.Length)
                    .PutShort(stackMapTableNumberOfEntries)
                    .PutByteArray(stackMapTableEntries.Data, 0, stackMapTableEntries.Length);
            }
            if (lineNumberTable != null)
            {
                output
                    .PutShort(symbolTable.AddConstantUtf8(Constants.LINE_NUMBER_TABLE))
                    .PutInt(2 + lineNumberTable.Length)
                    .PutShort(lineNumberTableLength)
                    .PutByteArray(lineNumberTable.Data, 0, lineNumberTable.Length);
            }
            if (localVariableTable != null)
            {
                output
                    .PutShort(symbolTable.AddConstantUtf8(Constants.LOCAL_VARIABLE_TABLE))
                    .PutInt(2 + localVariableTable.Length)
                    .PutShort(localVariableTableLength)
                    .PutByteArray(localVariableTable.Data, 0, localVariableTable.Length);
            }
            if (localVariableTypeTable != null)
            {
                output
                    .PutShort(symbolTable.AddConstantUtf8(Constants.LOCAL_VARIABLE_TYPE_TABLE))
                    .PutInt(2 + localVariableTypeTable.Length)
                    .PutShort(localVariableTypeTableLength)
                    .PutByteArray(localVariableTypeTable.Data, 0, localVariableTypeTable.Length);
            }
            // TODO: port AnnotationWriter — code type annotation checks skipped (fields always null)
            if (firstCodeAttribute != null)
            {
                firstCodeAttribute.PutAttributes(
                    symbolTable, code.Data, code.Length, maxStack, maxLocals, output);
            }
        }
        if (numberOfExceptions > 0)
        {
            output
                .PutShort(symbolTable.AddConstantUtf8(Constants.EXCEPTIONS))
                .PutInt(2 + 2 * numberOfExceptions)
                .PutShort(numberOfExceptions);
            foreach (int exceptionIndex in exceptionIndexTable!)
            {
                output.PutShort(exceptionIndex);
            }
        }
        Attribute.PutAttributes(symbolTable, accessFlags, signatureIndex, output);
        // TODO: port AnnotationWriter — annotation/parameter checks skipped (fields always null)
        if (defaultValue != null)
        {
            output
                .PutShort(symbolTable.AddConstantUtf8(Constants.ANNOTATION_DEFAULT))
                .PutInt(defaultValue.Length)
                .PutByteArray(defaultValue.Data, 0, defaultValue.Length);
        }
        if (parameters != null)
        {
            output
                .PutShort(symbolTable.AddConstantUtf8(Constants.METHOD_PARAMETERS))
                .PutInt(1 + parameters.Length)
                .PutByte(parametersCount)
                .PutByteArray(parameters.Data, 0, parameters.Length);
        }
        if (firstAttribute != null)
        {
            firstAttribute.PutAttributes(symbolTable, output);
        }
    }

    /// <summary>
    /// Collects the attributes of this method into the given set of attribute prototypes.
    /// </summary>
    /// <param name="attributePrototypes">a set of attribute prototypes.</param>
    internal void CollectAttributePrototypes(Attribute.Set attributePrototypes)
    {
        attributePrototypes.AddAttributes(firstAttribute);
        attributePrototypes.AddAttributes(firstCodeAttribute);
    }
}
