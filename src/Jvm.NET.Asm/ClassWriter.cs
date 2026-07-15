// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

using Attribute = Jvm.NET.Asm.Attribute;

namespace Jvm.NET.Asm;

/// <summary>
/// A <see cref="ClassVisitor"/> that generates a corresponding ClassFile structure, as defined in the
/// Java Virtual Machine Specification (JVMS). It can be used alone, to generate a Java class "from
/// scratch", or with one or more <c>ClassReader</c> and adapter <c>ClassVisitor</c> to generate a
/// modified class from one or more existing Java classes.
/// </summary>
/// <seealso href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html">JVMS 4</seealso>
/// <para><b>Author:</b> Eric Bruneton</para>
public sealed class ClassWriter : ClassVisitor
{
    /// <summary>
    /// A flag to automatically compute the maximum stack size and the maximum number of local
    /// variables of methods. If this flag is set, then the arguments of the
    /// <see cref="MethodVisitor.VisitMaxs"/> method of the <see cref="MethodVisitor"/> returned by
    /// the <see cref="VisitMethod"/> method will be ignored, and computed automatically from the
    /// signature and the bytecode of each method.
    /// </summary>
    /// <remarks>
    /// For classes whose version is <see cref="Opcodes.V1_7"/> of more, this option requires valid
    /// stack map frames. The maximum stack size is then computed from these frames, and from the
    /// bytecode instructions in between. If stack map frames are not present or must be recomputed,
    /// used <see cref="COMPUTE_FRAMES"/> instead.
    /// </remarks>
    /// <seealso cref="ClassWriter(int)"/>
    public const int COMPUTE_MAXS = 1;

    /// <summary>
    /// A flag to automatically compute the stack map frames of methods from scratch. If this flag is
    /// set, then the calls to the <see cref="MethodVisitor.VisitFrame"/> method are ignored, and the
    /// stack map frames are recomputed from the methods bytecode. The arguments of the
    /// <see cref="MethodVisitor.VisitMaxs"/> method are also ignored and recomputed from the bytecode.
    /// In other words, <see cref="COMPUTE_FRAMES"/> implies <see cref="COMPUTE_MAXS"/>.
    /// </summary>
    /// <seealso cref="ClassWriter(int)"/>
    public const int COMPUTE_FRAMES = 2;

    /// <summary>
    /// The flags passed to the constructor. Must be zero or more of <see cref="COMPUTE_MAXS"/> and
    /// <see cref="COMPUTE_FRAMES"/>.
    /// </summary>
    private readonly int flags;

    // Note: fields are ordered as in the ClassFile structure, and those related to attributes are
    // ordered as in Section 4.7 of the JVMS.

    /// <summary>
    /// The minor_version and major_version fields of the JVMS ClassFile structure. minor_version is
    /// stored in the 16 most significant bits, and major_version in the 16 least significant bits.
    /// </summary>
    private int version;

    /// <summary>The symbol table for this class (contains the constant_pool and the BootstrapMethods).</summary>
    private readonly SymbolTable symbolTable;

    /// <summary>
    /// The access_flags field of the JVMS ClassFile structure. This field can contain ASM specific
    /// access flags, such as <see cref="Opcodes.ACC_DEPRECATED"/> or <see cref="Opcodes.ACC_RECORD"/>,
    /// which are removed when generating the ClassFile structure.
    /// </summary>
    private int accessFlags;

    /// <summary>The this_class field of the JVMS ClassFile structure.</summary>
    private int thisClass;

    /// <summary>The super_class field of the JVMS ClassFile structure.</summary>
    private int superClass;

    /// <summary>The interface_count field of the JVMS ClassFile structure.</summary>
    private int interfaceCount;

    /// <summary>The 'interfaces' array of the JVMS ClassFile structure.</summary>
    private int[]? interfaces;

    /// <summary>
    /// The fields of this class, stored in a linked list of <see cref="FieldWriter"/> linked via their
    /// <see cref="FieldVisitor.Fv"/> field. This field stores the first element of this list.
    /// </summary>
    private FieldWriter? firstField;

    /// <summary>
    /// The fields of this class, stored in a linked list of <see cref="FieldWriter"/> linked via their
    /// <see cref="FieldVisitor.Fv"/> field. This field stores the last element of this list.
    /// </summary>
    private FieldWriter? lastField;

    /// <summary>
    /// The methods of this class, stored in a linked list of <see cref="MethodWriter"/> linked via
    /// their <see cref="MethodVisitor.Mv"/> field. This field stores the first element of this list.
    /// </summary>
    private MethodWriter? firstMethod;

    /// <summary>
    /// The methods of this class, stored in a linked list of <see cref="MethodWriter"/> linked via
    /// their <see cref="MethodVisitor.Mv"/> field. This field stores the last element of this list.
    /// </summary>
    private MethodWriter? lastMethod;

    /// <summary>The number_of_classes field of the InnerClasses attribute, or 0.</summary>
    private int numberOfInnerClasses;

    /// <summary>The 'classes' array of the InnerClasses attribute, or <c>null</c>.</summary>
    private ByteVector? innerClasses;

