// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET. 

namespace Jvm.NET.Asm;

/// <summary>
/// The JVM opcodes, access flags and array type codes.
/// Java class file version constants and ASM API versions are also defined here.
/// </summary>
public static class Opcodes
{
    // ---- ASM API versions ----

    public const int ASM4 = 4 << 16 | 0 << 8;
    public const int ASM5 = 5 << 16 | 0 << 8;
    public const int ASM6 = 6 << 16 | 0 << 8;
    public const int ASM7 = 7 << 16 | 0 << 8;
    public const int ASM8 = 8 << 16 | 0 << 8;
    public const int ASM9 = 9 << 16 | 0 << 8;

    /// <summary>
    /// Experimental API version for ASM10. This API is experimental and may change
    /// in incompatible ways in future releases.
    /// </summary>
    [Obsolete("This API is experimental.")]
    public const int ASM10_EXPERIMENTAL = 1 << 24 | 10 << 16 | 0 << 8;

    // ---- Internal source flags for deprecated method redirection ----

    public const int SOURCE_DEPRECATED = 0x100;
    public const int SOURCE_MASK = SOURCE_DEPRECATED;

    // ---- Java ClassFile versions ----
    // Layout: (minor << 16) | major

    public const int V1_1 = 3 << 16 | 45;
    public const int V1_2 = 0 << 16 | 46;
    public const int V1_3 = 0 << 16 | 47;
    public const int V1_4 = 0 << 16 | 48;
    public const int V1_5 = 0 << 16 | 49;
    public const int V1_6 = 0 << 16 | 50;
    public const int V1_7 = 0 << 16 | 51;
    public const int V1_8 = 0 << 16 | 52;
    public const int V9 = 0 << 16 | 53;
    public const int V10 = 0 << 16 | 54;
    public const int V11 = 0 << 16 | 55;
    public const int V12 = 0 << 16 | 56;
    public const int V13 = 0 << 16 | 57;
    public const int V14 = 0 << 16 | 58;
    public const int V15 = 0 << 16 | 59;
    public const int V16 = 0 << 16 | 60;
    public const int V17 = 0 << 16 | 61;
    public const int V18 = 0 << 16 | 62;
    public const int V19 = 0 << 16 | 63;
    public const int V20 = 0 << 16 | 64;
    public const int V21 = 0 << 16 | 65;
    public const int V22 = 0 << 16 | 66;
    public const int V23 = 0 << 16 | 67;
    public const int V24 = 0 << 16 | 68;
    public const int V25 = 0 << 16 | 69;
    public const int V26 = 0 << 16 | 70;
    public const int V27 = 0 << 16 | 71;

    /// <summary>Version flag indicating that the class is using 'preview' features.</summary>
    public const int V_PREVIEW = -65536; // 0xFFFF0000

    // ---- Access flags ----

    public const int ACC_PUBLIC = 0x0001;
    public const int ACC_PRIVATE = 0x0002;
    public const int ACC_PROTECTED = 0x0004;
    public const int ACC_STATIC = 0x0008;
    public const int ACC_FINAL = 0x0010;
    public const int ACC_SUPER = 0x0020;
    public const int ACC_SYNCHRONIZED = 0x0020;
    public const int ACC_OPEN = 0x0020;
    public const int ACC_TRANSITIVE = 0x0020;
    public const int ACC_VOLATILE = 0x0040;
    public const int ACC_BRIDGE = 0x0040;
    public const int ACC_STATIC_PHASE = 0x0040;
    public const int ACC_VARARGS = 0x0080;
    public const int ACC_TRANSIENT = 0x0080;
    public const int ACC_NATIVE = 0x0100;
    public const int ACC_INTERFACE = 0x0200;
    public const int ACC_ABSTRACT = 0x0400;
    public const int ACC_STRICT = 0x0800;
    public const int ACC_SYNTHETIC = 0x1000;
    public const int ACC_ANNOTATION = 0x2000;
    public const int ACC_ENUM = 0x4000;
    public const int ACC_MANDATED = 0x8000;
    public const int ACC_MODULE = 0x8000;

    // ---- ASM specific access flags (stored in 16+ bits, auto-filtered on write) ----

    public const int ACC_RECORD = 0x10000;
    public const int ACC_DEPRECATED = 0x20000;

    // ---- NEWARRAY type codes ----

    public const int T_BOOLEAN = 4;
    public const int T_CHAR = 5;
    public const int T_FLOAT = 6;
    public const int T_DOUBLE = 7;
    public const int T_BYTE = 8;
    public const int T_SHORT = 9;
    public const int T_INT = 10;
    public const int T_LONG = 11;

    // ---- MethodHandle reference kinds ----

    public const int H_GETFIELD = 1;
    public const int H_GETSTATIC = 2;
    public const int H_PUTFIELD = 3;
    public const int H_PUTSTATIC = 4;
    public const int H_INVOKEVIRTUAL = 5;
    public const int H_INVOKESTATIC = 6;
    public const int H_INVOKESPECIAL = 7;
    public const int H_NEWINVOKESPECIAL = 8;
    public const int H_INVOKEINTERFACE = 9;

    // ---- ASM stack map frame types ----

    public const int F_NEW = -1;
    public const int F_FULL = 0;
    public const int F_APPEND = 1;
    public const int F_CHOP = 2;
    public const int F_SAME = 3;
    public const int F_SAME1 = 4;

    // ---- Stack map frame element types ----

    public const int TOP = 0;
    public const int INTEGER = 1;
    public const int FLOAT = 2;
    public const int DOUBLE = 3;
    public const int LONG = 4;
    public const int NULL = 5;
    public const int UNINITIALIZED_THIS = 6;

