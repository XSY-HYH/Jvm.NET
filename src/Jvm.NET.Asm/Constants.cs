// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

using System.IO;
using System.Text.RegularExpressions;

namespace Jvm.NET.Asm;

/// <summary>
/// Defines additional JVM opcodes, access flags and constants which are not part of the ASM public
/// API.
/// </summary>
/// <seealso href="https://docs.oracle.com/javase/specs/jvms/se11/html/jvms-6.html">JVMS 6</seealso>
internal sealed class Constants
{
    // The ClassFile attribute names, in the order they are defined in
    // https://docs.oracle.com/javase/specs/jvms/se11/html/jvms-4.html#jvms-4.7-300.

    internal const string CONSTANT_VALUE = "ConstantValue";
    internal const string CODE = "Code";
    internal const string STACK_MAP_TABLE = "StackMapTable";
    internal const string EXCEPTIONS = "Exceptions";
    internal const string INNER_CLASSES = "InnerClasses";
    internal const string ENCLOSING_METHOD = "EnclosingMethod";
    internal const string SYNTHETIC = "Synthetic";
    internal const string SIGNATURE = "Signature";
    internal const string SOURCE_FILE = "SourceFile";
    internal const string SOURCE_DEBUG_EXTENSION = "SourceDebugExtension";
    internal const string LINE_NUMBER_TABLE = "LineNumberTable";
    internal const string LOCAL_VARIABLE_TABLE = "LocalVariableTable";
    internal const string LOCAL_VARIABLE_TYPE_TABLE = "LocalVariableTypeTable";
    internal const string DEPRECATED = "Deprecated";
    internal const string RUNTIME_VISIBLE_ANNOTATIONS = "RuntimeVisibleAnnotations";
    internal const string RUNTIME_INVISIBLE_ANNOTATIONS = "RuntimeInvisibleAnnotations";
    internal const string RUNTIME_VISIBLE_PARAMETER_ANNOTATIONS = "RuntimeVisibleParameterAnnotations";
    internal const string RUNTIME_INVISIBLE_PARAMETER_ANNOTATIONS =
        "RuntimeInvisibleParameterAnnotations";
    internal const string RUNTIME_VISIBLE_TYPE_ANNOTATIONS = "RuntimeVisibleTypeAnnotations";
    internal const string RUNTIME_INVISIBLE_TYPE_ANNOTATIONS = "RuntimeInvisibleTypeAnnotations";
    internal const string ANNOTATION_DEFAULT = "AnnotationDefault";
    internal const string BOOTSTRAP_METHODS = "BootstrapMethods";
    internal const string METHOD_PARAMETERS = "MethodParameters";
    internal const string MODULE = "Module";
    internal const string MODULE_PACKAGES = "ModulePackages";
    internal const string MODULE_MAIN_CLASS = "ModuleMainClass";
    internal const string NEST_HOST = "NestHost";
    internal const string NEST_MEMBERS = "NestMembers";
    internal const string PERMITTED_SUBCLASSES = "PermittedSubclasses";
    internal const string RECORD = "Record";

    // ASM specific access flags.
    // WARNING: the 16 least significant bits must NOT be used, to avoid conflicts with standard
    // access flags, and also to make sure that these flags are automatically filtered out when
    // written in class files (because access flags are stored using 16 bits only).

    internal const int ACC_CONSTRUCTOR = 0x40000; // method access flag.

    // ASM specific stack map frame types, used in ClassVisitor.VisitFrame.

    /// <summary>
    /// A frame inserted between already existing frames. This internal stack map frame type (in
    /// addition to the ones declared in <see cref="Opcodes"/>) can only be used if the frame content can be
    /// computed from the previous existing frame and from the instructions between this existing frame
    /// and the inserted one, without any knowledge of the type hierarchy. This kind of frame is only
    /// used when an unconditional jump is inserted in a method while expanding an ASM specific
    /// instruction. Keep in sync with Opcodes.java.
    /// </summary>
    internal const int F_INSERT = 256;

    // The JVM opcode values which are not part of the ASM public API.
    // See https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-6.html.

    internal const int LDC_W = 19;
    internal const int LDC2_W = 20;
    internal const int ILOAD_0 = 26;
    internal const int ILOAD_1 = 27;
    internal const int ILOAD_2 = 28;
    internal const int ILOAD_3 = 29;
    internal const int LLOAD_0 = 30;
    internal const int LLOAD_1 = 31;
    internal const int LLOAD_2 = 32;
    internal const int LLOAD_3 = 33;
    internal const int FLOAD_0 = 34;
    internal const int FLOAD_1 = 35;
    internal const int FLOAD_2 = 36;
    internal const int FLOAD_3 = 37;
    internal const int DLOAD_0 = 38;
    internal const int DLOAD_1 = 39;
    internal const int DLOAD_2 = 40;
    internal const int DLOAD_3 = 41;
    internal const int ALOAD_0 = 42;
    internal const int ALOAD_1 = 43;
    internal const int ALOAD_2 = 44;
    internal const int ALOAD_3 = 45;
    internal const int ISTORE_0 = 59;
    internal const int ISTORE_1 = 60;
    internal const int ISTORE_2 = 61;
    internal const int ISTORE_3 = 62;
    internal const int LSTORE_0 = 63;
    internal const int LSTORE_1 = 64;
    internal const int LSTORE_2 = 65;
    internal const int LSTORE_3 = 66;
    internal const int FSTORE_0 = 67;
    internal const int FSTORE_1 = 68;
    internal const int FSTORE_2 = 69;
    internal const int FSTORE_3 = 70;
    internal const int DSTORE_0 = 71;
    internal const int DSTORE_1 = 72;
    internal const int DSTORE_2 = 73;
    internal const int DSTORE_3 = 74;
    internal const int ASTORE_0 = 75;
    internal const int ASTORE_1 = 76;
    internal const int ASTORE_2 = 77;
    internal const int ASTORE_3 = 78;
    internal const int WIDE = 196;
    internal const int GOTO_W = 200;
    internal const int JSR_W = 201;