    /// <summary>The class_index field of the EnclosingMethod attribute, or 0.</summary>
    private int enclosingClassIndex;

    /// <summary>The method_index field of the EnclosingMethod attribute.</summary>
    private int enclosingMethodIndex;

    /// <summary>The signature_index field of the Signature attribute, or 0.</summary>
    private int signatureIndex;

    /// <summary>The source_file_index field of the SourceFile attribute, or 0.</summary>
    private int sourceFileIndex;

    /// <summary>The debug_extension field of the SourceDebugExtension attribute, or <c>null</c>.</summary>
    private ByteVector? debugExtension;

    /// <summary>
    /// The last runtime visible annotation of this class. The previous ones can be accessed with the
    /// <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeVisibleAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime invisible annotation of this class. The previous ones can be accessed with the
    /// <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeInvisibleAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime visible type annotation of this class. The previous ones can be accessed with
    /// the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeVisibleTypeAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime invisible type annotation of this class. The previous ones can be accessed
    /// with the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeInvisibleTypeAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>The Module attribute of this class, or <c>null</c>.</summary>
    private object? moduleWriter; // TODO: Replace with ModuleWriter when ported

    /// <summary>The host_class_index field of the NestHost attribute, or 0.</summary>
    private int nestHostClassIndex;

    /// <summary>The number_of_classes field of the NestMembers attribute, or 0.</summary>
    private int numberOfNestMemberClasses;

    /// <summary>The 'classes' array of the NestMembers attribute, or <c>null</c>.</summary>
    private ByteVector? nestMemberClasses;

    /// <summary>The number_of_classes field of the PermittedSubclasses attribute, or 0.</summary>
    private int numberOfPermittedSubclasses;

    /// <summary>The 'classes' array of the PermittedSubclasses attribute, or <c>null</c>.</summary>
    private ByteVector? permittedSubclasses;

    /// <summary>
    /// The record components of this class, stored in a linked list of
    /// <c>RecordComponentWriter</c> linked via their <c>delegate</c> field. This field stores the
    /// first element of this list.
    /// </summary>
    private object? firstRecordComponent; // TODO: Replace with RecordComponentWriter when ported

    /// <summary>
    /// The record components of this class, stored in a linked list of
    /// <c>RecordComponentWriter</c> linked via their <c>delegate</c> field. This field stores the
    /// last element of this list.
    /// </summary>
    private object? lastRecordComponent; // TODO: Replace with RecordComponentWriter when ported

    /// <summary>
    /// The first non standard attribute of this class. The next ones can be accessed with the
    /// <see cref="Attribute.nextAttribute"/> field. May be <c>null</c>.
    /// </summary>
    /// <remarks>
    /// <b>WARNING</b>: this list stores the attributes in the <i>reverse</i> order of their visit.
    /// firstAttribute is actually the last attribute visited in <see cref="VisitAttribute"/>. The
    /// <see cref="ToByteArray"/> method writes the attributes in the order defined by this list,
    /// i.e. in the reverse order specified by the user.
    /// </remarks>
    private Attribute? firstAttribute;

    /// <summary>
    /// Indicates what must be automatically computed in <see cref="MethodWriter"/>. Must be one of
    /// <see cref="MethodWriter.COMPUTE_NOTHING"/>,
    /// <see cref="MethodWriter.COMPUTE_MAX_STACK_AND_LOCAL"/>,
    /// <see cref="MethodWriter.COMPUTE_MAX_STACK_AND_LOCAL_FROM_FRAMES"/>,
    /// <see cref="MethodWriter.COMPUTE_INSERTED_FRAMES"/>, or
    /// <see cref="MethodWriter.COMPUTE_ALL_FRAMES"/>.
    /// </summary>
    private int compute;

    // -----------------------------------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Constructs a new <see cref="ClassWriter"/> object.
    /// </summary>
    /// <param name="flags">option flags that can be used to modify the default behavior of this
    /// class. Must be zero or more of <see cref="COMPUTE_MAXS"/> and <see cref="COMPUTE_FRAMES"/>.
    /// </param>
    public ClassWriter(int flags)
        : this(null, flags)
    {
    }