    // ---- JVM opcodes ----

    public const int NOP = 0;
    public const int ACONST_NULL = 1;
    public const int ICONST_M1 = 2;
    public const int ICONST_0 = 3;
    public const int ICONST_1 = 4;
    public const int ICONST_2 = 5;
    public const int ICONST_3 = 6;
    public const int ICONST_4 = 7;
    public const int ICONST_5 = 8;
    public const int LCONST_0 = 9;
    public const int LCONST_1 = 10;
    public const int FCONST_0 = 11;
    public const int FCONST_1 = 12;
    public const int FCONST_2 = 13;
    public const int DCONST_0 = 14;
    public const int DCONST_1 = 15;
    public const int BIPUSH = 16;
    public const int SIPUSH = 17;
    public const int LDC = 18;
    public const int ILOAD = 21;
    public const int LLOAD = 22;
    public const int FLOAD = 23;
    public const int DLOAD = 24;
    public const int ALOAD = 25;
    public const int IALOAD = 46;
    public const int LALOAD = 47;
    public const int FALOAD = 48;
    public const int DALOAD = 49;
    public const int AALOAD = 50;
    public const int BALOAD = 51;
    public const int CALOAD = 52;
    public const int SALOAD = 53;
    public const int ISTORE = 54;
    public const int LSTORE = 55;
    public const int FSTORE = 56;
    public const int DSTORE = 57;
    public const int ASTORE = 58;
    public const int IASTORE = 79;
    public const int LASTORE = 80;
    public const int FASTORE = 81;
    public const int DASTORE = 82;
    public const int AASTORE = 83;
    public const int BASTORE = 84;
    public const int CASTORE = 85;
    public const int SASTORE = 86;
    public const int POP = 87;
    public const int POP2 = 88;
    public const int DUP = 89;
    public const int DUP_X1 = 90;
    public const int DUP_X2 = 91;
    public const int DUP2 = 92;
    public const int DUP2_X1 = 93;
    public const int DUP2_X2 = 94;
    public const int SWAP = 95;
    public const int IADD = 96;
    public const int LADD = 97;
    public const int FADD = 98;
    public const int DADD = 99;
    public const int ISUB = 100;
    public const int LSUB = 101;
    public const int FSUB = 102;
    public const int DSUB = 103;
    public const int IMUL = 104;
    public const int LMUL = 105;
    public const int FMUL = 106;
    public const int DMUL = 107;
    public const int IDIV = 108;
    public const int LDIV = 109;
    public const int FDIV = 110;
    public const int DDIV = 111;
    public const int IREM = 112;
    public const int LREM = 113;
    public const int FREM = 114;
    public const int DREM = 115;
    public const int INEG = 116;
    public const int LNEG = 117;
    public const int FNEG = 118;
    public const int DNEG = 119;
    public const int ISHL = 120;
    public const int LSHL = 121;
    public const int ISHR = 122;
    public const int LSHR = 123;
    public const int IUSHR = 124;
    public const int LUSHR = 125;
    public const int IAND = 126;
    public const int LAND = 127;
    public const int IOR = 128;
    public const int LOR = 129;
    public const int IXOR = 130;
    public const int LXOR = 131;
    public const int IINC = 132;
    public const int I2L = 133;
    public const int I2F = 134;
    public const int I2D = 135;
    public const int L2I = 136;
    public const int L2F = 137;
    public const int L2D = 138;
    public const int F2I = 139;
    public const int F2L = 140;
    public const int F2D = 141;
    public const int D2I = 142;
    public const int D2L = 143;
    public const int D2F = 144;
    public const int I2B = 145;
    public const int I2C = 146;
    public const int I2S = 147;
    public const int LCMP = 148;
    public const int FCMPL = 149;
    public const int FCMPG = 150;
    public const int DCMPL = 151;
    public const int DCMPG = 152;
    public const int IFEQ = 153;
    public const int IFNE = 154;
    public const int IFLT = 155;
    public const int IFGE = 156;
    public const int IFGT = 157;
    public const int IFLE = 158;
    public const int IF_ICMPEQ = 159;
    public const int IF_ICMPNE = 160;
    public const int IF_ICMPLT = 161;
    public const int IF_ICMPGE = 162;
    public const int IF_ICMPGT = 163;
    public const int IF_ICMPLE = 164;
    public const int IF_ACMPEQ = 165;
    public const int IF_ACMPNE = 166;
    public const int GOTO = 167;
    public const int JSR = 168;
    public const int RET = 169;
    public const int TABLESWITCH = 170;
    public const int LOOKUPSWITCH = 171;
    public const int IRETURN = 172;
    public const int LRETURN = 173;
    public const int FRETURN = 174;
    public const int DRETURN = 175;
    public const int ARETURN = 176;
    public const int RETURN = 177;
    public const int GETSTATIC = 178;
    public const int PUTSTATIC = 179;
    public const int GETFIELD = 180;
    public const int PUTFIELD = 181;
    public const int INVOKEVIRTUAL = 182;
    public const int INVOKESPECIAL = 183;
    public const int INVOKESTATIC = 184;
    public const int INVOKEINTERFACE = 185;
    public const int INVOKEDYNAMIC = 186;
    public const int NEW = 187;
    public const int NEWARRAY = 188;
    public const int ANEWARRAY = 189;
    public const int ARRAYLENGTH = 190;
    public const int ATHROW = 191;
    public const int CHECKCAST = 192;
    public const int INSTANCEOF = 193;
    public const int MONITORENTER = 194;
    public const int MONITOREXIT = 195;
    public const int MULTIANEWARRAY = 197;
    public const int IFNULL = 198;
    public const int IFNONNULL = 199;
}