    // Constants to convert between normal and wide jump instructions.

    // The delta between the GOTO_W and JSR_W opcodes and GOTO and JUMP.
    internal const int WIDE_JUMP_OPCODE_DELTA = GOTO_W - Opcodes.GOTO;

    // Constants to convert JVM opcodes to the equivalent ASM specific opcodes, and vice versa.

    // The delta between the ASM_IFEQ, ..., ASM_IF_ACMPNE, ASM_GOTO and ASM_JSR opcodes
    // and IFEQ, ..., IF_ACMPNE, GOTO and JSR.
    internal const int ASM_OPCODE_DELTA = 49;

    // The delta between the ASM_IFNULL and ASM_IFNONNULL opcodes and IFNULL and IFNONNULL.
    internal const int ASM_IFNULL_OPCODE_DELTA = 20;

    // ASM specific opcodes, used for long forward jump instructions.

    internal const int ASM_IFEQ = Opcodes.IFEQ + ASM_OPCODE_DELTA;
    internal const int ASM_IFNE = Opcodes.IFNE + ASM_OPCODE_DELTA;
    internal const int ASM_IFLT = Opcodes.IFLT + ASM_OPCODE_DELTA;
    internal const int ASM_IFGE = Opcodes.IFGE + ASM_OPCODE_DELTA;
    internal const int ASM_IFGT = Opcodes.IFGT + ASM_OPCODE_DELTA;
    internal const int ASM_IFLE = Opcodes.IFLE + ASM_OPCODE_DELTA;
    internal const int ASM_IF_ICMPEQ = Opcodes.IF_ICMPEQ + ASM_OPCODE_DELTA;
    internal const int ASM_IF_ICMPNE = Opcodes.IF_ICMPNE + ASM_OPCODE_DELTA;
    internal const int ASM_IF_ICMPLT = Opcodes.IF_ICMPLT + ASM_OPCODE_DELTA;
    internal const int ASM_IF_ICMPGE = Opcodes.IF_ICMPGE + ASM_OPCODE_DELTA;
    internal const int ASM_IF_ICMPGT = Opcodes.IF_ICMPGT + ASM_OPCODE_DELTA;
    internal const int ASM_IF_ICMPLE = Opcodes.IF_ICMPLE + ASM_OPCODE_DELTA;
    internal const int ASM_IF_ACMPEQ = Opcodes.IF_ACMPEQ + ASM_OPCODE_DELTA;
    internal const int ASM_IF_ACMPNE = Opcodes.IF_ACMPNE + ASM_OPCODE_DELTA;
    internal const int ASM_GOTO = Opcodes.GOTO + ASM_OPCODE_DELTA;
    internal const int ASM_JSR = Opcodes.JSR + ASM_OPCODE_DELTA;
    internal const int ASM_IFNULL = Opcodes.IFNULL + ASM_IFNULL_OPCODE_DELTA;
    internal const int ASM_IFNONNULL = Opcodes.IFNONNULL + ASM_IFNULL_OPCODE_DELTA;
    internal const int ASM_GOTO_W = 220;

    private Constants() { }

    internal static void CheckAsmExperimental(object caller)
    {
        System.Type callerType = caller.GetType();
        string internalName = callerType.FullName?.Replace('.', '/') ?? string.Empty;
        if (!IsWhitelisted(internalName))
        {
            CheckIsPreview(callerType.Assembly.GetManifestResourceStream(internalName + ".class"));
        }
    }

    internal static bool IsWhitelisted(string internalName)
    {
        if (!internalName.StartsWith("org/objectweb/asm/"))
        {
            return false;
        }
        string member = "(Annotation|Class|Field|Method|Module|RecordComponent|Signature)";
        return internalName.Contains("Test$")
            || Regex.IsMatch(
                internalName, "^(?:org/objectweb/asm/util/Trace" + member + "Visitor(\\$.*)?)$")
            || Regex.IsMatch(
                internalName, "^(?:org/objectweb/asm/util/Check" + member + "Adapter(\\$.*)?)$");
    }

    internal static void CheckIsPreview(Stream? classInputStream)
    {
        if (classInputStream == null)
        {
            throw new InvalidOperationException("Bytecode not available, can't check class version");
        }
        int minorVersion;
        try
        {
            using BinaryReader callerClassStream = new BinaryReader(classInputStream);
            callerClassStream.ReadInt32();
            minorVersion = callerClassStream.ReadUInt16();
        }
        catch (IOException ioe)
        {
            throw new InvalidOperationException("I/O error, can't check class version", ioe);
        }
        if (minorVersion != 0xFFFF)
        {
            throw new InvalidOperationException(
                "ASM10_EXPERIMENTAL can only be used by classes compiled with --enable-preview");
        }
    }
}