    /// <summary>
    /// Constructs a new <see cref="ClassWriter"/> object and enables optimizations for "mostly add"
    /// bytecode transformations. These optimizations are the following:
    /// <list type="bullet">
    ///   <item>The constant pool and bootstrap methods from the original class are copied as is in
    ///       the new class, which saves time. New constant pool entries and new bootstrap methods
    ///       will be added at the end if necessary, but unused constant pool entries or bootstrap
    ///       methods <i>won't be removed</i>.</item>
    ///   <item>Methods that are not transformed are copied as is in the new class, directly from
    ///       the original class bytecode (i.e. without emitting visit events for all the method
    ///       instructions), which saves a <i>lot</i> of time. Untransformed methods are detected
    ///       by the fact that the <c>ClassReader</c> receives <see cref="MethodVisitor"/> objects
    ///       that come from a <see cref="ClassWriter"/> (and not from any other
    ///       <see cref="ClassVisitor"/> instance).</item>
    /// </list>
    /// </summary>
    /// <param name="classReader">the <c>ClassReader</c> used to read the original class. It will be
    /// used to copy the entire constant pool and bootstrap methods from the original class and also
    /// to copy other fragments of original bytecode where applicable.</param>
    /// <param name="flags">option flags that can be used to modify the default behavior of this
    /// class. Must be zero or more of <see cref="COMPUTE_MAXS"/> and <see cref="COMPUTE_FRAMES"/>.
    /// <i>These option flags do not affect methods that are copied as is in the new class. This
    /// means that neither the maximum stack size nor the stack frames will be computed for these
    /// methods</i>.</param>
    public ClassWriter(object? classReader, int flags) // TODO: ClassReader
        : base(Opcodes.ASM9)
    {
        this.flags = flags;
        symbolTable = classReader == null
            ? new SymbolTable(this)
            : new SymbolTable(this, classReader);
        SetFlags(flags);
    }

    // -----------------------------------------------------------------------------------------------
    // Accessors
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns true if all the given flags were passed to the constructor.
    /// </summary>
    /// <param name="flags">some option flags. Must be zero or more of <see cref="COMPUTE_MAXS"/> and
    /// <see cref="COMPUTE_FRAMES"/>.</param>
    /// <returns>true if all the given flags, or more, were passed to the constructor.</returns>
    public bool HasFlags(int flags)
    {
        return (this.flags & flags) == flags;
    }

    // -----------------------------------------------------------------------------------------------
    // Implementation of the ClassVisitor abstract class
    // -----------------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public override void Visit(
        int version,
        int access,
        string? name,
        string? signature,
        string? superName,
        string[]? interfaces)
    {
        this.version = version;
        this.accessFlags = access;
        this.thisClass = symbolTable.SetMajorVersionAndClassName(version & 0xFFFF, name!);
        if (signature != null)
        {
            this.signatureIndex = symbolTable.AddConstantUtf8(signature);
        }
        this.superClass = superName == null ? 0 : symbolTable.AddConstantClass(superName).Index;
        if (interfaces != null && interfaces.Length > 0)
        {
            interfaceCount = interfaces.Length;
            this.interfaces = new int[interfaceCount];
            for (int i = 0; i < interfaceCount; ++i)
            {
                this.interfaces[i] = symbolTable.AddConstantClass(interfaces[i]).Index;
            }
        }
        if (compute == MethodWriter.COMPUTE_MAX_STACK_AND_LOCAL && (version & 0xFFFF) >= Opcodes.V1_7)
        {
            compute = MethodWriter.COMPUTE_MAX_STACK_AND_LOCAL_FROM_FRAMES;
        }
    }

    /// <inheritdoc/>
    public override void VisitSource(string? source, string? debug)
    {
        if (source != null)
        {
            sourceFileIndex = symbolTable.AddConstantUtf8(source);
        }
        if (debug != null)
        {
            debugExtension = new ByteVector().EncodeUtf8(debug, 0, int.MaxValue);
        }
    }

    /// <inheritdoc/>
    public override ModuleVisitor? VisitModule(string? name, int access, string? version)
    {
        throw new NotImplementedException("ModuleWriter not ported yet.");
    }

    /// <inheritdoc/>
    public override void VisitNestHost(string? nestHost)
    {
        nestHostClassIndex = symbolTable.AddConstantClass(nestHost!).Index;
    }

    /// <inheritdoc/>
    public override void VisitOuterClass(string? owner, string? name, string? descriptor)
    {
        enclosingClassIndex = symbolTable.AddConstantClass(owner!).Index;
        if (name != null && descriptor != null)
        {
            enclosingMethodIndex = symbolTable.AddConstantNameAndType(name, descriptor);
        }
    }

    /// <inheritdoc/>
    public override AnnotationVisitor? VisitAnnotation(string? descriptor, bool visible)
    {
        return null; // TODO: port AnnotationWriter — annotations are dropped for now
    }

    /// <inheritdoc/>
    public override AnnotationVisitor? VisitTypeAnnotation(
        int typeRef, TypePath? typePath, string? descriptor, bool visible)
    {
        return null; // TODO: port AnnotationWriter — annotations are dropped for now
    }

    /// <inheritdoc/>
    public override void VisitAttribute(Attribute? attribute)
    {
        // Store the attributes in the <i>reverse</i> order of their visit by this method.
        attribute!.nextAttribute = firstAttribute;
        firstAttribute = attribute;
    }

    /// <inheritdoc/>
    public override void VisitNestMember(string? nestMember)
    {
        if (nestMemberClasses == null)
        {
            nestMemberClasses = new ByteVector();
        }
        ++numberOfNestMemberClasses;
        nestMemberClasses!.PutShort(symbolTable.AddConstantClass(nestMember!).Index);
    }

    /// <inheritdoc/>
    public override void VisitPermittedSubclass(string? permittedSubclass)
    {
        if (permittedSubclasses == null)
        {
            permittedSubclasses = new ByteVector();
        }
        ++numberOfPermittedSubclasses;
        permittedSubclasses!.PutShort(symbolTable.AddConstantClass(permittedSubclass!).Index);
    }

    /// <inheritdoc/>
    public override void VisitInnerClass(string? name, string? outerName, string? innerName, int access)
    {
        if (innerClasses == null)
        {
            innerClasses = new ByteVector();
        }
        // Section 4.7.6 of the JVMS states "Every CONSTANT_Class_info entry in the constant_pool
        // table which represents a class or interface C that is not a package member must have
        // exactly one corresponding entry in the classes array". To avoid duplicates we keep track
        // in the info field of the Symbol of each CONSTANT_Class_info entry C whether an inner
        // class entry has already been added for C. If so, we store the index of this inner class
        // entry (plus one) in the info field. This trick allows duplicate detection in O(1) time.
        Symbol nameSymbol = symbolTable.AddConstantClass(name!);
        if (nameSymbol.Info == 0)
        {
            ++numberOfInnerClasses;
            innerClasses!.PutShort(nameSymbol.Index);
            innerClasses.PutShort(outerName == null ? 0 : symbolTable.AddConstantClass(outerName).Index);
            innerClasses.PutShort(innerName == null ? 0 : symbolTable.AddConstantUtf8(innerName));
            innerClasses.PutShort(access);
            nameSymbol.Info = numberOfInnerClasses;
        }
    }

    /// <inheritdoc/>
    public override RecordComponentVisitor? VisitRecordComponent(
        string? name, string? descriptor, string? signature)
    {
        throw new NotImplementedException("RecordComponentWriter not ported yet.");
    }

    /// <inheritdoc/>
    public override FieldVisitor? VisitField(
        int access, string? name, string? descriptor, string? signature, object? value)
    {
        FieldWriter fieldWriter =
            new FieldWriter(symbolTable, access, name!, descriptor!, signature, value);
        if (firstField == null)
        {
            firstField = fieldWriter;
        }
        else
        {
            lastField!.NextField = fieldWriter;
        }
        lastField = fieldWriter;
        return fieldWriter;
    }

    /// <inheritdoc/>
    public override MethodVisitor? VisitMethod(
        int access, string? name, string? descriptor, string? signature, string[]? exceptions)
    {
        MethodWriter methodWriter =
            new MethodWriter(symbolTable, access, name!, descriptor!, signature, exceptions, compute);
        if (firstMethod == null)
        {
            firstMethod = methodWriter;
        }
        else
        {
            lastMethod!.NextMethod = methodWriter;
        }
        lastMethod = methodWriter;
        return methodWriter;
    }

    /// <inheritdoc/>
    public override void VisitEnd()
    {
        // Nothing to do.
    }

    // -----------------------------------------------------------------------------------------------
    // Other public methods
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the content of the class file that was built by this ClassWriter.
    /// </summary>
    /// <returns>the binary content of the JVMS ClassFile structure that was built by this
    /// ClassWriter.</returns>
    /// <exception cref="InvalidOperationException">if the constant pool of the class is too large or
    /// if the Code attribute of a method is too large.</exception>
    public byte[] ToByteArray()
    {
        // First step: compute the size in bytes of the ClassFile structure.
        // The magic field uses 4 bytes, 10 mandatory fields (minor_version, major_version,
        // constant_pool_count, access_flags, this_class, super_class, interfaces_count,
        // fields_count, methods_count and attributes_count) use 2 bytes each, and each interface
        // uses 2 bytes too.
        int size = 24 + 2 * interfaceCount;
        int fieldsCount = 0;
        FieldWriter? fieldWriter = firstField;
        while (fieldWriter != null)
        {
            ++fieldsCount;
            size += fieldWriter.ComputeFieldInfoSize();
            fieldWriter = fieldWriter.NextField;
        }
        int methodsCount = 0;
        MethodWriter? methodWriter = firstMethod;
        while (methodWriter != null)
        {
            ++methodsCount;
            size += methodWriter.ComputeMethodInfoSize();
            methodWriter = methodWriter.NextMethod;
        }

        // For ease of reference, we use here the same attribute order as in Section 4.7 of the JVMS.
        int attributesCount = 0;
        if (innerClasses != null)
        {
            ++attributesCount;
            size += 8 + innerClasses.Length;
            symbolTable.AddConstantUtf8(Constants.INNER_CLASSES);
        }
        if (enclosingClassIndex != 0)
        {
            ++attributesCount;
            size += 10;
            symbolTable.AddConstantUtf8(Constants.ENCLOSING_METHOD);
        }
        if ((accessFlags & Opcodes.ACC_SYNTHETIC) != 0 && (version & 0xFFFF) < Opcodes.V1_5)
        {
            ++attributesCount;
            size += 6;
            symbolTable.AddConstantUtf8(Constants.SYNTHETIC);
        }
        if (signatureIndex != 0)
        {
            ++attributesCount;
            size += 8;
            symbolTable.AddConstantUtf8(Constants.SIGNATURE);
        }
        if (sourceFileIndex != 0)
        {
            ++attributesCount;
            size += 8;
            symbolTable.AddConstantUtf8(Constants.SOURCE_FILE);
        }
        if (debugExtension != null)
        {
            ++attributesCount;
            size += 6 + debugExtension.Length;
            symbolTable.AddConstantUtf8(Constants.SOURCE_DEBUG_EXTENSION);
        }
        if ((accessFlags & Opcodes.ACC_DEPRECATED) != 0)
        {
            ++attributesCount;
            size += 6;
            symbolTable.AddConstantUtf8(Constants.DEPRECATED);
        }
        // TODO: Replace with AnnotationWriter when ported
        // if (lastRuntimeVisibleAnnotation != null) { ... }
        // if (lastRuntimeInvisibleAnnotation != null) { ... }
        // if (lastRuntimeVisibleTypeAnnotation != null) { ... }
        // if (lastRuntimeInvisibleTypeAnnotation != null) { ... }
        if (symbolTable.ComputeBootstrapMethodsSize() > 0)
        {
            ++attributesCount;
            size += symbolTable.ComputeBootstrapMethodsSize();
        }
        // TODO: Replace with ModuleWriter when ported
        // if (moduleWriter != null) { ... }
        if (nestHostClassIndex != 0)
        {
            ++attributesCount;
            size += 8;
            symbolTable.AddConstantUtf8(Constants.NEST_HOST);
        }
        if (nestMemberClasses != null)
        {
            ++attributesCount;
            size += 8 + nestMemberClasses.Length;
            symbolTable.AddConstantUtf8(Constants.NEST_MEMBERS);
        }
        if (permittedSubclasses != null)
        {
            ++attributesCount;
            size += 8 + permittedSubclasses.Length;
            symbolTable.AddConstantUtf8(Constants.PERMITTED_SUBCLASSES);
        }
        // TODO: Replace with RecordComponentWriter when ported
        // int recordComponentCount = 0;
        // int recordSize = 0;
        // if ((accessFlags & Opcodes.ACC_RECORD) != 0 || firstRecordComponent != null) { ... }
        if (firstAttribute != null)
        {
            attributesCount += firstAttribute.GetAttributeCount();
            size += firstAttribute.ComputeAttributesSize(symbolTable);
        }
        // IMPORTANT: this must be the last part of the ClassFile size computation, because the
        // previous statements can add attribute names to the constant pool, thereby changing its
        // size!
        size += symbolTable.GetConstantPoolLength();
        int constantPoolCount = symbolTable.GetConstantPoolCount();
        if (constantPoolCount > 0xFFFF)
        {
            throw new InvalidOperationException(
                "Class is too large: constant pool count=" + constantPoolCount
                + " for class " + symbolTable.GetClassName());
        }

        // Second step: allocate a ByteVector of the correct size (in order to avoid any array copy
        // in dynamic resizes) and fill it with the ClassFile content.
        ByteVector result = new ByteVector(size);
        result.PutInt(unchecked((int)0xCAFEBABE)).PutInt(version);
        symbolTable.PutConstantPool(result);
        int mask = (version & 0xFFFF) < Opcodes.V1_5 ? Opcodes.ACC_SYNTHETIC : 0;
        result.PutShort(accessFlags & ~mask).PutShort(thisClass).PutShort(superClass);
        result.PutShort(interfaceCount);
        for (int i = 0; i < interfaceCount; ++i)
        {
            result.PutShort(interfaces![i]);
        }
        result.PutShort(fieldsCount);
        fieldWriter = firstField;
        while (fieldWriter != null)
        {
            fieldWriter.PutFieldInfo(result);
            fieldWriter = fieldWriter.NextField;
        }
        result.PutShort(methodsCount);
        bool hasFrames = false;
        bool hasAsmInstructions = false;
        methodWriter = firstMethod;
        while (methodWriter != null)
        {
            hasFrames |= methodWriter.HasFrames();
            hasAsmInstructions |= methodWriter.HasAsmInstructions();
            methodWriter.PutMethodInfo(result);
            methodWriter = methodWriter.NextMethod;
        }
        // For ease of reference, we use here the same attribute order as in Section 4.7 of the JVMS.
        result.PutShort(attributesCount);
        if (innerClasses != null)
        {
            result
                .PutShort(symbolTable.AddConstantUtf8(Constants.INNER_CLASSES))
                .PutInt(innerClasses.Length + 2)
                .PutShort(numberOfInnerClasses)
                .PutByteArray(innerClasses.Data, 0, innerClasses.Length);
        }
        if (enclosingClassIndex != 0)
        {
            result
                .PutShort(symbolTable.AddConstantUtf8(Constants.ENCLOSING_METHOD))
                .PutInt(4)
                .PutShort(enclosingClassIndex)
                .PutShort(enclosingMethodIndex);
        }
        if ((accessFlags & Opcodes.ACC_SYNTHETIC) != 0 && (version & 0xFFFF) < Opcodes.V1_5)
        {
            result.PutShort(symbolTable.AddConstantUtf8(Constants.SYNTHETIC)).PutInt(0);
        }
        if (signatureIndex != 0)
        {
            result
                .PutShort(symbolTable.AddConstantUtf8(Constants.SIGNATURE))
                .PutInt(2)
                .PutShort(signatureIndex);
        }
        if (sourceFileIndex != 0)
        {
            result
                .PutShort(symbolTable.AddConstantUtf8(Constants.SOURCE_FILE))
                .PutInt(2)
                .PutShort(sourceFileIndex);
        }
        if (debugExtension != null)
        {
            int length = debugExtension.Length;
            result
                .PutShort(symbolTable.AddConstantUtf8(Constants.SOURCE_DEBUG_EXTENSION))
                .PutInt(length)
                .PutByteArray(debugExtension.Data, 0, length);
        }
        if ((accessFlags & Opcodes.ACC_DEPRECATED) != 0)
        {
            result.PutShort(symbolTable.AddConstantUtf8(Constants.DEPRECATED)).PutInt(0);
        }
        // TODO: Replace with AnnotationWriter when ported
        // AnnotationWriter.PutAnnotations(...)
        symbolTable.PutBootstrapMethods(result);
        // TODO: Replace with ModuleWriter when ported
        // if (moduleWriter != null) { moduleWriter.PutAttributes(result); }
        if (nestHostClassIndex != 0)
        {
            result
                .PutShort(symbolTable.AddConstantUtf8(Constants.NEST_HOST))
                .PutInt(2)
                .PutShort(nestHostClassIndex);
        }
        if (nestMemberClasses != null)
        {
            result
                .PutShort(symbolTable.AddConstantUtf8(Constants.NEST_MEMBERS))
                .PutInt(nestMemberClasses.Length + 2)
                .PutShort(numberOfNestMemberClasses)
                .PutByteArray(nestMemberClasses.Data, 0, nestMemberClasses.Length);
        }
        if (permittedSubclasses != null)
        {
            result
                .PutShort(symbolTable.AddConstantUtf8(Constants.PERMITTED_SUBCLASSES))
                .PutInt(permittedSubclasses.Length + 2)
                .PutShort(numberOfPermittedSubclasses)
                .PutByteArray(permittedSubclasses.Data, 0, permittedSubclasses.Length);
        }
        // TODO: Replace with RecordComponentWriter when ported
        // if ((accessFlags & Opcodes.ACC_RECORD) != 0 || firstRecordComponent != null) { ... }
        if (firstAttribute != null)
        {
            firstAttribute.PutAttributes(symbolTable, result);
        }

        // Third step: replace the ASM specific instructions, if any.
        if (hasAsmInstructions)
        {
            return ReplaceAsmInstructions(result.Data, hasFrames);
        }
        else
        {
            return result.Data;
        }
    }

    /// <summary>
    /// Returns the equivalent of the given class file, with the ASM specific instructions replaced
    /// with standard ones. This is done with a ClassReader -&gt; ClassWriter round trip.
    /// </summary>
    /// <param name="classFile">a class file containing ASM specific instructions, generated by this
    /// ClassWriter.</param>
    /// <param name="hasFrames">whether there is at least one stack map frames in 'classFile'.</param>
    /// <returns>an equivalent of 'classFile', with the ASM specific instructions replaced with
    /// standard ones.</returns>
    private byte[] ReplaceAsmInstructions(byte[] classFile, bool hasFrames)
    {
        throw new NotImplementedException("ClassReader not ported yet.");
    }

    /// <summary>
    /// Returns the prototypes of the attributes used by this class, its fields and its methods.
    /// </summary>
    /// <returns>the prototypes of the attributes used by this class, its fields and its
    /// methods.</returns>
    private Attribute[] GetAttributePrototypes()
    {
        Attribute.Set attributePrototypes = new Attribute.Set();
        attributePrototypes.AddAttributes(firstAttribute);
        FieldWriter? fieldWriter = firstField;
        while (fieldWriter != null)
        {
            fieldWriter.CollectAttributePrototypes(attributePrototypes);
            fieldWriter = fieldWriter.NextField;
        }
        MethodWriter? methodWriter = firstMethod;
        while (methodWriter != null)
        {
            methodWriter.CollectAttributePrototypes(attributePrototypes);
            methodWriter = methodWriter.NextMethod;
        }
        // TODO: Replace with RecordComponentWriter when ported
        // RecordComponentWriter recordComponentWriter = firstRecordComponent;
        // while (recordComponentWriter != null) { ... }
        return attributePrototypes.ToArray();
    }

    // -----------------------------------------------------------------------------------------------
    // Utility methods: constant pool management for Attribute sub classes
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds a number or string constant to the constant pool of the class being build. Does nothing
    /// if the constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="value">the value of the constant to be added to the constant pool. This
    /// parameter must be an <see cref="int"/>, a <see cref="float"/>, a <see cref="long"/>, a
    /// <see cref="double"/> or a <see cref="string"/>.</param>
    /// <returns>the index of a new or already existing constant item with the given value.</returns>
    public int NewConst(object? value)
    {
        return symbolTable.AddConstant(value).Index;
    }

    /// <summary>
    /// Adds an UTF8 string to the constant pool of the class being build. Does nothing if the
    /// constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="value">the String value.</param>
    /// <returns>the index of a new or already existing UTF8 item.</returns>
    public int NewUTF8(string value)
    {
        return symbolTable.AddConstantUtf8(value);
    }

    /// <summary>
    /// Adds a class reference to the constant pool of the class being build. Does nothing if the
    /// constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="value">the internal name of the class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <returns>the index of a new or already existing class reference item.</returns>
    public int NewClass(string value)
    {
        return symbolTable.AddConstantClass(value).Index;
    }

    /// <summary>
    /// Adds a method type reference to the constant pool of the class being build. Does nothing if
    /// the constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="methodDescriptor">method descriptor of the method type.</param>
    /// <returns>the index of a new or already existing method type reference item.</returns>
    public int NewMethodType(string methodDescriptor)
    {
        return symbolTable.AddConstantMethodType(methodDescriptor).Index;
    }

    /// <summary>
    /// Adds a module reference to the constant pool of the class being build. Does nothing if the
    /// constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="moduleName">name of the module.</param>
    /// <returns>the index of a new or already existing module reference item.</returns>
    public int NewModule(string moduleName)
    {
        return symbolTable.AddConstantModule(moduleName).Index;
    }

    /// <summary>
    /// Adds a package reference to the constant pool of the class being build. Does nothing if the
    /// constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="packageName">name of the package in its internal form.</param>
    /// <returns>the index of a new or already existing module reference item.</returns>
    public int NewPackage(string packageName)
    {
        return symbolTable.AddConstantPackage(packageName).Index;
    }

    /// <summary>
    /// Adds a handle to the constant pool of the class being build. Does nothing if the constant
    /// pool already contains a similar item. <i>This method is intended for <see cref="Attribute"/>
    /// sub classes, and is normally not needed by class generators or adapters.</i>
    /// </summary>
    /// <param name="tag">the kind of this handle. Must be <see cref="Opcodes.H_GETFIELD"/>,
    /// <see cref="Opcodes.H_GETSTATIC"/>, <see cref="Opcodes.H_PUTFIELD"/>,
    /// <see cref="Opcodes.H_PUTSTATIC"/>, <see cref="Opcodes.H_INVOKEVIRTUAL"/>,
    /// <see cref="Opcodes.H_INVOKESTATIC"/>, <see cref="Opcodes.H_INVOKESPECIAL"/>,
    /// <see cref="Opcodes.H_NEWINVOKESPECIAL"/> or <see cref="Opcodes.H_INVOKEINTERFACE"/>.</param>
    /// <param name="owner">the internal name of the field or method owner class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="name">the name of the field or method.</param>
    /// <param name="descriptor">the descriptor of the field or method.</param>
    /// <returns>the index of a new or already existing method type reference item.</returns>
    [Obsolete("This method is superseded by NewHandle(int, string, string, string, bool).")]
    public int NewHandle(int tag, string owner, string name, string descriptor)
    {
        return NewHandle(tag, owner, name, descriptor, tag == Opcodes.H_INVOKEINTERFACE);
    }

    /// <summary>
    /// Adds a handle to the constant pool of the class being build. Does nothing if the constant
    /// pool already contains a similar item. <i>This method is intended for <see cref="Attribute"/>
    /// sub classes, and is normally not needed by class generators or adapters.</i>
    /// </summary>
    /// <param name="tag">the kind of this handle. Must be <see cref="Opcodes.H_GETFIELD"/>,
    /// <see cref="Opcodes.H_GETSTATIC"/>, <see cref="Opcodes.H_PUTFIELD"/>,
    /// <see cref="Opcodes.H_PUTSTATIC"/>, <see cref="Opcodes.H_INVOKEVIRTUAL"/>,
    /// <see cref="Opcodes.H_INVOKESTATIC"/>, <see cref="Opcodes.H_INVOKESPECIAL"/>,
    /// <see cref="Opcodes.H_NEWINVOKESPECIAL"/> or <see cref="Opcodes.H_INVOKEINTERFACE"/>.</param>
    /// <param name="owner">the internal name of the field or method owner class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="name">the name of the field or method.</param>
    /// <param name="descriptor">the descriptor of the field or method.</param>
    /// <param name="isInterface">true if the owner is an interface.</param>
    /// <returns>the index of a new or already existing method type reference item.</returns>
    public int NewHandle(int tag, string owner, string name, string descriptor, bool isInterface)
    {
        return symbolTable.AddConstantMethodHandle(tag, owner, name, descriptor, isInterface).Index;
    }

    /// <summary>
    /// Adds a dynamic constant reference to the constant pool of the class being build. Does nothing
    /// if the constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="name">name of the invoked method.</param>
    /// <param name="descriptor">field descriptor of the constant type.</param>
    /// <param name="bootstrapMethodHandle">the bootstrap method.</param>
    /// <param name="bootstrapMethodArguments">the bootstrap method constant arguments.</param>
    /// <returns>the index of a new or already existing dynamic constant reference item.</returns>
    public int NewConstantDynamic(
        string name,
        string descriptor,
        Handle bootstrapMethodHandle,
        params object?[] bootstrapMethodArguments)
    {
        return symbolTable.AddConstantDynamic(
            name, descriptor, bootstrapMethodHandle, bootstrapMethodArguments).Index;
    }

    /// <summary>
    /// Adds an invokedynamic reference to the constant pool of the class being build. Does nothing
    /// if the constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="name">name of the invoked method.</param>
    /// <param name="descriptor">descriptor of the invoke method.</param>
    /// <param name="bootstrapMethodHandle">the bootstrap method.</param>
    /// <param name="bootstrapMethodArguments">the bootstrap method constant arguments.</param>
    /// <returns>the index of a new or already existing invokedynamic reference item.</returns>
    public int NewInvokeDynamic(
        string name,
        string descriptor,
        Handle bootstrapMethodHandle,
        params object?[] bootstrapMethodArguments)
    {
        return symbolTable.AddConstantInvokeDynamic(
            name, descriptor, bootstrapMethodHandle, bootstrapMethodArguments).Index;
    }

    /// <summary>
    /// Adds a field reference to the constant pool of the class being build. Does nothing if the
    /// constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="owner">the internal name of the field's owner class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="name">the field's name.</param>
    /// <param name="descriptor">the field's descriptor.</param>
    /// <returns>the index of a new or already existing field reference item.</returns>
    public int NewField(string owner, string name, string descriptor)
    {
        return symbolTable.AddConstantFieldref(owner, name, descriptor).Index;
    }

    /// <summary>
    /// Adds a method reference to the constant pool of the class being build. Does nothing if the
    /// constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="owner">the internal name of the method's owner class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="name">the method's name.</param>
    /// <param name="descriptor">the method's descriptor.</param>
    /// <param name="isInterface"><c>true</c> if <c>owner</c> is an interface.</param>
    /// <returns>the index of a new or already existing method reference item.</returns>
    public int NewMethod(string owner, string name, string descriptor, bool isInterface)
    {
        return symbolTable.AddConstantMethodref(owner, name, descriptor, isInterface).Index;
    }

    /// <summary>
    /// Adds a name and type to the constant pool of the class being build. Does nothing if the
    /// constant pool already contains a similar item. <i>This method is intended for
    /// <see cref="Attribute"/> sub classes, and is normally not needed by class generators or
    /// adapters.</i>
    /// </summary>
    /// <param name="name">a name.</param>
    /// <param name="descriptor">a type descriptor.</param>
    /// <returns>the index of a new or already existing name and type item.</returns>
    public int NewNameType(string name, string descriptor)
    {
        return symbolTable.AddConstantNameAndType(name, descriptor);
    }

    /// <summary>
    /// Changes the computation strategy of method properties like max stack size, max number of
    /// local variables, and frames.
    /// </summary>
    /// <remarks>
    /// <b>WARNING</b>: <see cref="SetFlags(int)"/> method changes the behavior of new method
    /// visitors returned from <see cref="VisitMethod(int, string?, string?, string?, string[]?)"/>.
    /// The behavior will be changed only after the next method visitor is returned. All the
    /// previously returned method visitors keep their previous behavior.
    /// </remarks>
    /// <param name="flags">option flags that can be used to modify the default behavior of this
    /// class. Must be zero or more of <see cref="COMPUTE_MAXS"/> and <see cref="COMPUTE_FRAMES"/>.
    /// </param>
    public void SetFlags(int flags)
    {
        if ((flags & COMPUTE_FRAMES) != 0)
        {
            compute = MethodWriter.COMPUTE_ALL_FRAMES;
        }
        else if ((flags & COMPUTE_MAXS) != 0)
        {
            compute = MethodWriter.COMPUTE_MAX_STACK_AND_LOCAL;
        }
        else
        {
            compute = MethodWriter.COMPUTE_NOTHING;
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Default method to compute common super classes when computing stack map frames
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the common super type of the two given types. The default implementation of this
    /// method <i>loads</i> the two given classes and uses the java.lang.Class methods to find the
    /// common super class. It can be overridden to compute this common super type in other ways, in
    /// particular without actually loading any class, or to take into account the class that is
    /// currently being generated by this ClassWriter, which can of course not be loaded since it is
    /// under construction.
    /// </summary>
    /// <param name="type1">the internal name of a class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="type2">the internal name of another class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <returns>the internal name of the common super class of the two given classes (see
    /// <see cref="Type.GetInternalName()"/>).</returns>
    protected string GetCommonSuperClass(string type1, string type2)
    {
        throw new NotImplementedException(
            "GetCommonSuperClass requires Java reflection (Class.forName) which is not available in .NET.");
    }

    /// <summary>
    /// Returns the <c>ClassLoader</c> to be used by the default implementation of
    /// <see cref="GetCommonSuperClass(string, string)"/>, that of this <see cref="ClassWriter"/>'s
    /// runtime type by default.
    /// </summary>
    /// <returns>ClassLoader</returns>
    protected object? GetClassLoader()
    {
        return null;
    }
}
