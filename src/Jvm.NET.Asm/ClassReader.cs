// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

using System.IO;

namespace Jvm.NET.Asm;

/// <summary>
/// A parser to make a <see cref="ClassVisitor"/> visit a ClassFile structure, as defined in the
/// Java Virtual Machine Specification (JVMS). This class parses the ClassFile content and calls the
/// appropriate visit methods of a given <see cref="ClassVisitor"/> for each field, method and
/// bytecode instruction encountered.
/// </summary>
/// <seealso href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html">JVMS 4</seealso>
public class ClassReader
{
    // TypeReference sort values, used internally until TypeReference is ported.
    private const int TYPE_REFERENCE_CLASS_TYPE_PARAMETER = 0x00;
    private const int TYPE_REFERENCE_METHOD_TYPE_PARAMETER = 0x01;
    private const int TYPE_REFERENCE_CLASS_EXTENDS = 0x10;
    private const int TYPE_REFERENCE_CLASS_TYPE_PARAMETER_BOUND = 0x11;
    private const int TYPE_REFERENCE_METHOD_TYPE_PARAMETER_BOUND = 0x12;
    private const int TYPE_REFERENCE_FIELD = 0x13;
    private const int TYPE_REFERENCE_METHOD_RETURN = 0x14;
    private const int TYPE_REFERENCE_METHOD_RECEIVER = 0x15;
    private const int TYPE_REFERENCE_METHOD_FORMAL_PARAMETER = 0x16;
    private const int TYPE_REFERENCE_THROWS = 0x17;
    private const int TYPE_REFERENCE_LOCAL_VARIABLE = 0x40;
    private const int TYPE_REFERENCE_RESOURCE_VARIABLE = 0x41;
    private const int TYPE_REFERENCE_EXCEPTION_PARAMETER = 0x42;
    private const int TYPE_REFERENCE_INSTANCEOF = 0x43;
    private const int TYPE_REFERENCE_NEW = 0x44;
    private const int TYPE_REFERENCE_CONSTRUCTOR_REFERENCE = 0x45;
    private const int TYPE_REFERENCE_METHOD_REFERENCE = 0x46;
    private const int TYPE_REFERENCE_CAST = 0x47;
    private const int TYPE_REFERENCE_CONSTRUCTOR_INVOCATION_TYPE_ARGUMENT = 0x48;
    private const int TYPE_REFERENCE_METHOD_INVOCATION_TYPE_ARGUMENT = 0x49;
    private const int TYPE_REFERENCE_CONSTRUCTOR_REFERENCE_TYPE_ARGUMENT = 0x4A;
    private const int TYPE_REFERENCE_METHOD_REFERENCE_TYPE_ARGUMENT = 0x4B;

    /// <summary>A flag to skip the Code attributes. If this flag is set the Code attributes are neither parsed nor visited.</summary>
    public const int SKIP_CODE = 1;

    /// <summary>
    /// A flag to skip the SourceFile, SourceDebugExtension, LocalVariableTable,
    /// LocalVariableTypeTable, LineNumberTable and MethodParameters attributes. If this flag is set
    /// these attributes are neither parsed nor visited.
    /// </summary>
    public const int SKIP_DEBUG = 2;

    /// <summary>
    /// A flag to skip the StackMap and StackMapTable attributes. If this flag is set these attributes
    /// are neither parsed nor visited (i.e. <see cref="MethodVisitor.VisitFrame"/> is not called).
    /// </summary>
    public const int SKIP_FRAMES = 4;

    /// <summary>
    /// A flag to expand the stack map frames. By default stack map frames are visited in their
    /// original format. If this flag is set, stack map frames are always visited in expanded format.
    /// </summary>
    public const int EXPAND_FRAMES = 8;

    /// <summary>
    /// A flag to expand the ASM specific instructions into an equivalent sequence of standard
    /// bytecode instructions.
    /// </summary>
    internal const int EXPAND_ASM_INSNS = 256;

    /// <summary>The maximum size of array to allocate.</summary>
    private const int MAX_BUFFER_SIZE = 1024 * 1024;

    /// <summary>The size of the temporary byte array used to read class input streams chunk by chunk.</summary>
    private const int INPUT_STREAM_DATA_CHUNK_SIZE = 4096;

    /// <summary>
    /// A byte array containing the JVMS ClassFile structure to be parsed.
    /// </summary>
    [Obsolete("Use ReadByte(int) and the other read methods instead.")]
    public readonly byte[] b;

    /// <summary>The offset in bytes of the ClassFile's access_flags field.</summary>
    public readonly int header;

    /// <summary>
    /// A byte array containing the JVMS ClassFile structure to be parsed. The content of this array
    /// must not be modified.
    /// </summary>
    internal readonly byte[] classFileBuffer;

    /// <summary>
    /// The offset in bytes, in <see cref="classFileBuffer"/>, of each cp_info entry of the
    /// ClassFile's constant_pool array, <i>plus one</i>.
    /// </summary>
    private readonly int[] cpInfoOffsets;

    /// <summary>
    /// The String objects corresponding to the CONSTANT_Utf8 constant pool items. This cache avoids
    /// multiple parsing of a given CONSTANT_Utf8 constant pool item.
    /// </summary>
    private readonly string[] constantUtf8Values;

    /// <summary>
    /// The ConstantDynamic objects corresponding to the CONSTANT_Dynamic constant pool items.
    /// </summary>
    private readonly ConstantDynamic[] constantDynamicValues;

    /// <summary>
    /// The start offsets in <see cref="classFileBuffer"/> of each element of the bootstrap_methods
    /// array (in the BootstrapMethods attribute).
    /// </summary>
    private readonly int[] bootstrapMethodOffsets;

    /// <summary>
    /// A conservative estimate of the maximum length of the strings contained in the constant pool
    /// of the class.
    /// </summary>
    private readonly int maxStringLength;

    // -----------------------------------------------------------------------------------------------
    // Constructors
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Constructs a new <see cref="ClassReader"/> object.
    /// </summary>
    /// <param name="classFile">the JVMS ClassFile structure to be read.</param>
    public ClassReader(byte[] classFile)
        : this(classFile, 0, classFile.Length)
    {
    }

    /// <summary>
    /// Constructs a new <see cref="ClassReader"/> object.
    /// </summary>
    /// <param name="classFileBuffer">a byte array containing the JVMS ClassFile structure to be read.</param>
    /// <param name="classFileOffset">the offset in byteBuffer of the first byte of the ClassFile to be read.</param>
    /// <param name="classFileLength">the length in bytes of the ClassFile to be read.</param>
    public ClassReader(byte[] classFileBuffer, int classFileOffset, int classFileLength)
        : this(classFileBuffer, classFileOffset, true)
    {
    }

    /// <summary>
    /// Constructs a new <see cref="ClassReader"/> object. <i>This internal constructor must not be
    /// exposed as a public API.</i>
    /// </summary>
    /// <param name="classFileBuffer">a byte array containing the JVMS ClassFile structure to be read.</param>
    /// <param name="classFileOffset">the offset in byteBuffer of the first byte of the ClassFile to be read.</param>
    /// <param name="checkClassVersion">whether to check the class version or not.</param>
    internal ClassReader(byte[] classFileBuffer, int classFileOffset, bool checkClassVersion)
    {
        this.classFileBuffer = classFileBuffer;
        this.b = classFileBuffer;
        // Check the class' major_version. This field is after the magic and minor_version fields,
        // which use 4 and 2 bytes respectively.
        if (checkClassVersion && ReadShort(classFileOffset + 6) > Opcodes.V27)
        {
            throw new ArgumentException(
                "Unsupported class file major version " + ReadShort(classFileOffset + 6));
        }
        // Create the constant pool arrays. The constant_pool_count field is after the magic,
        // minor_version and major_version fields, which use 4, 2 and 2 bytes respectively.
        int constantPoolCount = ReadUnsignedShort(classFileOffset + 8);
        cpInfoOffsets = new int[constantPoolCount];
        constantUtf8Values = new string[constantPoolCount];
        // Compute the offset of each constant pool entry, as well as a conservative estimate of the
        // maximum length of the constant pool strings.
        int currentCpInfoIndex = 1;
        int currentCpInfoOffset = classFileOffset + 10;
        int currentMaxStringLength = 0;
        bool hasBootstrapMethods = false;
        bool hasConstantDynamic = false;
        while (currentCpInfoIndex < constantPoolCount)
        {
            cpInfoOffsets[currentCpInfoIndex++] = currentCpInfoOffset + 1;
            int cpInfoSize;
            switch (classFileBuffer[currentCpInfoOffset])
            {
                case Symbol.CONSTANT_FIELDREF_TAG:
                case Symbol.CONSTANT_METHODREF_TAG:
                case Symbol.CONSTANT_INTERFACE_METHODREF_TAG:
                case Symbol.CONSTANT_INTEGER_TAG:
                case Symbol.CONSTANT_FLOAT_TAG:
                case Symbol.CONSTANT_NAME_AND_TYPE_TAG:
                    cpInfoSize = 5;
                    break;
                case Symbol.CONSTANT_DYNAMIC_TAG:
                    cpInfoSize = 5;
                    hasBootstrapMethods = true;
                    hasConstantDynamic = true;
                    break;
                case Symbol.CONSTANT_INVOKE_DYNAMIC_TAG:
                    cpInfoSize = 5;
                    hasBootstrapMethods = true;
                    break;
                case Symbol.CONSTANT_LONG_TAG:
                case Symbol.CONSTANT_DOUBLE_TAG:
                    cpInfoSize = 9;
                    currentCpInfoIndex++;
                    break;
                case Symbol.CONSTANT_UTF8_TAG:
                    cpInfoSize = 3 + ReadUnsignedShort(currentCpInfoOffset + 1);
                    if (cpInfoSize > currentMaxStringLength)
                    {
                        currentMaxStringLength = cpInfoSize;
                    }
                    break;
                case Symbol.CONSTANT_METHOD_HANDLE_TAG:
                    cpInfoSize = 4;
                    break;
                case Symbol.CONSTANT_CLASS_TAG:
                case Symbol.CONSTANT_STRING_TAG:
                case Symbol.CONSTANT_METHOD_TYPE_TAG:
                case Symbol.CONSTANT_PACKAGE_TAG:
                case Symbol.CONSTANT_MODULE_TAG:
                    cpInfoSize = 3;
                    break;
                default:
                    throw new ArgumentException();
            }
            currentCpInfoOffset += cpInfoSize;
        }
        maxStringLength = currentMaxStringLength;
        // The Classfile's access_flags field is just after the last constant pool entry.
        header = currentCpInfoOffset;

        // Allocate the cache of ConstantDynamic values, if there is at least one.
        constantDynamicValues = hasConstantDynamic ? new ConstantDynamic[constantPoolCount] : null;

        // Read the BootstrapMethods attribute, if any (only get the offset of each method).
        bootstrapMethodOffsets =
            hasBootstrapMethods ? ReadBootstrapMethodsAttribute(currentMaxStringLength) : null;
    }

    /// <summary>
    /// Constructs a new <see cref="ClassReader"/> object.
    /// </summary>
    /// <param name="inputStream">an input stream of the JVMS ClassFile structure to be read.</param>
    public ClassReader(Stream inputStream)
        : this(ReadStream(inputStream, false))
    {
    }

    /// <summary>
    /// Reads the given input stream and returns its content as a byte array.
    /// </summary>
    /// <param name="inputStream">an input stream.</param>
    /// <param name="close">true to close the input stream after reading.</param>
    /// <returns>the content of the given input stream.</returns>
    private static byte[] ReadStream(Stream inputStream, bool close)
    {
        if (inputStream == null)
        {
            throw new IOException("Class not found");
        }
        int bufferSize = ComputeBufferSize(inputStream);
        try
        {
            using MemoryStream outputStream = new MemoryStream();
            byte[] data = new byte[bufferSize];
            int bytesRead;
            int readCount = 0;
            while ((bytesRead = inputStream.Read(data, 0, bufferSize)) > 0)
            {
                outputStream.Write(data, 0, bytesRead);
                readCount++;
            }
            outputStream.Flush();
            if (readCount == 1)
            {
                return data;
            }
            return outputStream.ToArray();
        }
        finally
        {
            if (close)
            {
                inputStream.Close();
            }
        }
    }

    private static int ComputeBufferSize(Stream inputStream)
    {
        int expectedLength = inputStream.CanSeek ? (int)inputStream.Length : 0;
        if (expectedLength < 256)
        {
            return INPUT_STREAM_DATA_CHUNK_SIZE;
        }
        return Math.Min(expectedLength, MAX_BUFFER_SIZE);
    }

    // -----------------------------------------------------------------------------------------------
    // Accessors
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the class's access flags (see <see cref="Opcodes"/>).
    /// </summary>
    /// <returns>the class access flags.</returns>
    public int GetAccess()
    {
        return ReadUnsignedShort(header);
    }

    /// <summary>
    /// Returns the internal name of the class (see <see cref="Type.GetInternalName()"/>).
    /// </summary>
    /// <returns>the internal class name.</returns>
    public string GetClassName()
    {
        return ReadClass(header + 2, new char[maxStringLength]);
    }

    /// <summary>
    /// Returns the internal name of the super class (see <see cref="Type.GetInternalName()"/>).
    /// </summary>
    /// <returns>the internal name of the super class, or <c>null</c> for the <see cref="object"/> class.</returns>
    public string GetSuperName()
    {
        return ReadClass(header + 4, new char[maxStringLength]);
    }

    /// <summary>
    /// Returns the internal names of the implemented interfaces (see <see cref="Type.GetInternalName()"/>).
    /// </summary>
    /// <returns>the internal names of the directly implemented interfaces.</returns>
    public string[] GetInterfaces()
    {
        int currentOffset = header + 6;
        int interfacesCount = ReadUnsignedShort(currentOffset);
        string[] interfaces = new string[interfacesCount];
        if (interfacesCount > 0)
        {
            char[] charBuffer = new char[maxStringLength];
            for (int i = 0; i < interfacesCount; ++i)
            {
                currentOffset += 2;
                interfaces[i] = ReadClass(currentOffset, charBuffer);
            }
        }
        return interfaces;
    }

    // -----------------------------------------------------------------------------------------------
    // Public methods
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Makes the given visitor visit the JVMS ClassFile structure passed to the constructor of this
    /// <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="classVisitor">the visitor that must visit this class.</param>
    /// <param name="parsingOptions">the options to use to parse this class.</param>
    public void Accept(ClassVisitor classVisitor, int parsingOptions)
    {
        Accept(classVisitor, new Attribute[0], parsingOptions);
    }

    /// <summary>
    /// Makes the given visitor visit the JVMS ClassFile structure passed to the constructor of this
    /// <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="classVisitor">the visitor that must visit this class.</param>
    /// <param name="attributePrototypes">prototypes of the attributes that must be parsed.</param>
    /// <param name="parsingOptions">the options to use to parse this class.</param>
    public void Accept(
        ClassVisitor classVisitor,
        Attribute[] attributePrototypes,
        int parsingOptions)
    {
        Context context = new Context();
        context.AttributePrototypes = attributePrototypes;
        context.ParsingOptions = parsingOptions;
        context.CharBuffer = new char[maxStringLength];

        // Read the access_flags, this_class, super_class, interface_count and interfaces fields.
        char[] charBuffer = context.CharBuffer;
        int currentOffset = header;
        int accessFlags = ReadUnsignedShort(currentOffset);
        string thisClass = ReadClass(currentOffset + 2, charBuffer);
        string superClass = ReadClass(currentOffset + 4, charBuffer);
        string[] interfaces = new string[ReadUnsignedShort(currentOffset + 6)];
        currentOffset += 8;
        for (int i = 0; i < interfaces.Length; ++i)
        {
            interfaces[i] = ReadClass(currentOffset, charBuffer);
            currentOffset += 2;
        }

        // Read the class attributes.
        int innerClassesOffset = 0;
        int enclosingMethodOffset = 0;
        string signature = null;
        string sourceFile = null;
        string sourceDebugExtension = null;
        int runtimeVisibleAnnotationsOffset = 0;
        int runtimeInvisibleAnnotationsOffset = 0;
        int runtimeVisibleTypeAnnotationsOffset = 0;
        int runtimeInvisibleTypeAnnotationsOffset = 0;
        int moduleOffset = 0;
        int modulePackagesOffset = 0;
        string moduleMainClass = null;
        string nestHostClass = null;
        int nestMembersOffset = 0;
        int permittedSubclassesOffset = 0;
        int recordOffset = 0;
        Attribute attributes = null;

        int currentAttributeOffset = GetFirstAttributeOffset();
        for (int i = ReadUnsignedShort(currentAttributeOffset - 2); i > 0; --i)
        {
            string attributeName = ReadUTF8(currentAttributeOffset, charBuffer);
            int attributeLength = ReadInt(currentAttributeOffset + 2);
            currentAttributeOffset += 6;
            if (Constants.SOURCE_FILE == attributeName)
            {
                sourceFile = ReadUTF8(currentAttributeOffset, charBuffer);
            }
            else if (Constants.INNER_CLASSES == attributeName)
            {
                innerClassesOffset = currentAttributeOffset;
            }
            else if (Constants.ENCLOSING_METHOD == attributeName)
            {
                enclosingMethodOffset = currentAttributeOffset;
            }
            else if (Constants.NEST_HOST == attributeName)
            {
                nestHostClass = ReadClass(currentAttributeOffset, charBuffer);
            }
            else if (Constants.NEST_MEMBERS == attributeName)
            {
                nestMembersOffset = currentAttributeOffset;
            }
            else if (Constants.PERMITTED_SUBCLASSES == attributeName)
            {
                permittedSubclassesOffset = currentAttributeOffset;
            }
            else if (Constants.SIGNATURE == attributeName)
            {
                signature = ReadUTF8(currentAttributeOffset, charBuffer);
            }
            else if (Constants.RUNTIME_VISIBLE_ANNOTATIONS == attributeName)
            {
                runtimeVisibleAnnotationsOffset = currentAttributeOffset;
            }
            else if (Constants.RUNTIME_VISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                runtimeVisibleTypeAnnotationsOffset = currentAttributeOffset;
            }
            else if (Constants.DEPRECATED == attributeName)
            {
                accessFlags |= Opcodes.ACC_DEPRECATED;
            }
            else if (Constants.SYNTHETIC == attributeName)
            {
                accessFlags |= Opcodes.ACC_SYNTHETIC;
            }
            else if (Constants.SOURCE_DEBUG_EXTENSION == attributeName)
            {
                if (attributeLength > classFileBuffer.Length - currentAttributeOffset)
                {
                    throw new ArgumentException();
                }
                sourceDebugExtension =
                    ReadUtf(currentAttributeOffset, attributeLength, new char[attributeLength]);
            }
            else if (Constants.RUNTIME_INVISIBLE_ANNOTATIONS == attributeName)
            {
                runtimeInvisibleAnnotationsOffset = currentAttributeOffset;
            }
            else if (Constants.RUNTIME_INVISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                runtimeInvisibleTypeAnnotationsOffset = currentAttributeOffset;
            }
            else if (Constants.RECORD == attributeName)
            {
                recordOffset = currentAttributeOffset;
                accessFlags |= Opcodes.ACC_RECORD;
            }
            else if (Constants.MODULE == attributeName)
            {
                moduleOffset = currentAttributeOffset;
            }
            else if (Constants.MODULE_MAIN_CLASS == attributeName)
            {
                moduleMainClass = ReadClass(currentAttributeOffset, charBuffer);
            }
            else if (Constants.MODULE_PACKAGES == attributeName)
            {
                modulePackagesOffset = currentAttributeOffset;
            }
            else if (Constants.BOOTSTRAP_METHODS != attributeName)
            {
                Attribute attribute =
                    ReadAttribute(
                        attributePrototypes,
                        attributeName,
                        currentAttributeOffset,
                        attributeLength,
                        charBuffer,
                        -1,
                        null);
                attribute.nextAttribute = attributes;
                attributes = attribute;
            }
            currentAttributeOffset += attributeLength;
        }

        // Visit the class declaration.
        classVisitor.Visit(
            ReadInt(cpInfoOffsets[1] - 7), accessFlags, thisClass, signature, superClass, interfaces);

        // Visit the SourceFile and SourceDebugExtension attributes.
        if ((parsingOptions & SKIP_DEBUG) == 0
            && (sourceFile != null || sourceDebugExtension != null))
        {
            classVisitor.VisitSource(sourceFile, sourceDebugExtension);
        }

        // Visit the Module, ModulePackages and ModuleMainClass attributes.
        if (moduleOffset != 0)
        {
            ReadModuleAttributes(
                classVisitor, context, moduleOffset, modulePackagesOffset, moduleMainClass);
        }

        // Visit the NestHost attribute.
        if (nestHostClass != null)
        {
            classVisitor.VisitNestHost(nestHostClass);
        }

        // Visit the EnclosingMethod attribute.
        if (enclosingMethodOffset != 0)
        {
            string className = ReadClass(enclosingMethodOffset, charBuffer);
            int methodIndex = ReadUnsignedShort(enclosingMethodOffset + 2);
            string name = methodIndex == 0 ? null : ReadUTF8(cpInfoOffsets[methodIndex], charBuffer);
            string type = methodIndex == 0 ? null : ReadUTF8(cpInfoOffsets[methodIndex] + 2, charBuffer);
            classVisitor.VisitOuterClass(className, name, type);
        }

        // Visit the RuntimeVisibleAnnotations attribute.
        if (runtimeVisibleAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeVisibleAnnotationsOffset);
            int currentAnnotationOffset = runtimeVisibleAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        classVisitor.VisitAnnotation(annotationDescriptor, true),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        // Visit the RuntimeInvisibleAnnotations attribute.
        if (runtimeInvisibleAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeInvisibleAnnotationsOffset);
            int currentAnnotationOffset = runtimeInvisibleAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        classVisitor.VisitAnnotation(annotationDescriptor, false),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        // Visit the RuntimeVisibleTypeAnnotations attribute.
        if (runtimeVisibleTypeAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeVisibleTypeAnnotationsOffset);
            int currentAnnotationOffset = runtimeVisibleTypeAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                currentAnnotationOffset = ReadTypeAnnotationTarget(context, currentAnnotationOffset);
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        classVisitor.VisitTypeAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            true),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        // Visit the RuntimeInvisibleTypeAnnotations attribute.
        if (runtimeInvisibleTypeAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeInvisibleTypeAnnotationsOffset);
            int currentAnnotationOffset = runtimeInvisibleTypeAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                currentAnnotationOffset = ReadTypeAnnotationTarget(context, currentAnnotationOffset);
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        classVisitor.VisitTypeAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            false),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        // Visit the non standard attributes.
        while (attributes != null)
        {
            Attribute nextAttribute = attributes.nextAttribute;
            attributes.nextAttribute = null;
            classVisitor.VisitAttribute(attributes);
            attributes = nextAttribute;
        }

        // Visit the NestMembers attribute.
        if (nestMembersOffset != 0)
        {
            int numberOfNestMembers = ReadUnsignedShort(nestMembersOffset);
            int currentNestMemberOffset = nestMembersOffset + 2;
            while (numberOfNestMembers-- > 0)
            {
                classVisitor.VisitNestMember(ReadClass(currentNestMemberOffset, charBuffer));
                currentNestMemberOffset += 2;
            }
        }

        // Visit the PermittedSubclasses attribute.
        if (permittedSubclassesOffset != 0)
        {
            int numberOfPermittedSubclasses = ReadUnsignedShort(permittedSubclassesOffset);
            int currentPermittedSubclassesOffset = permittedSubclassesOffset + 2;
            while (numberOfPermittedSubclasses-- > 0)
            {
                classVisitor.VisitPermittedSubclass(
                    ReadClass(currentPermittedSubclassesOffset, charBuffer));
                currentPermittedSubclassesOffset += 2;
            }
        }

        // Visit the InnerClasses attribute.
        if (innerClassesOffset != 0)
        {
            int numberOfClasses = ReadUnsignedShort(innerClassesOffset);
            int currentClassesOffset = innerClassesOffset + 2;
            while (numberOfClasses-- > 0)
            {
                classVisitor.VisitInnerClass(
                    ReadClass(currentClassesOffset, charBuffer),
                    ReadClass(currentClassesOffset + 2, charBuffer),
                    ReadUTF8(currentClassesOffset + 4, charBuffer),
                    ReadUnsignedShort(currentClassesOffset + 6));
                currentClassesOffset += 8;
            }
        }

        // Visit Record components.
        if (recordOffset != 0)
        {
            int recordComponentsCount = ReadUnsignedShort(recordOffset);
            recordOffset += 2;
            while (recordComponentsCount-- > 0)
            {
                recordOffset = ReadRecordComponent(classVisitor, context, recordOffset);
            }
        }

        // Visit the fields and methods.
        int fieldsCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (fieldsCount-- > 0)
        {
            currentOffset = ReadField(classVisitor, context, currentOffset);
        }
        int methodsCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (methodsCount-- > 0)
        {
            currentOffset = ReadMethod(classVisitor, context, currentOffset);
        }

        // Visit the end of the class.
        classVisitor.VisitEnd();
    }

    // ----------------------------------------------------------------------------------------------
    // Methods to parse modules, fields and methods
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads the Module, ModulePackages and ModuleMainClass attributes and visit them.
    /// </summary>
    /// <param name="classVisitor">the current class visitor.</param>
    /// <param name="context">information about the class being parsed.</param>
    /// <param name="moduleOffset">the offset of the Module attribute.</param>
    /// <param name="modulePackagesOffset">the offset of the ModulePackages attribute, or 0.</param>
    /// <param name="moduleMainClass">the string corresponding to the ModuleMainClass attribute, or null.</param>
    private void ReadModuleAttributes(
        ClassVisitor classVisitor,
        Context context,
        int moduleOffset,
        int modulePackagesOffset,
        string moduleMainClass)
    {
        char[] buffer = context.CharBuffer;

        int currentOffset = moduleOffset;
        string moduleName = ReadModule(currentOffset, buffer);
        int moduleFlags = ReadUnsignedShort(currentOffset + 2);
        string moduleVersion = ReadUTF8(currentOffset + 4, buffer);
        currentOffset += 6;
        ModuleVisitor moduleVisitor = classVisitor.VisitModule(moduleName, moduleFlags, moduleVersion);
        if (moduleVisitor == null)
        {
            return;
        }

        if (moduleMainClass != null)
        {
            moduleVisitor.VisitMainClass(moduleMainClass);
        }

        if (modulePackagesOffset != 0)
        {
            int packageCount = ReadUnsignedShort(modulePackagesOffset);
            int currentPackageOffset = modulePackagesOffset + 2;
            while (packageCount-- > 0)
            {
                moduleVisitor.VisitPackage(ReadPackage(currentPackageOffset, buffer));
                currentPackageOffset += 2;
            }
        }

        int requiresCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (requiresCount-- > 0)
        {
            string requires = ReadModule(currentOffset, buffer);
            int requiresFlags = ReadUnsignedShort(currentOffset + 2);
            string requiresVersion = ReadUTF8(currentOffset + 4, buffer);
            currentOffset += 6;
            moduleVisitor.VisitRequire(requires, requiresFlags, requiresVersion);
        }

        int exportsCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (exportsCount-- > 0)
        {
            string exports = ReadPackage(currentOffset, buffer);
            int exportsFlags = ReadUnsignedShort(currentOffset + 2);
            int exportsToCount = ReadUnsignedShort(currentOffset + 4);
            currentOffset += 6;
            string[] exportsTo = null;
            if (exportsToCount != 0)
            {
                exportsTo = new string[exportsToCount];
                for (int i = 0; i < exportsToCount; ++i)
                {
                    exportsTo[i] = ReadModule(currentOffset, buffer);
                    currentOffset += 2;
                }
            }
            moduleVisitor.VisitExport(exports, exportsFlags, exportsTo);
        }

        int opensCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (opensCount-- > 0)
        {
            string opens = ReadPackage(currentOffset, buffer);
            int opensFlags = ReadUnsignedShort(currentOffset + 2);
            int opensToCount = ReadUnsignedShort(currentOffset + 4);
            currentOffset += 6;
            string[] opensTo = null;
            if (opensToCount != 0)
            {
                opensTo = new string[opensToCount];
                for (int i = 0; i < opensToCount; ++i)
                {
                    opensTo[i] = ReadModule(currentOffset, buffer);
                    currentOffset += 2;
                }
            }
            moduleVisitor.VisitOpen(opens, opensFlags, opensTo);
        }

        int usesCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (usesCount-- > 0)
        {
            moduleVisitor.VisitUse(ReadClass(currentOffset, buffer));
            currentOffset += 2;
        }

        int providesCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (providesCount-- > 0)
        {
            string provides = ReadClass(currentOffset, buffer);
            int providesWithCount = ReadUnsignedShort(currentOffset + 2);
            currentOffset += 4;
            string[] providesWith = new string[providesWithCount];
            for (int i = 0; i < providesWithCount; ++i)
            {
                providesWith[i] = ReadClass(currentOffset, buffer);
                currentOffset += 2;
            }
            moduleVisitor.VisitProvide(provides, providesWith);
        }

        moduleVisitor.VisitEnd();
    }

    /// <summary>
    /// Reads a record component and visit it.
    /// </summary>
    /// <param name="classVisitor">the current class visitor.</param>
    /// <param name="context">information about the class being parsed.</param>
    /// <param name="recordComponentOffset">the offset of the current record component.</param>
    /// <returns>the offset of the first byte following the record component.</returns>
    private int ReadRecordComponent(
        ClassVisitor classVisitor, Context context, int recordComponentOffset)
    {
        char[] charBuffer = context.CharBuffer;

        int currentOffset = recordComponentOffset;
        string name = ReadUTF8(currentOffset, charBuffer);
        string descriptor = ReadUTF8(currentOffset + 2, charBuffer);
        currentOffset += 4;

        string signature = null;
        int runtimeVisibleAnnotationsOffset = 0;
        int runtimeInvisibleAnnotationsOffset = 0;
        int runtimeVisibleTypeAnnotationsOffset = 0;
        int runtimeInvisibleTypeAnnotationsOffset = 0;
        Attribute attributes = null;

        int attributesCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (attributesCount-- > 0)
        {
            string attributeName = ReadUTF8(currentOffset, charBuffer);
            int attributeLength = ReadInt(currentOffset + 2);
            currentOffset += 6;
            if (Constants.SIGNATURE == attributeName)
            {
                signature = ReadUTF8(currentOffset, charBuffer);
            }
            else if (Constants.RUNTIME_VISIBLE_ANNOTATIONS == attributeName)
            {
                runtimeVisibleAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_VISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                runtimeVisibleTypeAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_INVISIBLE_ANNOTATIONS == attributeName)
            {
                runtimeInvisibleAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_INVISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                runtimeInvisibleTypeAnnotationsOffset = currentOffset;
            }
            else
            {
                Attribute attribute =
                    ReadAttribute(
                        context.AttributePrototypes,
                        attributeName,
                        currentOffset,
                        attributeLength,
                        charBuffer,
                        -1,
                        null);
                attribute.nextAttribute = attributes;
                attributes = attribute;
            }
            currentOffset += attributeLength;
        }

        RecordComponentVisitor recordComponentVisitor =
            classVisitor.VisitRecordComponent(name, descriptor, signature);
        if (recordComponentVisitor == null)
        {
            return currentOffset;
        }

        if (runtimeVisibleAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeVisibleAnnotationsOffset);
            int currentAnnotationOffset = runtimeVisibleAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        recordComponentVisitor.VisitAnnotation(annotationDescriptor, true),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        if (runtimeInvisibleAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeInvisibleAnnotationsOffset);
            int currentAnnotationOffset = runtimeInvisibleAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        recordComponentVisitor.VisitAnnotation(annotationDescriptor, false),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        if (runtimeVisibleTypeAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeVisibleTypeAnnotationsOffset);
            int currentAnnotationOffset = runtimeVisibleTypeAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                currentAnnotationOffset = ReadTypeAnnotationTarget(context, currentAnnotationOffset);
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        recordComponentVisitor.VisitTypeAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            true),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        if (runtimeInvisibleTypeAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeInvisibleTypeAnnotationsOffset);
            int currentAnnotationOffset = runtimeInvisibleTypeAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                currentAnnotationOffset = ReadTypeAnnotationTarget(context, currentAnnotationOffset);
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        recordComponentVisitor.VisitTypeAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            false),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        while (attributes != null)
        {
            Attribute nextAttribute = attributes.nextAttribute;
            attributes.nextAttribute = null;
            recordComponentVisitor.VisitAttribute(attributes);
            attributes = nextAttribute;
        }

        recordComponentVisitor.VisitEnd();
        return currentOffset;
    }

    /// <summary>
    /// Reads a JVMS field_info structure and makes the given visitor visit it.
    /// </summary>
    /// <param name="classVisitor">the visitor that must visit the field.</param>
    /// <param name="context">information about the class being parsed.</param>
    /// <param name="fieldInfoOffset">the start offset of the field_info structure.</param>
    /// <returns>the offset of the first byte following the field_info structure.</returns>
    private int ReadField(
        ClassVisitor classVisitor, Context context, int fieldInfoOffset)
    {
        char[] charBuffer = context.CharBuffer;

        int currentOffset = fieldInfoOffset;
        int accessFlags = ReadUnsignedShort(currentOffset);
        string name = ReadUTF8(currentOffset + 2, charBuffer);
        string descriptor = ReadUTF8(currentOffset + 4, charBuffer);
        currentOffset += 6;

        object constantValue = null;
        string signature = null;
        int runtimeVisibleAnnotationsOffset = 0;
        int runtimeInvisibleAnnotationsOffset = 0;
        int runtimeVisibleTypeAnnotationsOffset = 0;
        int runtimeInvisibleTypeAnnotationsOffset = 0;
        Attribute attributes = null;

        int attributesCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (attributesCount-- > 0)
        {
            string attributeName = ReadUTF8(currentOffset, charBuffer);
            int attributeLength = ReadInt(currentOffset + 2);
            currentOffset += 6;
            if (Constants.CONSTANT_VALUE == attributeName)
            {
                int constantvalueIndex = ReadUnsignedShort(currentOffset);
                constantValue = constantvalueIndex == 0 ? null : ReadConst(constantvalueIndex, charBuffer);
            }
            else if (Constants.SIGNATURE == attributeName)
            {
                signature = ReadUTF8(currentOffset, charBuffer);
            }
            else if (Constants.DEPRECATED == attributeName)
            {
                accessFlags |= Opcodes.ACC_DEPRECATED;
            }
            else if (Constants.SYNTHETIC == attributeName)
            {
                accessFlags |= Opcodes.ACC_SYNTHETIC;
            }
            else if (Constants.RUNTIME_VISIBLE_ANNOTATIONS == attributeName)
            {
                runtimeVisibleAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_VISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                runtimeVisibleTypeAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_INVISIBLE_ANNOTATIONS == attributeName)
            {
                runtimeInvisibleAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_INVISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                runtimeInvisibleTypeAnnotationsOffset = currentOffset;
            }
            else
            {
                Attribute attribute =
                    ReadAttribute(
                        context.AttributePrototypes,
                        attributeName,
                        currentOffset,
                        attributeLength,
                        charBuffer,
                        -1,
                        null);
                attribute.nextAttribute = attributes;
                attributes = attribute;
            }
            currentOffset += attributeLength;
        }

        FieldVisitor fieldVisitor =
            classVisitor.VisitField(accessFlags, name, descriptor, signature, constantValue);
        if (fieldVisitor == null)
        {
            return currentOffset;
        }

        if (runtimeVisibleAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeVisibleAnnotationsOffset);
            int currentAnnotationOffset = runtimeVisibleAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        fieldVisitor.VisitAnnotation(annotationDescriptor, true),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        if (runtimeInvisibleAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeInvisibleAnnotationsOffset);
            int currentAnnotationOffset = runtimeInvisibleAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        fieldVisitor.VisitAnnotation(annotationDescriptor, false),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        if (runtimeVisibleTypeAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeVisibleTypeAnnotationsOffset);
            int currentAnnotationOffset = runtimeVisibleTypeAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                currentAnnotationOffset = ReadTypeAnnotationTarget(context, currentAnnotationOffset);
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        fieldVisitor.VisitTypeAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            true),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        if (runtimeInvisibleTypeAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeInvisibleTypeAnnotationsOffset);
            int currentAnnotationOffset = runtimeInvisibleTypeAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                currentAnnotationOffset = ReadTypeAnnotationTarget(context, currentAnnotationOffset);
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        fieldVisitor.VisitTypeAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            false),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        while (attributes != null)
        {
            Attribute nextAttribute = attributes.nextAttribute;
            attributes.nextAttribute = null;
            fieldVisitor.VisitAttribute(attributes);
            attributes = nextAttribute;
        }

        fieldVisitor.VisitEnd();
        return currentOffset;
    }

    /// <summary>
    /// Reads a JVMS method_info structure and makes the given visitor visit it.
    /// </summary>
    /// <param name="classVisitor">the visitor that must visit the method.</param>
    /// <param name="context">information about the class being parsed.</param>
    /// <param name="methodInfoOffset">the start offset of the method_info structure.</param>
    /// <returns>the offset of the first byte following the method_info structure.</returns>
    private int ReadMethod(
        ClassVisitor classVisitor, Context context, int methodInfoOffset)
    {
        char[] charBuffer = context.CharBuffer;

        int currentOffset = methodInfoOffset;
        context.CurrentMethodAccessFlags = ReadUnsignedShort(currentOffset);
        context.CurrentMethodName = ReadUTF8(currentOffset + 2, charBuffer);
        context.CurrentMethodDescriptor = ReadUTF8(currentOffset + 4, charBuffer);
        currentOffset += 6;

        int codeOffset = 0;
        int exceptionsOffset = 0;
        string[] exceptions = null;
        bool synthetic = false;
        int signatureIndex = 0;
        int runtimeVisibleAnnotationsOffset = 0;
        int runtimeInvisibleAnnotationsOffset = 0;
        int runtimeVisibleParameterAnnotationsOffset = 0;
        int runtimeInvisibleParameterAnnotationsOffset = 0;
        int runtimeVisibleTypeAnnotationsOffset = 0;
        int runtimeInvisibleTypeAnnotationsOffset = 0;
        int annotationDefaultOffset = 0;
        int methodParametersOffset = 0;
        Attribute attributes = null;

        int attributesCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (attributesCount-- > 0)
        {
            string attributeName = ReadUTF8(currentOffset, charBuffer);
            int attributeLength = ReadInt(currentOffset + 2);
            currentOffset += 6;
            if (Constants.CODE == attributeName)
            {
                if ((context.ParsingOptions & SKIP_CODE) == 0)
                {
                    codeOffset = currentOffset;
                }
            }
            else if (Constants.EXCEPTIONS == attributeName)
            {
                exceptionsOffset = currentOffset;
                exceptions = new string[ReadUnsignedShort(exceptionsOffset)];
                int currentExceptionOffset = exceptionsOffset + 2;
                for (int i = 0; i < exceptions.Length; ++i)
                {
                    exceptions[i] = ReadClass(currentExceptionOffset, charBuffer);
                    currentExceptionOffset += 2;
                }
            }
            else if (Constants.SIGNATURE == attributeName)
            {
                signatureIndex = ReadUnsignedShort(currentOffset);
            }
            else if (Constants.DEPRECATED == attributeName)
            {
                context.CurrentMethodAccessFlags |= Opcodes.ACC_DEPRECATED;
            }
            else if (Constants.RUNTIME_VISIBLE_ANNOTATIONS == attributeName)
            {
                runtimeVisibleAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_VISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                runtimeVisibleTypeAnnotationsOffset = currentOffset;
            }
            else if (Constants.ANNOTATION_DEFAULT == attributeName)
            {
                annotationDefaultOffset = currentOffset;
            }
            else if (Constants.SYNTHETIC == attributeName)
            {
                synthetic = true;
                context.CurrentMethodAccessFlags |= Opcodes.ACC_SYNTHETIC;
            }
            else if (Constants.RUNTIME_INVISIBLE_ANNOTATIONS == attributeName)
            {
                runtimeInvisibleAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_INVISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                runtimeInvisibleTypeAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_VISIBLE_PARAMETER_ANNOTATIONS == attributeName)
            {
                runtimeVisibleParameterAnnotationsOffset = currentOffset;
            }
            else if (Constants.RUNTIME_INVISIBLE_PARAMETER_ANNOTATIONS == attributeName)
            {
                runtimeInvisibleParameterAnnotationsOffset = currentOffset;
            }
            else if (Constants.METHOD_PARAMETERS == attributeName)
            {
                methodParametersOffset = currentOffset;
            }
            else
            {
                Attribute attribute =
                    ReadAttribute(
                        context.AttributePrototypes,
                        attributeName,
                        currentOffset,
                        attributeLength,
                        charBuffer,
                        -1,
                        null);
                attribute.nextAttribute = attributes;
                attributes = attribute;
            }
            currentOffset += attributeLength;
        }

        MethodVisitor methodVisitor =
            classVisitor.VisitMethod(
                context.CurrentMethodAccessFlags,
                context.CurrentMethodName,
                context.CurrentMethodDescriptor,
                signatureIndex == 0 ? null : ReadUtf(signatureIndex, charBuffer),
                exceptions);
        if (methodVisitor == null)
        {
            return currentOffset;
        }

        // If the returned MethodVisitor is in fact a MethodWriter, it might be possible to copy the
        // method attributes directly into the writer.
        if (methodVisitor is MethodWriter methodWriter)
        {
            if (methodWriter.CanCopyMethodAttributes(
                this,
                synthetic,
                (context.CurrentMethodAccessFlags & Opcodes.ACC_DEPRECATED) != 0,
                ReadUnsignedShort(methodInfoOffset + 4),
                signatureIndex,
                exceptionsOffset))
            {
                methodWriter.SetMethodAttributesSource(methodInfoOffset, currentOffset - methodInfoOffset);
                return currentOffset;
            }
        }

        // Visit the MethodParameters attribute.
        if (methodParametersOffset != 0 && (context.ParsingOptions & SKIP_DEBUG) == 0)
        {
            int parametersCount = ReadByte(methodParametersOffset);
            int currentParameterOffset = methodParametersOffset + 1;
            while (parametersCount-- > 0)
            {
                methodVisitor.VisitParameter(
                    ReadUTF8(currentParameterOffset, charBuffer),
                    ReadUnsignedShort(currentParameterOffset + 2));
                currentParameterOffset += 4;
            }
        }

        // Visit the AnnotationDefault attribute.
        if (annotationDefaultOffset != 0)
        {
            AnnotationVisitor annotationVisitor = methodVisitor.VisitAnnotationDefault();
            ReadElementValue(annotationVisitor, annotationDefaultOffset, null, charBuffer);
            if (annotationVisitor != null)
            {
                annotationVisitor.VisitEnd();
            }
        }

        // Visit the RuntimeVisibleAnnotations attribute.
        if (runtimeVisibleAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeVisibleAnnotationsOffset);
            int currentAnnotationOffset = runtimeVisibleAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        methodVisitor.VisitAnnotation(annotationDescriptor, true),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        // Visit the RuntimeInvisibleAnnotations attribute.
        if (runtimeInvisibleAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeInvisibleAnnotationsOffset);
            int currentAnnotationOffset = runtimeInvisibleAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        methodVisitor.VisitAnnotation(annotationDescriptor, false),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        // Visit the RuntimeVisibleTypeAnnotations attribute.
        if (runtimeVisibleTypeAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeVisibleTypeAnnotationsOffset);
            int currentAnnotationOffset = runtimeVisibleTypeAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                currentAnnotationOffset = ReadTypeAnnotationTarget(context, currentAnnotationOffset);
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        methodVisitor.VisitTypeAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            true),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        // Visit the RuntimeInvisibleTypeAnnotations attribute.
        if (runtimeInvisibleTypeAnnotationsOffset != 0)
        {
            int numAnnotations = ReadUnsignedShort(runtimeInvisibleTypeAnnotationsOffset);
            int currentAnnotationOffset = runtimeInvisibleTypeAnnotationsOffset + 2;
            while (numAnnotations-- > 0)
            {
                currentAnnotationOffset = ReadTypeAnnotationTarget(context, currentAnnotationOffset);
                string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                currentAnnotationOffset += 2;
                currentAnnotationOffset =
                    ReadElementValues(
                        methodVisitor.VisitTypeAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            false),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
            }
        }

        // Visit the RuntimeVisibleParameterAnnotations attribute.
        if (runtimeVisibleParameterAnnotationsOffset != 0)
        {
            ReadParameterAnnotations(
                methodVisitor, context, runtimeVisibleParameterAnnotationsOffset, true);
        }

        // Visit the RuntimeInvisibleParameterAnnotations attribute.
        if (runtimeInvisibleParameterAnnotationsOffset != 0)
        {
            ReadParameterAnnotations(
                methodVisitor, context, runtimeInvisibleParameterAnnotationsOffset, false);
        }

        // Visit the non standard attributes.
        while (attributes != null)
        {
            Attribute nextAttribute = attributes.nextAttribute;
            attributes.nextAttribute = null;
            methodVisitor.VisitAttribute(attributes);
            attributes = nextAttribute;
        }

        // Visit the Code attribute.
        if (codeOffset != 0)
        {
            methodVisitor.VisitCode();
            ReadCode(methodVisitor, context, codeOffset);
        }

        // Visit the end of the method.
        methodVisitor.VisitEnd();
        return currentOffset;
    }

    // ----------------------------------------------------------------------------------------------
    // Methods to parse a Code attribute
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads a JVMS 'Code' attribute and makes the given visitor visit it.
    /// </summary>
    /// <param name="methodVisitor">the visitor that must visit the Code attribute.</param>
    /// <param name="context">information about the class being parsed.</param>
    /// <param name="codeOffset">the start offset of the Code attribute.</param>
    private void ReadCode(
        MethodVisitor methodVisitor, Context context, int codeOffset)
    {
        int currentOffset = codeOffset;

        byte[] classBuffer = classFileBuffer;
        char[] charBuffer = context.CharBuffer;
        int maxStack = ReadUnsignedShort(currentOffset);
        int maxLocals = ReadUnsignedShort(currentOffset + 2);
        int codeLength = ReadInt(currentOffset + 4);
        currentOffset += 8;
        if (codeLength > classFileBuffer.Length - currentOffset)
        {
            throw new ArgumentException();
        }

        // Read the bytecode 'code' array to create a label for each referenced instruction.
        int bytecodeStartOffset = currentOffset;
        int bytecodeEndOffset = currentOffset + codeLength;
        Label[] labels = context.CurrentMethodLabels = new Label[codeLength + 1];
        while (currentOffset < bytecodeEndOffset)
        {
            int bytecodeOffset = currentOffset - bytecodeStartOffset;
            int opcode = classBuffer[currentOffset] & 0xFF;
            switch (opcode)
            {
                case Opcodes.NOP:
                case Opcodes.ACONST_NULL:
                case Opcodes.ICONST_M1:
                case Opcodes.ICONST_0:
                case Opcodes.ICONST_1:
                case Opcodes.ICONST_2:
                case Opcodes.ICONST_3:
                case Opcodes.ICONST_4:
                case Opcodes.ICONST_5:
                case Opcodes.LCONST_0:
                case Opcodes.LCONST_1:
                case Opcodes.FCONST_0:
                case Opcodes.FCONST_1:
                case Opcodes.FCONST_2:
                case Opcodes.DCONST_0:
                case Opcodes.DCONST_1:
                case Opcodes.IALOAD:
                case Opcodes.LALOAD:
                case Opcodes.FALOAD:
                case Opcodes.DALOAD:
                case Opcodes.AALOAD:
                case Opcodes.BALOAD:
                case Opcodes.CALOAD:
                case Opcodes.SALOAD:
                case Opcodes.IASTORE:
                case Opcodes.LASTORE:
                case Opcodes.FASTORE:
                case Opcodes.DASTORE:
                case Opcodes.AASTORE:
                case Opcodes.BASTORE:
                case Opcodes.CASTORE:
                case Opcodes.SASTORE:
                case Opcodes.POP:
                case Opcodes.POP2:
                case Opcodes.DUP:
                case Opcodes.DUP_X1:
                case Opcodes.DUP_X2:
                case Opcodes.DUP2:
                case Opcodes.DUP2_X1:
                case Opcodes.DUP2_X2:
                case Opcodes.SWAP:
                case Opcodes.IADD:
                case Opcodes.LADD:
                case Opcodes.FADD:
                case Opcodes.DADD:
                case Opcodes.ISUB:
                case Opcodes.LSUB:
                case Opcodes.FSUB:
                case Opcodes.DSUB:
                case Opcodes.IMUL:
                case Opcodes.LMUL:
                case Opcodes.FMUL:
                case Opcodes.DMUL:
                case Opcodes.IDIV:
                case Opcodes.LDIV:
                case Opcodes.FDIV:
                case Opcodes.DDIV:
                case Opcodes.IREM:
                case Opcodes.LREM:
                case Opcodes.FREM:
                case Opcodes.DREM:
                case Opcodes.INEG:
                case Opcodes.LNEG:
                case Opcodes.FNEG:
                case Opcodes.DNEG:
                case Opcodes.ISHL:
                case Opcodes.LSHL:
                case Opcodes.ISHR:
                case Opcodes.LSHR:
                case Opcodes.IUSHR:
                case Opcodes.LUSHR:
                case Opcodes.IAND:
                case Opcodes.LAND:
                case Opcodes.IOR:
                case Opcodes.LOR:
                case Opcodes.IXOR:
                case Opcodes.LXOR:
                case Opcodes.I2L:
                case Opcodes.I2F:
                case Opcodes.I2D:
                case Opcodes.L2I:
                case Opcodes.L2F:
                case Opcodes.L2D:
                case Opcodes.F2I:
                case Opcodes.F2L:
                case Opcodes.F2D:
                case Opcodes.D2I:
                case Opcodes.D2L:
                case Opcodes.D2F:
                case Opcodes.I2B:
                case Opcodes.I2C:
                case Opcodes.I2S:
                case Opcodes.LCMP:
                case Opcodes.FCMPL:
                case Opcodes.FCMPG:
                case Opcodes.DCMPL:
                case Opcodes.DCMPG:
                case Opcodes.IRETURN:
                case Opcodes.LRETURN:
                case Opcodes.FRETURN:
                case Opcodes.DRETURN:
                case Opcodes.ARETURN:
                case Opcodes.RETURN:
                case Opcodes.ARRAYLENGTH:
                case Opcodes.ATHROW:
                case Opcodes.MONITORENTER:
                case Opcodes.MONITOREXIT:
                case Constants.ILOAD_0:
                case Constants.ILOAD_1:
                case Constants.ILOAD_2:
                case Constants.ILOAD_3:
                case Constants.LLOAD_0:
                case Constants.LLOAD_1:
                case Constants.LLOAD_2:
                case Constants.LLOAD_3:
                case Constants.FLOAD_0:
                case Constants.FLOAD_1:
                case Constants.FLOAD_2:
                case Constants.FLOAD_3:
                case Constants.DLOAD_0:
                case Constants.DLOAD_1:
                case Constants.DLOAD_2:
                case Constants.DLOAD_3:
                case Constants.ALOAD_0:
                case Constants.ALOAD_1:
                case Constants.ALOAD_2:
                case Constants.ALOAD_3:
                case Constants.ISTORE_0:
                case Constants.ISTORE_1:
                case Constants.ISTORE_2:
                case Constants.ISTORE_3:
                case Constants.LSTORE_0:
                case Constants.LSTORE_1:
                case Constants.LSTORE_2:
                case Constants.LSTORE_3:
                case Constants.FSTORE_0:
                case Constants.FSTORE_1:
                case Constants.FSTORE_2:
                case Constants.FSTORE_3:
                case Constants.DSTORE_0:
                case Constants.DSTORE_1:
                case Constants.DSTORE_2:
                case Constants.DSTORE_3:
                case Constants.ASTORE_0:
                case Constants.ASTORE_1:
                case Constants.ASTORE_2:
                case Constants.ASTORE_3:
                    currentOffset += 1;
                    break;
                case Opcodes.IFEQ:
                case Opcodes.IFNE:
                case Opcodes.IFLT:
                case Opcodes.IFGE:
                case Opcodes.IFGT:
                case Opcodes.IFLE:
                case Opcodes.IF_ICMPEQ:
                case Opcodes.IF_ICMPNE:
                case Opcodes.IF_ICMPLT:
                case Opcodes.IF_ICMPGE:
                case Opcodes.IF_ICMPGT:
                case Opcodes.IF_ICMPLE:
                case Opcodes.IF_ACMPEQ:
                case Opcodes.IF_ACMPNE:
                case Opcodes.GOTO:
                case Opcodes.JSR:
                case Opcodes.IFNULL:
                case Opcodes.IFNONNULL:
                    CreateLabel(bytecodeOffset + ReadShort(currentOffset + 1), labels);
                    currentOffset += 3;
                    break;
                case Constants.ASM_IFEQ:
                case Constants.ASM_IFNE:
                case Constants.ASM_IFLT:
                case Constants.ASM_IFGE:
                case Constants.ASM_IFGT:
                case Constants.ASM_IFLE:
                case Constants.ASM_IF_ICMPEQ:
                case Constants.ASM_IF_ICMPNE:
                case Constants.ASM_IF_ICMPLT:
                case Constants.ASM_IF_ICMPGE:
                case Constants.ASM_IF_ICMPGT:
                case Constants.ASM_IF_ICMPLE:
                case Constants.ASM_IF_ACMPEQ:
                case Constants.ASM_IF_ACMPNE:
                case Constants.ASM_GOTO:
                case Constants.ASM_JSR:
                case Constants.ASM_IFNULL:
                case Constants.ASM_IFNONNULL:
                    CreateLabel(bytecodeOffset + ReadUnsignedShort(currentOffset + 1), labels);
                    currentOffset += 3;
                    break;
                case Constants.GOTO_W:
                case Constants.JSR_W:
                case Constants.ASM_GOTO_W:
                    CreateLabel(bytecodeOffset + ReadInt(currentOffset + 1), labels);
                    currentOffset += 5;
                    break;
                case Constants.WIDE:
                    switch (classBuffer[currentOffset + 1] & 0xFF)
                    {
                        case Opcodes.ILOAD:
                        case Opcodes.FLOAD:
                        case Opcodes.ALOAD:
                        case Opcodes.LLOAD:
                        case Opcodes.DLOAD:
                        case Opcodes.ISTORE:
                        case Opcodes.FSTORE:
                        case Opcodes.ASTORE:
                        case Opcodes.LSTORE:
                        case Opcodes.DSTORE:
                        case Opcodes.RET:
                            currentOffset += 4;
                            break;
                        case Opcodes.IINC:
                            currentOffset += 6;
                            break;
                        default:
                            throw new ArgumentException();
                    }
                    break;
                case Opcodes.TABLESWITCH:
                    currentOffset += 4 - (bytecodeOffset & 3);
                    CreateLabel(bytecodeOffset + ReadInt(currentOffset), labels);
                    int numTableEntries = ReadInt(currentOffset + 8) - ReadInt(currentOffset + 4) + 1;
                    currentOffset += 12;
                    while (numTableEntries-- > 0)
                    {
                        CreateLabel(bytecodeOffset + ReadInt(currentOffset), labels);
                        currentOffset += 4;
                    }
                    break;
                case Opcodes.LOOKUPSWITCH:
                    currentOffset += 4 - (bytecodeOffset & 3);
                    CreateLabel(bytecodeOffset + ReadInt(currentOffset), labels);
                    int numSwitchCases = ReadInt(currentOffset + 4);
                    currentOffset += 8;
                    while (numSwitchCases-- > 0)
                    {
                        CreateLabel(bytecodeOffset + ReadInt(currentOffset + 4), labels);
                        currentOffset += 8;
                    }
                    break;
                case Opcodes.ILOAD:
                case Opcodes.LLOAD:
                case Opcodes.FLOAD:
                case Opcodes.DLOAD:
                case Opcodes.ALOAD:
                case Opcodes.ISTORE:
                case Opcodes.LSTORE:
                case Opcodes.FSTORE:
                case Opcodes.DSTORE:
                case Opcodes.ASTORE:
                case Opcodes.RET:
                case Opcodes.BIPUSH:
                case Opcodes.NEWARRAY:
                case Opcodes.LDC:
                    currentOffset += 2;
                    break;
                case Opcodes.SIPUSH:
                case Constants.LDC_W:
                case Constants.LDC2_W:
                case Opcodes.GETSTATIC:
                case Opcodes.PUTSTATIC:
                case Opcodes.GETFIELD:
                case Opcodes.PUTFIELD:
                case Opcodes.INVOKEVIRTUAL:
                case Opcodes.INVOKESPECIAL:
                case Opcodes.INVOKESTATIC:
                case Opcodes.NEW:
                case Opcodes.ANEWARRAY:
                case Opcodes.CHECKCAST:
                case Opcodes.INSTANCEOF:
                case Opcodes.IINC:
                    currentOffset += 3;
                    break;
                case Opcodes.INVOKEINTERFACE:
                case Opcodes.INVOKEDYNAMIC:
                    currentOffset += 5;
                    break;
                case Opcodes.MULTIANEWARRAY:
                    currentOffset += 4;
                    break;
                default:
                    throw new ArgumentException();
            }
        }

        // Read the 'exception_table_length' and 'exception_table' field.
        int exceptionTableLength = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (exceptionTableLength-- > 0)
        {
            Label start = CreateLabel(ReadUnsignedShort(currentOffset), labels);
            Label end = CreateLabel(ReadUnsignedShort(currentOffset + 2), labels);
            Label handler = CreateLabel(ReadUnsignedShort(currentOffset + 4), labels);
            string catchType = ReadUTF8(cpInfoOffsets[ReadUnsignedShort(currentOffset + 6)], charBuffer);
            currentOffset += 8;
            methodVisitor.VisitTryCatchBlock(start, end, handler, catchType);
        }

        // Read the Code attributes.
        int stackMapFrameOffset = 0;
        int stackMapTableEndOffset = 0;
        bool compressedFrames = true;
        int localVariableTableOffset = 0;
        int localVariableTypeTableOffset = 0;
        int[] visibleTypeAnnotationOffsets = null;
        int[] invisibleTypeAnnotationOffsets = null;
        Attribute attributes = null;

        int attributesCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (attributesCount-- > 0)
        {
            string attributeName = ReadUTF8(currentOffset, charBuffer);
            int attributeLength = ReadInt(currentOffset + 2);
            currentOffset += 6;
            if (Constants.LOCAL_VARIABLE_TABLE == attributeName)
            {
                if ((context.ParsingOptions & SKIP_DEBUG) == 0)
                {
                    localVariableTableOffset = currentOffset;
                    int currentLocalVariableTableOffset = currentOffset;
                    int localVariableTableLength = ReadUnsignedShort(currentLocalVariableTableOffset);
                    currentLocalVariableTableOffset += 2;
                    while (localVariableTableLength-- > 0)
                    {
                        int startPc = ReadUnsignedShort(currentLocalVariableTableOffset);
                        int length = ReadUnsignedShort(currentLocalVariableTableOffset + 2);
                        CreateDebugLabel(startPc, labels);
                        CreateDebugLabel(startPc + length, labels);
                        currentLocalVariableTableOffset += 10;
                    }
                }
            }
            else if (Constants.LOCAL_VARIABLE_TYPE_TABLE == attributeName)
            {
                localVariableTypeTableOffset = currentOffset;
            }
            else if (Constants.LINE_NUMBER_TABLE == attributeName)
            {
                if ((context.ParsingOptions & SKIP_DEBUG) == 0)
                {
                    int currentLineNumberTableOffset = currentOffset;
                    int lineNumberTableLength = ReadUnsignedShort(currentLineNumberTableOffset);
                    currentLineNumberTableOffset += 2;
                    while (lineNumberTableLength-- > 0)
                    {
                        int startPc = ReadUnsignedShort(currentLineNumberTableOffset);
                        int lineNumber = ReadUnsignedShort(currentLineNumberTableOffset + 2);
                        currentLineNumberTableOffset += 4;
                        CreateDebugLabel(startPc, labels);
                        labels[startPc].AddLineNumber(lineNumber);
                    }
                }
            }
            else if (Constants.RUNTIME_VISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                visibleTypeAnnotationOffsets =
                    ReadTypeAnnotations(methodVisitor, context, currentOffset, true);
            }
            else if (Constants.RUNTIME_INVISIBLE_TYPE_ANNOTATIONS == attributeName)
            {
                invisibleTypeAnnotationOffsets =
                    ReadTypeAnnotations(methodVisitor, context, currentOffset, false);
            }
            else if (Constants.STACK_MAP_TABLE == attributeName)
            {
                if ((context.ParsingOptions & SKIP_FRAMES) == 0)
                {
                    stackMapFrameOffset = currentOffset + 2;
                    stackMapTableEndOffset = currentOffset + attributeLength;
                }
            }
            else if ("StackMap" == attributeName)
            {
                if ((context.ParsingOptions & SKIP_FRAMES) == 0)
                {
                    stackMapFrameOffset = currentOffset + 2;
                    stackMapTableEndOffset = currentOffset + attributeLength;
                    compressedFrames = false;
                }
            }
            else
            {
                Attribute attribute =
                    ReadAttribute(
                        context.AttributePrototypes,
                        attributeName,
                        currentOffset,
                        attributeLength,
                        charBuffer,
                        codeOffset,
                        labels);
                attribute.nextAttribute = attributes;
                attributes = attribute;
            }
            currentOffset += attributeLength;
        }

        // Initialize the context fields related to stack map frames.
        bool expandFrames = (context.ParsingOptions & EXPAND_FRAMES) != 0;
        if (stackMapFrameOffset != 0)
        {
            context.CurrentFrameOffset = -1;
            context.CurrentFrameType = 0;
            context.CurrentFrameLocalCount = 0;
            context.CurrentFrameLocalCountDelta = 0;
            context.CurrentFrameLocalTypes = new object[maxLocals];
            context.CurrentFrameStackCount = 0;
            context.CurrentFrameStackTypes = new object[maxStack];
            if (expandFrames)
            {
                ComputeImplicitFrame(context);
            }
            // Find the labels for UNINITIALIZED frame types.
            for (int offset = stackMapFrameOffset; offset < stackMapTableEndOffset - 2; ++offset)
            {
                if (classBuffer[offset] == Frame.ITEM_UNINITIALIZED)
                {
                    int potentialBytecodeOffset = ReadUnsignedShort(offset + 1);
                    if (potentialBytecodeOffset >= 0
                        && potentialBytecodeOffset < codeLength
                        && (classBuffer[bytecodeStartOffset + potentialBytecodeOffset] & 0xFF)
                            == Opcodes.NEW)
                    {
                        CreateLabel(potentialBytecodeOffset, labels);
                    }
                }
            }
        }
        if (expandFrames && (context.ParsingOptions & EXPAND_ASM_INSNS) != 0)
        {
            methodVisitor.VisitFrame(Opcodes.F_NEW, maxLocals, null, 0, null);
        }

        // Visit the bytecode instructions.
        int currentVisibleTypeAnnotationIndex = 0;
        int currentVisibleTypeAnnotationBytecodeOffset =
            GetTypeAnnotationBytecodeOffset(visibleTypeAnnotationOffsets, 0);
        int currentInvisibleTypeAnnotationIndex = 0;
        int currentInvisibleTypeAnnotationBytecodeOffset =
            GetTypeAnnotationBytecodeOffset(invisibleTypeAnnotationOffsets, 0);

        bool insertFrame = false;

        int wideJumpOpcodeDelta =
            (context.ParsingOptions & EXPAND_ASM_INSNS) == 0 ? Constants.WIDE_JUMP_OPCODE_DELTA : 0;

        currentOffset = bytecodeStartOffset;
        while (currentOffset < bytecodeEndOffset)
        {
            int currentBytecodeOffset = currentOffset - bytecodeStartOffset;
            ReadBytecodeInstructionOffset(currentBytecodeOffset);

            // Visit the label and the line number(s) for this bytecode offset, if any.
            Label currentLabel = labels[currentBytecodeOffset];
            if (currentLabel != null)
            {
                currentLabel.Accept(methodVisitor, (context.ParsingOptions & SKIP_DEBUG) == 0);
            }

            // Visit the stack map frame for this bytecode offset, if any.
            while (stackMapFrameOffset != 0
                && (context.CurrentFrameOffset == currentBytecodeOffset
                    || context.CurrentFrameOffset == -1))
            {
                if (context.CurrentFrameOffset != -1)
                {
                    if (!compressedFrames || expandFrames)
                    {
                        methodVisitor.VisitFrame(
                            Opcodes.F_NEW,
                            context.CurrentFrameLocalCount,
                            context.CurrentFrameLocalTypes,
                            context.CurrentFrameStackCount,
                            context.CurrentFrameStackTypes);
                    }
                    else
                    {
                        methodVisitor.VisitFrame(
                            context.CurrentFrameType,
                            context.CurrentFrameLocalCountDelta,
                            context.CurrentFrameLocalTypes,
                            context.CurrentFrameStackCount,
                            context.CurrentFrameStackTypes);
                    }
                    insertFrame = false;
                }
                if (stackMapFrameOffset < stackMapTableEndOffset)
                {
                    stackMapFrameOffset =
                        ReadStackMapFrame(stackMapFrameOffset, compressedFrames, expandFrames, context);
                }
                else
                {
                    stackMapFrameOffset = 0;
                }
            }

            // Insert a stack map frame for this bytecode offset, if requested.
            if (insertFrame)
            {
                if ((context.ParsingOptions & EXPAND_FRAMES) != 0)
                {
                    methodVisitor.VisitFrame(Constants.F_INSERT, 0, null, 0, null);
                }
                insertFrame = false;
            }

            // Visit the instruction at this bytecode offset.
            int opcode = classBuffer[currentOffset] & 0xFF;
            switch (opcode)
            {
                case Opcodes.NOP:
                case Opcodes.ACONST_NULL:
                case Opcodes.ICONST_M1:
                case Opcodes.ICONST_0:
                case Opcodes.ICONST_1:
                case Opcodes.ICONST_2:
                case Opcodes.ICONST_3:
                case Opcodes.ICONST_4:
                case Opcodes.ICONST_5:
                case Opcodes.LCONST_0:
                case Opcodes.LCONST_1:
                case Opcodes.FCONST_0:
                case Opcodes.FCONST_1:
                case Opcodes.FCONST_2:
                case Opcodes.DCONST_0:
                case Opcodes.DCONST_1:
                case Opcodes.IALOAD:
                case Opcodes.LALOAD:
                case Opcodes.FALOAD:
                case Opcodes.DALOAD:
                case Opcodes.AALOAD:
                case Opcodes.BALOAD:
                case Opcodes.CALOAD:
                case Opcodes.SALOAD:
                case Opcodes.IASTORE:
                case Opcodes.LASTORE:
                case Opcodes.FASTORE:
                case Opcodes.DASTORE:
                case Opcodes.AASTORE:
                case Opcodes.BASTORE:
                case Opcodes.CASTORE:
                case Opcodes.SASTORE:
                case Opcodes.POP:
                case Opcodes.POP2:
                case Opcodes.DUP:
                case Opcodes.DUP_X1:
                case Opcodes.DUP_X2:
                case Opcodes.DUP2:
                case Opcodes.DUP2_X1:
                case Opcodes.DUP2_X2:
                case Opcodes.SWAP:
                case Opcodes.IADD:
                case Opcodes.LADD:
                case Opcodes.FADD:
                case Opcodes.DADD:
                case Opcodes.ISUB:
                case Opcodes.LSUB:
                case Opcodes.FSUB:
                case Opcodes.DSUB:
                case Opcodes.IMUL:
                case Opcodes.LMUL:
                case Opcodes.FMUL:
                case Opcodes.DMUL:
                case Opcodes.IDIV:
                case Opcodes.LDIV:
                case Opcodes.FDIV:
                case Opcodes.DDIV:
                case Opcodes.IREM:
                case Opcodes.LREM:
                case Opcodes.FREM:
                case Opcodes.DREM:
                case Opcodes.INEG:
                case Opcodes.LNEG:
                case Opcodes.FNEG:
                case Opcodes.DNEG:
                case Opcodes.ISHL:
                case Opcodes.LSHL:
                case Opcodes.ISHR:
                case Opcodes.LSHR:
                case Opcodes.IUSHR:
                case Opcodes.LUSHR:
                case Opcodes.IAND:
                case Opcodes.LAND:
                case Opcodes.IOR:
                case Opcodes.LOR:
                case Opcodes.IXOR:
                case Opcodes.LXOR:
                case Opcodes.I2L:
                case Opcodes.I2F:
                case Opcodes.I2D:
                case Opcodes.L2I:
                case Opcodes.L2F:
                case Opcodes.L2D:
                case Opcodes.F2I:
                case Opcodes.F2L:
                case Opcodes.F2D:
                case Opcodes.D2I:
                case Opcodes.D2L:
                case Opcodes.D2F:
                case Opcodes.I2B:
                case Opcodes.I2C:
                case Opcodes.I2S:
                case Opcodes.LCMP:
                case Opcodes.FCMPL:
                case Opcodes.FCMPG:
                case Opcodes.DCMPL:
                case Opcodes.DCMPG:
                case Opcodes.IRETURN:
                case Opcodes.LRETURN:
                case Opcodes.FRETURN:
                case Opcodes.DRETURN:
                case Opcodes.ARETURN:
                case Opcodes.RETURN:
                case Opcodes.ARRAYLENGTH:
                case Opcodes.ATHROW:
                case Opcodes.MONITORENTER:
                case Opcodes.MONITOREXIT:
                    methodVisitor.VisitInsn(opcode);
                    currentOffset += 1;
                    break;
                case Constants.ILOAD_0:
                case Constants.ILOAD_1:
                case Constants.ILOAD_2:
                case Constants.ILOAD_3:
                case Constants.LLOAD_0:
                case Constants.LLOAD_1:
                case Constants.LLOAD_2:
                case Constants.LLOAD_3:
                case Constants.FLOAD_0:
                case Constants.FLOAD_1:
                case Constants.FLOAD_2:
                case Constants.FLOAD_3:
                case Constants.DLOAD_0:
                case Constants.DLOAD_1:
                case Constants.DLOAD_2:
                case Constants.DLOAD_3:
                case Constants.ALOAD_0:
                case Constants.ALOAD_1:
                case Constants.ALOAD_2:
                case Constants.ALOAD_3:
                    opcode -= Constants.ILOAD_0;
                    methodVisitor.VisitVarInsn(Opcodes.ILOAD + (opcode >> 2), opcode & 0x3);
                    currentOffset += 1;
                    break;
                case Constants.ISTORE_0:
                case Constants.ISTORE_1:
                case Constants.ISTORE_2:
                case Constants.ISTORE_3:
                case Constants.LSTORE_0:
                case Constants.LSTORE_1:
                case Constants.LSTORE_2:
                case Constants.LSTORE_3:
                case Constants.FSTORE_0:
                case Constants.FSTORE_1:
                case Constants.FSTORE_2:
                case Constants.FSTORE_3:
                case Constants.DSTORE_0:
                case Constants.DSTORE_1:
                case Constants.DSTORE_2:
                case Constants.DSTORE_3:
                case Constants.ASTORE_0:
                case Constants.ASTORE_1:
                case Constants.ASTORE_2:
                case Constants.ASTORE_3:
                    opcode -= Constants.ISTORE_0;
                    methodVisitor.VisitVarInsn(Opcodes.ISTORE + (opcode >> 2), opcode & 0x3);
                    currentOffset += 1;
                    break;
                case Opcodes.IFEQ:
                case Opcodes.IFNE:
                case Opcodes.IFLT:
                case Opcodes.IFGE:
                case Opcodes.IFGT:
                case Opcodes.IFLE:
                case Opcodes.IF_ICMPEQ:
                case Opcodes.IF_ICMPNE:
                case Opcodes.IF_ICMPLT:
                case Opcodes.IF_ICMPGE:
                case Opcodes.IF_ICMPGT:
                case Opcodes.IF_ICMPLE:
                case Opcodes.IF_ACMPEQ:
                case Opcodes.IF_ACMPNE:
                case Opcodes.GOTO:
                case Opcodes.JSR:
                case Opcodes.IFNULL:
                case Opcodes.IFNONNULL:
                    methodVisitor.VisitJumpInsn(
                        opcode, labels[currentBytecodeOffset + ReadShort(currentOffset + 1)]);
                    currentOffset += 3;
                    break;
                case Constants.GOTO_W:
                case Constants.JSR_W:
                    methodVisitor.VisitJumpInsn(
                        opcode - wideJumpOpcodeDelta,
                        labels[currentBytecodeOffset + ReadInt(currentOffset + 1)]);
                    currentOffset += 5;
                    break;
                case Constants.ASM_IFEQ:
                case Constants.ASM_IFNE:
                case Constants.ASM_IFLT:
                case Constants.ASM_IFGE:
                case Constants.ASM_IFGT:
                case Constants.ASM_IFLE:
                case Constants.ASM_IF_ICMPEQ:
                case Constants.ASM_IF_ICMPNE:
                case Constants.ASM_IF_ICMPLT:
                case Constants.ASM_IF_ICMPGE:
                case Constants.ASM_IF_ICMPGT:
                case Constants.ASM_IF_ICMPLE:
                case Constants.ASM_IF_ACMPEQ:
                case Constants.ASM_IF_ACMPNE:
                case Constants.ASM_GOTO:
                case Constants.ASM_JSR:
                case Constants.ASM_IFNULL:
                case Constants.ASM_IFNONNULL:
                    {
                        opcode =
                            opcode < Constants.ASM_IFNULL
                                ? opcode - Constants.ASM_OPCODE_DELTA
                                : opcode - Constants.ASM_IFNULL_OPCODE_DELTA;
                        Label target = labels[currentBytecodeOffset + ReadUnsignedShort(currentOffset + 1)];
                        if (opcode == Opcodes.GOTO || opcode == Opcodes.JSR)
                        {
                            methodVisitor.VisitJumpInsn(opcode + Constants.WIDE_JUMP_OPCODE_DELTA, target);
                        }
                        else
                        {
                            opcode = opcode < Opcodes.GOTO ? ((opcode + 1) ^ 1) - 1 : opcode ^ 1;
                            Label endif = CreateLabel(currentBytecodeOffset + 3, labels);
                            methodVisitor.VisitJumpInsn(opcode, endif);
                            methodVisitor.VisitJumpInsn(Constants.GOTO_W, target);
                            insertFrame = true;
                        }
                        currentOffset += 3;
                        break;
                    }
                case Constants.ASM_GOTO_W:
                    methodVisitor.VisitJumpInsn(
                        Constants.GOTO_W, labels[currentBytecodeOffset + ReadInt(currentOffset + 1)]);
                    insertFrame = true;
                    currentOffset += 5;
                    break;
                case Constants.WIDE:
                    opcode = classBuffer[currentOffset + 1] & 0xFF;
                    if (opcode == Opcodes.IINC)
                    {
                        methodVisitor.VisitIincInsn(
                            ReadUnsignedShort(currentOffset + 2), ReadShort(currentOffset + 4));
                        currentOffset += 6;
                    }
                    else
                    {
                        methodVisitor.VisitVarInsn(opcode, ReadUnsignedShort(currentOffset + 2));
                        currentOffset += 4;
                    }
                    break;
                case Opcodes.TABLESWITCH:
                    {
                        currentOffset += 4 - (currentBytecodeOffset & 3);
                        Label defaultLabel = labels[currentBytecodeOffset + ReadInt(currentOffset)];
                        int low = ReadInt(currentOffset + 4);
                        int high = ReadInt(currentOffset + 8);
                        currentOffset += 12;
                        Label[] table = new Label[high - low + 1];
                        for (int i = 0; i < table.Length; ++i)
                        {
                            table[i] = labels[currentBytecodeOffset + ReadInt(currentOffset)];
                            currentOffset += 4;
                        }
                        methodVisitor.VisitTableSwitchInsn(low, high, defaultLabel, table);
                        break;
                    }
                case Opcodes.LOOKUPSWITCH:
                    {
                        currentOffset += 4 - (currentBytecodeOffset & 3);
                        Label defaultLabel = labels[currentBytecodeOffset + ReadInt(currentOffset)];
                        int numPairs = ReadInt(currentOffset + 4);
                        currentOffset += 8;
                        int[] keys = new int[numPairs];
                        Label[] values = new Label[numPairs];
                        for (int i = 0; i < numPairs; ++i)
                        {
                            keys[i] = ReadInt(currentOffset);
                            values[i] = labels[currentBytecodeOffset + ReadInt(currentOffset + 4)];
                            currentOffset += 8;
                        }
                        methodVisitor.VisitLookupSwitchInsn(defaultLabel, keys, values);
                        break;
                    }
                case Opcodes.ILOAD:
                case Opcodes.LLOAD:
                case Opcodes.FLOAD:
                case Opcodes.DLOAD:
                case Opcodes.ALOAD:
                case Opcodes.ISTORE:
                case Opcodes.LSTORE:
                case Opcodes.FSTORE:
                case Opcodes.DSTORE:
                case Opcodes.ASTORE:
                case Opcodes.RET:
                    methodVisitor.VisitVarInsn(opcode, classBuffer[currentOffset + 1] & 0xFF);
                    currentOffset += 2;
                    break;
                case Opcodes.BIPUSH:
                case Opcodes.NEWARRAY:
                    methodVisitor.VisitIntInsn(opcode, classBuffer[currentOffset + 1]);
                    currentOffset += 2;
                    break;
                case Opcodes.SIPUSH:
                    methodVisitor.VisitIntInsn(opcode, ReadShort(currentOffset + 1));
                    currentOffset += 3;
                    break;
                case Opcodes.LDC:
                    methodVisitor.VisitLdcInsn(ReadConst(classBuffer[currentOffset + 1] & 0xFF, charBuffer));
                    currentOffset += 2;
                    break;
                case Constants.LDC_W:
                case Constants.LDC2_W:
                    methodVisitor.VisitLdcInsn(ReadConst(ReadUnsignedShort(currentOffset + 1), charBuffer));
                    currentOffset += 3;
                    break;
                case Opcodes.GETSTATIC:
                case Opcodes.PUTSTATIC:
                case Opcodes.GETFIELD:
                case Opcodes.PUTFIELD:
                case Opcodes.INVOKEVIRTUAL:
                case Opcodes.INVOKESPECIAL:
                case Opcodes.INVOKESTATIC:
                case Opcodes.INVOKEINTERFACE:
                    {
                        int cpInfoOffset = cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)];
                        int nameAndTypeCpInfoOffset = cpInfoOffsets[ReadUnsignedShort(cpInfoOffset + 2)];
                        string owner = ReadClass(cpInfoOffset, charBuffer);
                        string name = ReadUTF8(nameAndTypeCpInfoOffset, charBuffer);
                        string descriptor = ReadUTF8(nameAndTypeCpInfoOffset + 2, charBuffer);
                        if (opcode < Opcodes.INVOKEVIRTUAL)
                        {
                            methodVisitor.VisitFieldInsn(opcode, owner, name, descriptor);
                        }
                        else
                        {
                            bool isInterface =
                                classBuffer[cpInfoOffset - 1] == Symbol.CONSTANT_INTERFACE_METHODREF_TAG;
                            methodVisitor.VisitMethodInsn(opcode, owner, name, descriptor, isInterface);
                        }
                        if (opcode == Opcodes.INVOKEINTERFACE)
                        {
                            currentOffset += 5;
                        }
                        else
                        {
                            currentOffset += 3;
                        }
                        break;
                    }
                case Opcodes.INVOKEDYNAMIC:
                    {
                        int cpInfoOffset = cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)];
                        int nameAndTypeCpInfoOffset = cpInfoOffsets[ReadUnsignedShort(cpInfoOffset + 2)];
                        string name = ReadUTF8(nameAndTypeCpInfoOffset, charBuffer);
                        string descriptor = ReadUTF8(nameAndTypeCpInfoOffset + 2, charBuffer);
                        int bootstrapMethodOffset = bootstrapMethodOffsets[ReadUnsignedShort(cpInfoOffset)];
                        Handle handle =
                            (Handle)ReadConst(ReadUnsignedShort(bootstrapMethodOffset), charBuffer);
                        object[] bootstrapMethodArguments =
                            new object[ReadUnsignedShort(bootstrapMethodOffset + 2)];
                        bootstrapMethodOffset += 4;
                        for (int i = 0; i < bootstrapMethodArguments.Length; i++)
                        {
                            bootstrapMethodArguments[i] =
                                ReadConst(ReadUnsignedShort(bootstrapMethodOffset), charBuffer);
                            bootstrapMethodOffset += 2;
                        }
                        methodVisitor.VisitInvokeDynamicInsn(
                            name, descriptor, handle, bootstrapMethodArguments);
                        currentOffset += 5;
                        break;
                    }
                case Opcodes.NEW:
                case Opcodes.ANEWARRAY:
                case Opcodes.CHECKCAST:
                case Opcodes.INSTANCEOF:
                    methodVisitor.VisitTypeInsn(opcode, ReadClass(currentOffset + 1, charBuffer));
                    currentOffset += 3;
                    break;
                case Opcodes.IINC:
                    methodVisitor.VisitIincInsn(
                        classBuffer[currentOffset + 1] & 0xFF, classBuffer[currentOffset + 2]);
                    currentOffset += 3;
                    break;
                case Opcodes.MULTIANEWARRAY:
                    methodVisitor.VisitMultiANewArrayInsn(
                        ReadClass(currentOffset + 1, charBuffer), classBuffer[currentOffset + 3] & 0xFF);
                    currentOffset += 4;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            // Visit the runtime visible instruction annotations, if any.
            while (visibleTypeAnnotationOffsets != null
                && currentVisibleTypeAnnotationIndex < visibleTypeAnnotationOffsets.Length
                && currentVisibleTypeAnnotationBytecodeOffset <= currentBytecodeOffset)
            {
                if (currentVisibleTypeAnnotationBytecodeOffset == currentBytecodeOffset)
                {
                    int currentAnnotationOffset =
                        ReadTypeAnnotationTarget(
                            context, visibleTypeAnnotationOffsets[currentVisibleTypeAnnotationIndex]);
                    string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                    currentAnnotationOffset += 2;
                    ReadElementValues(
                        methodVisitor.VisitInsnAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            true),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
                }
                currentVisibleTypeAnnotationBytecodeOffset =
                    GetTypeAnnotationBytecodeOffset(
                        visibleTypeAnnotationOffsets, ++currentVisibleTypeAnnotationIndex);
            }

            // Visit the runtime invisible instruction annotations, if any.
            while (invisibleTypeAnnotationOffsets != null
                && currentInvisibleTypeAnnotationIndex < invisibleTypeAnnotationOffsets.Length
                && currentInvisibleTypeAnnotationBytecodeOffset <= currentBytecodeOffset)
            {
                if (currentInvisibleTypeAnnotationBytecodeOffset == currentBytecodeOffset)
                {
                    int currentAnnotationOffset =
                        ReadTypeAnnotationTarget(
                            context, invisibleTypeAnnotationOffsets[currentInvisibleTypeAnnotationIndex]);
                    string annotationDescriptor = ReadUTF8(currentAnnotationOffset, charBuffer);
                    currentAnnotationOffset += 2;
                    ReadElementValues(
                        methodVisitor.VisitInsnAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            annotationDescriptor,
                            false),
                        currentAnnotationOffset,
                        true,
                        charBuffer);
                }
                currentInvisibleTypeAnnotationBytecodeOffset =
                    GetTypeAnnotationBytecodeOffset(
                        invisibleTypeAnnotationOffsets, ++currentInvisibleTypeAnnotationIndex);
            }
        }
        if (labels[codeLength] != null)
        {
            methodVisitor.VisitLabel(labels[codeLength]);
        }

        // Visit LocalVariableTable and LocalVariableTypeTable attributes.
        if (localVariableTableOffset != 0 && (context.ParsingOptions & SKIP_DEBUG) == 0)
        {
            int[] typeTable = null;
            if (localVariableTypeTableOffset != 0)
            {
                typeTable = new int[ReadUnsignedShort(localVariableTypeTableOffset) * 3];
                currentOffset = localVariableTypeTableOffset + 2;
                int typeTableIndex = typeTable.Length;
                while (typeTableIndex > 0)
                {
                    typeTable[--typeTableIndex] = currentOffset + 6;
                    typeTable[--typeTableIndex] = ReadUnsignedShort(currentOffset + 8);
                    typeTable[--typeTableIndex] = ReadUnsignedShort(currentOffset);
                    currentOffset += 10;
                }
            }
            int localVariableTableLength = ReadUnsignedShort(localVariableTableOffset);
            currentOffset = localVariableTableOffset + 2;
            while (localVariableTableLength-- > 0)
            {
                int startPc = ReadUnsignedShort(currentOffset);
                int length = ReadUnsignedShort(currentOffset + 2);
                string name = ReadUTF8(currentOffset + 4, charBuffer);
                string descriptor = ReadUTF8(currentOffset + 6, charBuffer);
                int index = ReadUnsignedShort(currentOffset + 8);
                currentOffset += 10;
                string signature = null;
                if (typeTable != null)
                {
                    for (int i = 0; i < typeTable.Length; i += 3)
                    {
                        if (typeTable[i] == startPc && typeTable[i + 1] == index)
                        {
                            signature = ReadUTF8(typeTable[i + 2], charBuffer);
                            break;
                        }
                    }
                }
                methodVisitor.VisitLocalVariable(
                    name, descriptor, signature, labels[startPc], labels[startPc + length], index);
            }
        }

        // Visit the local variable type annotations of the RuntimeVisibleTypeAnnotations attribute.
        if (visibleTypeAnnotationOffsets != null)
        {
            foreach (int typeAnnotationOffset in visibleTypeAnnotationOffsets)
            {
                int targetType = ReadByte(typeAnnotationOffset);
                if (targetType == TYPE_REFERENCE_LOCAL_VARIABLE
                    || targetType == TYPE_REFERENCE_RESOURCE_VARIABLE)
                {
                    currentOffset = ReadTypeAnnotationTarget(context, typeAnnotationOffset);
                    string annotationDescriptor = ReadUTF8(currentOffset, charBuffer);
                    currentOffset += 2;
                    ReadElementValues(
                        methodVisitor.VisitLocalVariableAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            context.CurrentLocalVariableAnnotationRangeStarts,
                            context.CurrentLocalVariableAnnotationRangeEnds,
                            context.CurrentLocalVariableAnnotationRangeIndices,
                            annotationDescriptor,
                            true),
                        currentOffset,
                        true,
                        charBuffer);
                }
            }
        }

        // Visit the local variable type annotations of the RuntimeInvisibleTypeAnnotations attribute.
        if (invisibleTypeAnnotationOffsets != null)
        {
            foreach (int typeAnnotationOffset in invisibleTypeAnnotationOffsets)
            {
                int targetType = ReadByte(typeAnnotationOffset);
                if (targetType == TYPE_REFERENCE_LOCAL_VARIABLE
                    || targetType == TYPE_REFERENCE_RESOURCE_VARIABLE)
                {
                    currentOffset = ReadTypeAnnotationTarget(context, typeAnnotationOffset);
                    string annotationDescriptor = ReadUTF8(currentOffset, charBuffer);
                    currentOffset += 2;
                    ReadElementValues(
                        methodVisitor.VisitLocalVariableAnnotation(
                            context.CurrentTypeAnnotationTarget,
                            context.CurrentTypeAnnotationTargetPath,
                            context.CurrentLocalVariableAnnotationRangeStarts,
                            context.CurrentLocalVariableAnnotationRangeEnds,
                            context.CurrentLocalVariableAnnotationRangeIndices,
                            annotationDescriptor,
                            false),
                        currentOffset,
                        true,
                        charBuffer);
                }
            }
        }

        // Visit the non standard attributes.
        while (attributes != null)
        {
            Attribute nextAttribute = attributes.nextAttribute;
            attributes.nextAttribute = null;
            methodVisitor.VisitAttribute(attributes);
            attributes = nextAttribute;
        }

        // Visit the max stack and max locals values.
        methodVisitor.VisitMaxs(maxStack, maxLocals);
    }

    /// <summary>
    /// Handles the bytecode offset of the next instruction to be visited. The default implementation
    /// does nothing. Subclasses can override this method to store the argument in a mutable field.
    /// </summary>
    /// <param name="bytecodeOffset">the bytecode offset of the next instruction to be visited.</param>
    protected virtual void ReadBytecodeInstructionOffset(int bytecodeOffset)
    {
        // Do nothing by default.
    }

    /// <summary>
    /// Returns the label corresponding to the given bytecode offset.
    /// </summary>
    /// <param name="bytecodeOffset">a bytecode offset in a method.</param>
    /// <param name="labels">the already created labels, indexed by their offset.</param>
    /// <returns>a non null Label, which must be equal to labels[bytecodeOffset].</returns>
    protected virtual Label ReadLabel(int bytecodeOffset, Label[] labels)
    {
        if (labels[bytecodeOffset] == null)
        {
            labels[bytecodeOffset] = new Label();
        }
        return labels[bytecodeOffset];
    }

    /// <summary>
    /// Creates a label without the <see cref="Label.FLAG_DEBUG_ONLY"/> flag set.
    /// </summary>
    /// <param name="bytecodeOffset">a bytecode offset in a method.</param>
    /// <param name="labels">the already created labels, indexed by their offset.</param>
    /// <returns>a Label without the FLAG_DEBUG_ONLY flag set.</returns>
    private Label CreateLabel(int bytecodeOffset, Label[] labels)
    {
        Label label = ReadLabel(bytecodeOffset, labels);
        label.Flags &= (short)~Label.FLAG_DEBUG_ONLY;
        return label;
    }

    /// <summary>
    /// Creates a label with the <see cref="Label.FLAG_DEBUG_ONLY"/> flag set, if there is no already
    /// existing label for the given bytecode offset.
    /// </summary>
    /// <param name="bytecodeOffset">a bytecode offset in a method.</param>
    /// <param name="labels">the already created labels, indexed by their offset.</param>
    private void CreateDebugLabel(int bytecodeOffset, Label[] labels)
    {
        if (labels[bytecodeOffset] == null)
        {
            ReadLabel(bytecodeOffset, labels).Flags |= Label.FLAG_DEBUG_ONLY;
        }
    }

    // ----------------------------------------------------------------------------------------------
    // Methods to parse annotations, type annotations and parameter annotations
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Parses a Runtime[In]VisibleTypeAnnotations attribute to find the offset of each type_annotation
    /// entry, and to visit the try catch block annotations.
    /// </summary>
    /// <param name="methodVisitor">the method visitor to be used.</param>
    /// <param name="context">information about the class being parsed.</param>
    /// <param name="runtimeTypeAnnotationsOffset">the start offset of the attribute.</param>
    /// <param name="visible">true if the attribute is RuntimeVisibleTypeAnnotations.</param>
    /// <returns>the start offset of each type_annotation entry.</returns>
    private int[] ReadTypeAnnotations(
        MethodVisitor methodVisitor,
        Context context,
        int runtimeTypeAnnotationsOffset,
        bool visible)
    {
        char[] charBuffer = context.CharBuffer;
        int currentOffset = runtimeTypeAnnotationsOffset;
        int[] typeAnnotationsOffsets = new int[ReadUnsignedShort(currentOffset)];
        currentOffset += 2;
        for (int i = 0; i < typeAnnotationsOffsets.Length; ++i)
        {
            typeAnnotationsOffsets[i] = currentOffset;
            int targetType = ReadInt(currentOffset);
            switch ((int)((uint)targetType >> 24))
            {
                case TYPE_REFERENCE_LOCAL_VARIABLE:
                case TYPE_REFERENCE_RESOURCE_VARIABLE:
                    int tableLength = ReadUnsignedShort(currentOffset + 1);
                    currentOffset += 3;
                    while (tableLength-- > 0)
                    {
                        int startPc = ReadUnsignedShort(currentOffset);
                        int length = ReadUnsignedShort(currentOffset + 2);
                        currentOffset += 6;
                        CreateLabel(startPc, context.CurrentMethodLabels);
                        CreateLabel(startPc + length, context.CurrentMethodLabels);
                    }
                    break;
                case TYPE_REFERENCE_CAST:
                case TYPE_REFERENCE_CONSTRUCTOR_INVOCATION_TYPE_ARGUMENT:
                case TYPE_REFERENCE_METHOD_INVOCATION_TYPE_ARGUMENT:
                case TYPE_REFERENCE_CONSTRUCTOR_REFERENCE_TYPE_ARGUMENT:
                case TYPE_REFERENCE_METHOD_REFERENCE_TYPE_ARGUMENT:
                    currentOffset += 4;
                    break;
                case TYPE_REFERENCE_CLASS_EXTENDS:
                case TYPE_REFERENCE_CLASS_TYPE_PARAMETER_BOUND:
                case TYPE_REFERENCE_METHOD_TYPE_PARAMETER_BOUND:
                case TYPE_REFERENCE_THROWS:
                case TYPE_REFERENCE_EXCEPTION_PARAMETER:
                case TYPE_REFERENCE_INSTANCEOF:
                case TYPE_REFERENCE_NEW:
                case TYPE_REFERENCE_CONSTRUCTOR_REFERENCE:
                case TYPE_REFERENCE_METHOD_REFERENCE:
                    currentOffset += 3;
                    break;
                case TYPE_REFERENCE_CLASS_TYPE_PARAMETER:
                case TYPE_REFERENCE_METHOD_TYPE_PARAMETER:
                case TYPE_REFERENCE_METHOD_FORMAL_PARAMETER:
                case TYPE_REFERENCE_FIELD:
                case TYPE_REFERENCE_METHOD_RETURN:
                case TYPE_REFERENCE_METHOD_RECEIVER:
                default:
                    throw new ArgumentException();
            }
            int pathLength = ReadByte(currentOffset);
            if ((int)((uint)targetType >> 24) == TYPE_REFERENCE_EXCEPTION_PARAMETER)
            {
                TypePath path = pathLength == 0 ? null : new TypePath(classFileBuffer, currentOffset);
                currentOffset += 1 + 2 * pathLength;
                string annotationDescriptor = ReadUTF8(currentOffset, charBuffer);
                currentOffset += 2;
                currentOffset =
                    ReadElementValues(
                        methodVisitor.VisitTryCatchAnnotation(
                            targetType & unchecked((int)0xFFFFFF00), path, annotationDescriptor, visible),
                        currentOffset,
                        true,
                        charBuffer);
            }
            else
            {
                currentOffset += 3 + 2 * pathLength;
                currentOffset =
                    ReadElementValues(
                        null, currentOffset, true, charBuffer);
            }
        }
        return typeAnnotationsOffsets;
    }

    /// <summary>
    /// Returns the bytecode offset corresponding to the specified type_annotation structure, or -1.
    /// </summary>
    /// <param name="typeAnnotationOffsets">the offset of each type_annotation entry, or null.</param>
    /// <param name="typeAnnotationIndex">the index in typeAnnotationOffsets.</param>
    /// <returns>the bytecode offset, or -1.</returns>
    private int GetTypeAnnotationBytecodeOffset(int[] typeAnnotationOffsets, int typeAnnotationIndex)
    {
        if (typeAnnotationOffsets == null
            || typeAnnotationIndex >= typeAnnotationOffsets.Length
            || ReadByte(typeAnnotationOffsets[typeAnnotationIndex]) < TYPE_REFERENCE_INSTANCEOF)
        {
            return -1;
        }
        return ReadUnsignedShort(typeAnnotationOffsets[typeAnnotationIndex] + 1);
    }

    /// <summary>
    /// Parses the header of a JVMS type_annotation structure to extract its target_type, target_info
    /// and target_path, and returns the start offset of the rest of the type_annotation structure.
    /// </summary>
    /// <param name="context">information about the class being parsed.</param>
    /// <param name="typeAnnotationOffset">the start offset of a type_annotation structure.</param>
    /// <returns>the start offset of the rest of the type_annotation structure.</returns>
    private int ReadTypeAnnotationTarget(Context context, int typeAnnotationOffset)
    {
        int currentOffset = typeAnnotationOffset;
        int targetType = ReadInt(typeAnnotationOffset);
        switch ((int)((uint)targetType >> 24))
        {
            case TYPE_REFERENCE_CLASS_TYPE_PARAMETER:
            case TYPE_REFERENCE_METHOD_TYPE_PARAMETER:
            case TYPE_REFERENCE_METHOD_FORMAL_PARAMETER:
                targetType &= unchecked((int)0xFFFF0000);
                currentOffset += 2;
                break;
            case TYPE_REFERENCE_FIELD:
            case TYPE_REFERENCE_METHOD_RETURN:
            case TYPE_REFERENCE_METHOD_RECEIVER:
                targetType &= unchecked((int)0xFF000000);
                currentOffset += 1;
                break;
            case TYPE_REFERENCE_LOCAL_VARIABLE:
            case TYPE_REFERENCE_RESOURCE_VARIABLE:
                targetType &= unchecked((int)0xFF000000);
                int tableLength = ReadUnsignedShort(currentOffset + 1);
                currentOffset += 3;
                context.CurrentLocalVariableAnnotationRangeStarts = new Label[tableLength];
                context.CurrentLocalVariableAnnotationRangeEnds = new Label[tableLength];
                context.CurrentLocalVariableAnnotationRangeIndices = new int[tableLength];
                for (int i = 0; i < tableLength; ++i)
                {
                    int startPc = ReadUnsignedShort(currentOffset);
                    int length = ReadUnsignedShort(currentOffset + 2);
                    int index = ReadUnsignedShort(currentOffset + 4);
                    currentOffset += 6;
                    context.CurrentLocalVariableAnnotationRangeStarts[i] =
                        CreateLabel(startPc, context.CurrentMethodLabels);
                    context.CurrentLocalVariableAnnotationRangeEnds[i] =
                        CreateLabel(startPc + length, context.CurrentMethodLabels);
                    context.CurrentLocalVariableAnnotationRangeIndices[i] = index;
                }
                break;
            case TYPE_REFERENCE_CAST:
            case TYPE_REFERENCE_CONSTRUCTOR_INVOCATION_TYPE_ARGUMENT:
            case TYPE_REFERENCE_METHOD_INVOCATION_TYPE_ARGUMENT:
            case TYPE_REFERENCE_CONSTRUCTOR_REFERENCE_TYPE_ARGUMENT:
            case TYPE_REFERENCE_METHOD_REFERENCE_TYPE_ARGUMENT:
                targetType &= unchecked((int)0xFF0000FF);
                currentOffset += 4;
                break;
            case TYPE_REFERENCE_CLASS_EXTENDS:
            case TYPE_REFERENCE_CLASS_TYPE_PARAMETER_BOUND:
            case TYPE_REFERENCE_METHOD_TYPE_PARAMETER_BOUND:
            case TYPE_REFERENCE_THROWS:
            case TYPE_REFERENCE_EXCEPTION_PARAMETER:
                targetType &= unchecked((int)0xFFFFFF00);
                currentOffset += 3;
                break;
            case TYPE_REFERENCE_INSTANCEOF:
            case TYPE_REFERENCE_NEW:
            case TYPE_REFERENCE_CONSTRUCTOR_REFERENCE:
            case TYPE_REFERENCE_METHOD_REFERENCE:
                targetType &= unchecked((int)0xFF000000);
                currentOffset += 3;
                break;
            default:
                throw new ArgumentException();
        }
        context.CurrentTypeAnnotationTarget = targetType;
        int pathLength = ReadByte(currentOffset);
        context.CurrentTypeAnnotationTargetPath =
            pathLength == 0 ? null : new TypePath(classFileBuffer, currentOffset);
        return currentOffset + 1 + 2 * pathLength;
    }

    /// <summary>
    /// Reads a Runtime[In]VisibleParameterAnnotations attribute and makes the given visitor visit it.
    /// </summary>
    /// <param name="methodVisitor">the visitor that must visit the parameter annotations.</param>
    /// <param name="context">information about the class being parsed.</param>
    /// <param name="runtimeParameterAnnotationsOffset">the start offset of the attribute.</param>
    /// <param name="visible">true if the attribute is RuntimeVisibleParameterAnnotations.</param>
    private void ReadParameterAnnotations(
        MethodVisitor methodVisitor,
        Context context,
        int runtimeParameterAnnotationsOffset,
        bool visible)
    {
        int currentOffset = runtimeParameterAnnotationsOffset;
        int numParameters = classFileBuffer[currentOffset++] & 0xFF;
        methodVisitor.VisitAnnotableParameterCount(numParameters, visible);
        char[] charBuffer = context.CharBuffer;
        for (int i = 0; i < numParameters; ++i)
        {
            int numAnnotations = ReadUnsignedShort(currentOffset);
            currentOffset += 2;
            while (numAnnotations-- > 0)
            {
                string annotationDescriptor = ReadUTF8(currentOffset, charBuffer);
                currentOffset += 2;
                currentOffset =
                    ReadElementValues(
                        methodVisitor.VisitParameterAnnotation(i, annotationDescriptor, visible),
                        currentOffset,
                        true,
                        charBuffer);
            }
        }
    }

    /// <summary>
    /// Reads the element values of a JVMS 'annotation' structure and makes the given visitor visit
    /// them.
    /// </summary>
    /// <param name="annotationVisitor">the visitor that must visit the values.</param>
    /// <param name="annotationOffset">the start offset of an 'annotation' structure.</param>
    /// <param name="named">if the annotation values are named or not.</param>
    /// <param name="charBuffer">the buffer used to read strings in the constant pool.</param>
    /// <returns>the end offset of the JVMS 'annotation' or 'array_value' structure.</returns>
    private int ReadElementValues(
        AnnotationVisitor annotationVisitor,
        int annotationOffset,
        bool named,
        char[] charBuffer)
    {
        int currentOffset = annotationOffset;
        int numElementValuePairs = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        if (named)
        {
            while (numElementValuePairs-- > 0)
            {
                string elementName = ReadUTF8(currentOffset, charBuffer);
                currentOffset =
                    ReadElementValue(annotationVisitor, currentOffset + 2, elementName, charBuffer);
            }
        }
        else
        {
            while (numElementValuePairs-- > 0)
            {
                currentOffset =
                    ReadElementValue(annotationVisitor, currentOffset, null, charBuffer);
            }
        }
        if (annotationVisitor != null)
        {
            annotationVisitor.VisitEnd();
        }
        return currentOffset;
    }

    /// <summary>
    /// Reads a JVMS 'element_value' structure and makes the given visitor visit it.
    /// </summary>
    /// <param name="annotationVisitor">the visitor that must visit the element_value structure.</param>
    /// <param name="elementValueOffset">the start offset of the element_value structure.</param>
    /// <param name="elementName">the name of the element_value structure, or null.</param>
    /// <param name="charBuffer">the buffer used to read strings in the constant pool.</param>
    /// <returns>the end offset of the JVMS 'element_value' structure.</returns>
    private int ReadElementValue(
        AnnotationVisitor annotationVisitor,
        int elementValueOffset,
        string elementName,
        char[] charBuffer)
    {
        int currentOffset = elementValueOffset;
        if (annotationVisitor == null)
        {
            switch (classFileBuffer[currentOffset] & 0xFF)
            {
                case 'e':
                    return currentOffset + 5;
                case '@':
                    return ReadElementValues(null, currentOffset + 3, true, charBuffer);
                case '[':
                    return ReadElementValues(null, currentOffset + 1, false, charBuffer);
                default:
                    return currentOffset + 3;
            }
        }
        switch (classFileBuffer[currentOffset++] & 0xFF)
        {
            case 'B':
                annotationVisitor.Visit(
                    elementName, (byte)ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset)]));
                currentOffset += 2;
                break;
            case 'C':
                annotationVisitor.Visit(
                    elementName, (char)ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset)]));
                currentOffset += 2;
                break;
            case 'D':
            case 'F':
            case 'I':
            case 'J':
                annotationVisitor.Visit(
                    elementName, ReadConst(ReadUnsignedShort(currentOffset), charBuffer));
                currentOffset += 2;
                break;
            case 'S':
                annotationVisitor.Visit(
                    elementName, (short)ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset)]));
                currentOffset += 2;
                break;
            case 'Z':
                annotationVisitor.Visit(
                    elementName,
                    ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset)]) == 0
                        ? false
                        : true);
                currentOffset += 2;
                break;
            case 's':
                annotationVisitor.Visit(elementName, ReadUTF8(currentOffset, charBuffer));
                currentOffset += 2;
                break;
            case 'e':
                annotationVisitor.VisitEnum(
                    elementName,
                    ReadUTF8(currentOffset, charBuffer),
                    ReadUTF8(currentOffset + 2, charBuffer));
                currentOffset += 4;
                break;
            case 'c':
                annotationVisitor.Visit(elementName, Type.GetType(ReadUTF8(currentOffset, charBuffer)));
                currentOffset += 2;
                break;
            case '@':
                currentOffset =
                    ReadElementValues(
                        annotationVisitor.VisitAnnotation(elementName, ReadUTF8(currentOffset, charBuffer)),
                        currentOffset + 2,
                        true,
                        charBuffer);
                break;
            case '[':
                int numValues = ReadUnsignedShort(currentOffset);
                currentOffset += 2;
                if (numValues == 0)
                {
                    return ReadElementValues(
                        annotationVisitor.VisitArray(elementName),
                        currentOffset - 2,
                        false,
                        charBuffer);
                }
                switch (classFileBuffer[currentOffset] & 0xFF)
                {
                    case 'B':
                        byte[] byteValues = new byte[numValues];
                        for (int i = 0; i < numValues; i++)
                        {
                            byteValues[i] = (byte)ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)]);
                            currentOffset += 3;
                        }
                        annotationVisitor.Visit(elementName, byteValues);
                        break;
                    case 'Z':
                        bool[] booleanValues = new bool[numValues];
                        for (int i = 0; i < numValues; i++)
                        {
                            booleanValues[i] = ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)]) != 0;
                            currentOffset += 3;
                        }
                        annotationVisitor.Visit(elementName, booleanValues);
                        break;
                    case 'S':
                        short[] shortValues = new short[numValues];
                        for (int i = 0; i < numValues; i++)
                        {
                            shortValues[i] = (short)ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)]);
                            currentOffset += 3;
                        }
                        annotationVisitor.Visit(elementName, shortValues);
                        break;
                    case 'C':
                        char[] charValues = new char[numValues];
                        for (int i = 0; i < numValues; i++)
                        {
                            charValues[i] = (char)ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)]);
                            currentOffset += 3;
                        }
                        annotationVisitor.Visit(elementName, charValues);
                        break;
                    case 'I':
                        int[] intValues = new int[numValues];
                        for (int i = 0; i < numValues; i++)
                        {
                            intValues[i] = ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)]);
                            currentOffset += 3;
                        }
                        annotationVisitor.Visit(elementName, intValues);
                        break;
                    case 'J':
                        long[] longValues = new long[numValues];
                        for (int i = 0; i < numValues; i++)
                        {
                            longValues[i] = ReadLong(cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)]);
                            currentOffset += 3;
                        }
                        annotationVisitor.Visit(elementName, longValues);
                        break;
                    case 'F':
                        float[] floatValues = new float[numValues];
                        for (int i = 0; i < numValues; i++)
                        {
                            floatValues[i] =
                                BitConverter.Int32BitsToSingle(
                                    ReadInt(cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)]));
                            currentOffset += 3;
                        }
                        annotationVisitor.Visit(elementName, floatValues);
                        break;
                    case 'D':
                        double[] doubleValues = new double[numValues];
                        for (int i = 0; i < numValues; i++)
                        {
                            doubleValues[i] =
                                BitConverter.Int64BitsToDouble(
                                    ReadLong(cpInfoOffsets[ReadUnsignedShort(currentOffset + 1)]));
                            currentOffset += 3;
                        }
                        annotationVisitor.Visit(elementName, doubleValues);
                        break;
                    default:
                        currentOffset =
                            ReadElementValues(
                                annotationVisitor.VisitArray(elementName),
                                currentOffset - 2,
                                false,
                                charBuffer);
                        break;
                }
                break;
            default:
                throw new ArgumentException();
        }
        return currentOffset;
    }

    // ----------------------------------------------------------------------------------------------
    // Methods to parse stack map frames
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Computes the implicit frame of the method currently being parsed and stores it in the given
    /// context.
    /// </summary>
    /// <param name="context">information about the class being parsed.</param>
    private void ComputeImplicitFrame(Context context)
    {
        string methodDescriptor = context.CurrentMethodDescriptor;
        object[] locals = context.CurrentFrameLocalTypes;
        int numLocal = 0;
        if ((context.CurrentMethodAccessFlags & Opcodes.ACC_STATIC) == 0)
        {
            if ("<init>" == context.CurrentMethodName)
            {
                locals[numLocal++] = Opcodes.UNINITIALIZED_THIS;
            }
            else
            {
                locals[numLocal++] = ReadClass(header + 2, context.CharBuffer);
            }
        }
        int currentMethodDescritorOffset = 1;
        while (true)
        {
            int currentArgumentDescriptorStartOffset = currentMethodDescritorOffset;
            switch (methodDescriptor[currentMethodDescritorOffset++])
            {
                case 'Z':
                case 'C':
                case 'B':
                case 'S':
                case 'I':
                    locals[numLocal++] = Opcodes.INTEGER;
                    break;
                case 'F':
                    locals[numLocal++] = Opcodes.FLOAT;
                    break;
                case 'J':
                    locals[numLocal++] = Opcodes.LONG;
                    break;
                case 'D':
                    locals[numLocal++] = Opcodes.DOUBLE;
                    break;
                case '[':
                    while (methodDescriptor[currentMethodDescritorOffset] == '[')
                    {
                        ++currentMethodDescritorOffset;
                    }
                    if (methodDescriptor[currentMethodDescritorOffset] == 'L')
                    {
                        ++currentMethodDescritorOffset;
                        while (methodDescriptor[currentMethodDescritorOffset] != ';')
                        {
                            ++currentMethodDescritorOffset;
                        }
                    }
                    locals[numLocal++] =
                        methodDescriptor.Substring(
                            currentArgumentDescriptorStartOffset, ++currentMethodDescritorOffset - currentArgumentDescriptorStartOffset);
                    break;
                case 'L':
                    while (methodDescriptor[currentMethodDescritorOffset] != ';')
                    {
                        ++currentMethodDescritorOffset;
                    }
                    locals[numLocal++] =
                        methodDescriptor.Substring(
                            currentArgumentDescriptorStartOffset + 1, currentMethodDescritorOffset++ - (currentArgumentDescriptorStartOffset + 1));
                    break;
                default:
                    context.CurrentFrameLocalCount = numLocal;
                    return;
            }
        }
    }

    /// <summary>
    /// Reads a JVMS 'stack_map_frame' structure and stores the result in the given context.
    /// </summary>
    /// <param name="stackMapFrameOffset">the start offset of the stack_map_frame structure.</param>
    /// <param name="compressed">true to read a 'stack_map_frame', false to read a 'full_frame'.</param>
    /// <param name="expand">if the stack map frame must be expanded.</param>
    /// <param name="context">where the parsed stack map frame must be stored.</param>
    /// <returns>the end offset of the JVMS 'stack_map_frame' or 'full_frame' structure.</returns>
    private int ReadStackMapFrame(
        int stackMapFrameOffset,
        bool compressed,
        bool expand,
        Context context)
    {
        int currentOffset = stackMapFrameOffset;
        char[] charBuffer = context.CharBuffer;
        Label[] labels = context.CurrentMethodLabels;
        int frameType;
        if (compressed)
        {
            frameType = classFileBuffer[currentOffset++] & 0xFF;
        }
        else
        {
            frameType = Frame.FULL_FRAME;
            context.CurrentFrameOffset = -1;
        }
        int offsetDelta;
        context.CurrentFrameLocalCountDelta = 0;
        if (frameType < Frame.SAME_LOCALS_1_STACK_ITEM_FRAME)
        {
            offsetDelta = frameType;
            context.CurrentFrameType = Opcodes.F_SAME;
            context.CurrentFrameStackCount = 0;
        }
        else if (frameType < Frame.RESERVED)
        {
            offsetDelta = frameType - Frame.SAME_LOCALS_1_STACK_ITEM_FRAME;
            currentOffset =
                ReadVerificationTypeInfo(
                    currentOffset, context.CurrentFrameStackTypes, 0, charBuffer, labels);
            context.CurrentFrameType = Opcodes.F_SAME1;
            context.CurrentFrameStackCount = 1;
        }
        else if (frameType >= Frame.SAME_LOCALS_1_STACK_ITEM_FRAME_EXTENDED)
        {
            offsetDelta = ReadUnsignedShort(currentOffset);
            currentOffset += 2;
            if (frameType == Frame.SAME_LOCALS_1_STACK_ITEM_FRAME_EXTENDED)
            {
                currentOffset =
                    ReadVerificationTypeInfo(
                        currentOffset, context.CurrentFrameStackTypes, 0, charBuffer, labels);
                context.CurrentFrameType = Opcodes.F_SAME1;
                context.CurrentFrameStackCount = 1;
            }
            else if (frameType >= Frame.CHOP_FRAME && frameType < Frame.SAME_FRAME_EXTENDED)
            {
                context.CurrentFrameType = Opcodes.F_CHOP;
                context.CurrentFrameLocalCountDelta = Frame.SAME_FRAME_EXTENDED - frameType;
                context.CurrentFrameLocalCount -= context.CurrentFrameLocalCountDelta;
                context.CurrentFrameStackCount = 0;
            }
            else if (frameType == Frame.SAME_FRAME_EXTENDED)
            {
                context.CurrentFrameType = Opcodes.F_SAME;
                context.CurrentFrameStackCount = 0;
            }
            else if (frameType < Frame.FULL_FRAME)
            {
                int local = expand ? context.CurrentFrameLocalCount : 0;
                for (int k = frameType - Frame.SAME_FRAME_EXTENDED; k > 0; k--)
                {
                    currentOffset =
                        ReadVerificationTypeInfo(
                            currentOffset, context.CurrentFrameLocalTypes, local++, charBuffer, labels);
                }
                context.CurrentFrameType = Opcodes.F_APPEND;
                context.CurrentFrameLocalCountDelta = frameType - Frame.SAME_FRAME_EXTENDED;
                context.CurrentFrameLocalCount += context.CurrentFrameLocalCountDelta;
                context.CurrentFrameStackCount = 0;
            }
            else
            {
                int numberOfLocals = ReadUnsignedShort(currentOffset);
                currentOffset += 2;
                context.CurrentFrameType = Opcodes.F_FULL;
                context.CurrentFrameLocalCountDelta = numberOfLocals;
                context.CurrentFrameLocalCount = numberOfLocals;
                for (int local = 0; local < numberOfLocals; ++local)
                {
                    currentOffset =
                        ReadVerificationTypeInfo(
                            currentOffset, context.CurrentFrameLocalTypes, local, charBuffer, labels);
                }
                int numberOfStackItems = ReadUnsignedShort(currentOffset);
                currentOffset += 2;
                context.CurrentFrameStackCount = numberOfStackItems;
                for (int stack = 0; stack < numberOfStackItems; ++stack)
                {
                    currentOffset =
                        ReadVerificationTypeInfo(
                            currentOffset, context.CurrentFrameStackTypes, stack, charBuffer, labels);
                }
            }
        }
        else
        {
            throw new ArgumentException();
        }
        context.CurrentFrameOffset += offsetDelta + 1;
        CreateLabel(context.CurrentFrameOffset, labels);
        return currentOffset;
    }

    /// <summary>
    /// Reads a JVMS 'verification_type_info' structure and stores it at the given index in the given
    /// array.
    /// </summary>
    /// <param name="verificationTypeInfoOffset">the start offset of the structure.</param>
    /// <param name="frame">the array where the parsed type must be stored.</param>
    /// <param name="index">the index in 'frame' where the parsed type must be stored.</param>
    /// <param name="charBuffer">the buffer used to read strings.</param>
    /// <param name="labels">the labels of the method currently being parsed.</param>
    /// <returns>the end offset of the JVMS 'verification_type_info' structure.</returns>
    private int ReadVerificationTypeInfo(
        int verificationTypeInfoOffset,
        object[] frame,
        int index,
        char[] charBuffer,
        Label[] labels)
    {
        int currentOffset = verificationTypeInfoOffset;
        int tag = classFileBuffer[currentOffset++] & 0xFF;
        switch (tag)
        {
            case Frame.ITEM_TOP:
                frame[index] = Opcodes.TOP;
                break;
            case Frame.ITEM_INTEGER:
                frame[index] = Opcodes.INTEGER;
                break;
            case Frame.ITEM_FLOAT:
                frame[index] = Opcodes.FLOAT;
                break;
            case Frame.ITEM_DOUBLE:
                frame[index] = Opcodes.DOUBLE;
                break;
            case Frame.ITEM_LONG:
                frame[index] = Opcodes.LONG;
                break;
            case Frame.ITEM_NULL:
                frame[index] = Opcodes.NULL;
                break;
            case Frame.ITEM_UNINITIALIZED_THIS:
                frame[index] = Opcodes.UNINITIALIZED_THIS;
                break;
            case Frame.ITEM_OBJECT:
                frame[index] = ReadClass(currentOffset, charBuffer);
                currentOffset += 2;
                break;
            case Frame.ITEM_UNINITIALIZED:
                frame[index] = CreateLabel(ReadUnsignedShort(currentOffset), labels);
                currentOffset += 2;
                break;
            default:
                throw new ArgumentException();
        }
        return currentOffset;
    }

    // ----------------------------------------------------------------------------------------------
    // Methods to parse attributes
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the offset in <see cref="classFileBuffer"/> of the first ClassFile's 'attributes'
    /// array field entry.
    /// </summary>
    /// <returns>the offset of the first attribute entry.</returns>
    internal int GetFirstAttributeOffset()
    {
        int currentOffset = header + 8 + ReadUnsignedShort(header + 6) * 2;

        int fieldsCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (fieldsCount-- > 0)
        {
            int attributesCount = ReadUnsignedShort(currentOffset + 6);
            currentOffset += 8;
            while (attributesCount-- > 0)
            {
                currentOffset += 6 + ReadInt(currentOffset + 2);
            }
        }

        int methodsCount = ReadUnsignedShort(currentOffset);
        currentOffset += 2;
        while (methodsCount-- > 0)
        {
            int attributesCount = ReadUnsignedShort(currentOffset + 6);
            currentOffset += 8;
            while (attributesCount-- > 0)
            {
                currentOffset += 6 + ReadInt(currentOffset + 2);
            }
        }

        return currentOffset + 2;
    }

    /// <summary>
    /// Reads the BootstrapMethods attribute to compute the offset of each bootstrap method.
    /// </summary>
    /// <param name="maxStringLength">a conservative estimate of the maximum string length.</param>
    /// <returns>the offsets of the bootstrap methods.</returns>
    private int[] ReadBootstrapMethodsAttribute(int maxStringLength)
    {
        char[] charBuffer = new char[maxStringLength];
        int currentAttributeOffset = GetFirstAttributeOffset();
        for (int i = ReadUnsignedShort(currentAttributeOffset - 2); i > 0; --i)
        {
            string attributeName = ReadUTF8(currentAttributeOffset, charBuffer);
            int attributeLength = ReadInt(currentAttributeOffset + 2);
            currentAttributeOffset += 6;
            if (Constants.BOOTSTRAP_METHODS == attributeName)
            {
                int[] result = new int[ReadUnsignedShort(currentAttributeOffset)];
                int currentBootstrapMethodOffset = currentAttributeOffset + 2;
                for (int j = 0; j < result.Length; ++j)
                {
                    result[j] = currentBootstrapMethodOffset;
                    currentBootstrapMethodOffset +=
                        4 + ReadUnsignedShort(currentBootstrapMethodOffset + 2) * 2;
                }
                return result;
            }
            currentAttributeOffset += attributeLength;
        }
        throw new ArgumentException();
    }

    /// <summary>
    /// Reads a non standard JVMS 'attribute' structure in <see cref="classFileBuffer"/>.
    /// </summary>
    /// <param name="attributePrototypes">prototypes of the attributes that must be parsed.</param>
    /// <param name="type">the type of the attribute.</param>
    /// <param name="offset">the start offset of the attribute's content.</param>
    /// <param name="length">the length of the attribute's content.</param>
    /// <param name="charBuffer">the buffer to be used to read strings.</param>
    /// <param name="codeAttributeOffset">the start offset of the enclosing Code attribute, or -1.</param>
    /// <param name="labels">the labels of the method's code, or null.</param>
    /// <returns>the attribute that has been read.</returns>
    private Attribute ReadAttribute(
        Attribute[] attributePrototypes,
        string type,
        int offset,
        int length,
        char[] charBuffer,
        int codeAttributeOffset,
        Label[] labels)
    {
        if (length > classFileBuffer.Length - offset)
        {
            throw new ArgumentException();
        }
        foreach (Attribute attributePrototype in attributePrototypes)
        {
            if (attributePrototype.type == type)
            {
                return attributePrototype.Read(
                    this, offset, length, charBuffer, codeAttributeOffset, labels);
            }
        }
        return new Attribute.UnknownAttribute(type).Read(this, offset, length, null, -1, null);
    }

    // -----------------------------------------------------------------------------------------------
    // Utility methods: low level parsing
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the number of entries in the class's constant pool table.
    /// </summary>
    /// <returns>the number of entries in the class's constant pool table.</returns>
    public int GetItemCount()
    {
        return cpInfoOffsets.Length;
    }

    /// <summary>
    /// Returns the start offset in this <see cref="ClassReader"/> of a JVMS 'cp_info' structure,
    /// plus one.
    /// </summary>
    /// <param name="constantPoolEntryIndex">the index of a constant pool entry.</param>
    /// <returns>the start offset, plus one.</returns>
    public int GetItem(int constantPoolEntryIndex)
    {
        return cpInfoOffsets[constantPoolEntryIndex];
    }

    /// <summary>
    /// Returns a conservative estimate of the maximum length of the strings contained in the class's
    /// constant pool table.
    /// </summary>
    /// <returns>a conservative estimate of the maximum string length.</returns>
    public int GetMaxStringLength()
    {
        return maxStringLength;
    }

    /// <summary>
    /// Reads a byte value in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start offset of the value to be read.</param>
    /// <returns>the read value.</returns>
    public int ReadByte(int offset)
    {
        return classFileBuffer[offset] & 0xFF;
    }

    /// <summary>
    /// Reads several bytes in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start offset of the bytes to be read.</param>
    /// <param name="length">the number of bytes to read.</param>
    /// <returns>the read bytes.</returns>
    public byte[] ReadBytes(int offset, int length)
    {
        byte[] result = new byte[length];
        Array.Copy(classFileBuffer, offset, result, 0, length);
        return result;
    }

    /// <summary>
    /// Reads an unsigned short value in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start index of the value to be read.</param>
    /// <returns>the read value.</returns>
    public int ReadUnsignedShort(int offset)
    {
        byte[] classBuffer = classFileBuffer;
        return ((classBuffer[offset] & 0xFF) << 8) | (classBuffer[offset + 1] & 0xFF);
    }

    /// <summary>
    /// Reads a signed short value in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start offset of the value to be read.</param>
    /// <returns>the read value.</returns>
    public short ReadShort(int offset)
    {
        byte[] classBuffer = classFileBuffer;
        return (short)(((classBuffer[offset] & 0xFF) << 8) | (classBuffer[offset + 1] & 0xFF));
    }

    /// <summary>
    /// Reads a signed int value in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start offset of the value to be read.</param>
    /// <returns>the read value.</returns>
    public int ReadInt(int offset)
    {
        byte[] classBuffer = classFileBuffer;
        return ((classBuffer[offset] & 0xFF) << 24)
            | ((classBuffer[offset + 1] & 0xFF) << 16)
            | ((classBuffer[offset + 2] & 0xFF) << 8)
            | (classBuffer[offset + 3] & 0xFF);
    }

    /// <summary>
    /// Reads a signed long value in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start offset of the value to be read.</param>
    /// <returns>the read value.</returns>
    public long ReadLong(int offset)
    {
        long l1 = ReadInt(offset);
        long l0 = ReadInt(offset + 4) & 0xFFFFFFFFL;
        return (l1 << 32) | l0;
    }

    /// <summary>
    /// Reads a CONSTANT_Utf8 constant pool entry in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start offset of an unsigned short value whose value is the index of
    /// a CONSTANT_Utf8 entry.</param>
    /// <param name="charBuffer">the buffer used to read the string.</param>
    /// <returns>the String corresponding to the specified CONSTANT_Utf8 entry.</returns>
    public string ReadUTF8(int offset, char[] charBuffer)
    {
        int constantPoolEntryIndex = ReadUnsignedShort(offset);
        if (offset == 0 || constantPoolEntryIndex == 0)
        {
            return null;
        }
        return ReadUtf(constantPoolEntryIndex, charBuffer);
    }

    /// <summary>
    /// Reads a CONSTANT_Utf8 constant pool entry in <see cref="classFileBuffer"/>.
    /// </summary>
    /// <param name="constantPoolEntryIndex">the index of a CONSTANT_Utf8 entry.</param>
    /// <param name="charBuffer">the buffer used to read the string.</param>
    /// <returns>the String corresponding to the specified CONSTANT_Utf8 entry.</returns>
    internal string ReadUtf(int constantPoolEntryIndex, char[] charBuffer)
    {
        string value = constantUtf8Values[constantPoolEntryIndex];
        if (value != null)
        {
            return value;
        }
        int cpInfoOffset = cpInfoOffsets[constantPoolEntryIndex];
        return constantUtf8Values[constantPoolEntryIndex] =
            ReadUtf(cpInfoOffset + 2, ReadUnsignedShort(cpInfoOffset), charBuffer);
    }

    /// <summary>
    /// Reads an UTF8 string in <see cref="classFileBuffer"/>.
    /// </summary>
    /// <param name="utfOffset">the start offset of the UTF8 string to be read.</param>
    /// <param name="utfLength">the length of the UTF8 string to be read.</param>
    /// <param name="charBuffer">the buffer used to read the string.</param>
    /// <returns>the String corresponding to the specified UTF8 string.</returns>
    private string ReadUtf(int utfOffset, int utfLength, char[] charBuffer)
    {
        int currentOffset = utfOffset;
        int endOffset = currentOffset + utfLength;
        int strLength = 0;
        byte[] classBuffer = classFileBuffer;
        while (currentOffset < endOffset)
        {
            int currentByte = classBuffer[currentOffset++];
            if ((currentByte & 0x80) == 0)
            {
                charBuffer[strLength++] = (char)(currentByte & 0x7F);
            }
            else if ((currentByte & 0xE0) == 0xC0)
            {
                charBuffer[strLength++] =
                    (char)(((currentByte & 0x1F) << 6) + (classBuffer[currentOffset++] & 0x3F));
            }
            else
            {
                charBuffer[strLength++] =
                    (char)(((currentByte & 0xF) << 12)
                        + ((classBuffer[currentOffset++] & 0x3F) << 6)
                        + (classBuffer[currentOffset++] & 0x3F));
            }
        }
        return new string(charBuffer, 0, strLength);
    }

    /// <summary>
    /// Reads a CONSTANT_Class, CONSTANT_String, CONSTANT_MethodType, CONSTANT_Module or
    /// CONSTANT_Package constant pool entry.
    /// </summary>
    /// <param name="offset">the start offset of an unsigned short value.</param>
    /// <param name="charBuffer">the buffer used to read the item.</param>
    /// <returns>the String corresponding to the specified constant pool entry.</returns>
    private string ReadStringish(int offset, char[] charBuffer)
    {
        return ReadUTF8(cpInfoOffsets[ReadUnsignedShort(offset)], charBuffer);
    }

    /// <summary>
    /// Reads a CONSTANT_Class constant pool entry in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start offset of an unsigned short value.</param>
    /// <param name="charBuffer">the buffer used to read the item.</param>
    /// <returns>the String corresponding to the specified CONSTANT_Class entry.</returns>
    public string ReadClass(int offset, char[] charBuffer)
    {
        return ReadStringish(offset, charBuffer);
    }

    /// <summary>
    /// Reads a CONSTANT_Module constant pool entry in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start offset of an unsigned short value.</param>
    /// <param name="charBuffer">the buffer used to read the item.</param>
    /// <returns>the String corresponding to the specified CONSTANT_Module entry.</returns>
    public string ReadModule(int offset, char[] charBuffer)
    {
        return ReadStringish(offset, charBuffer);
    }

    /// <summary>
    /// Reads a CONSTANT_Package constant pool entry in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="offset">the start offset of an unsigned short value.</param>
    /// <param name="charBuffer">the buffer used to read the item.</param>
    /// <returns>the String corresponding to the specified CONSTANT_Package entry.</returns>
    public string ReadPackage(int offset, char[] charBuffer)
    {
        return ReadStringish(offset, charBuffer);
    }

    /// <summary>
    /// Reads a CONSTANT_Dynamic constant pool entry in <see cref="classFileBuffer"/>.
    /// </summary>
    /// <param name="constantPoolEntryIndex">the index of a CONSTANT_Dynamic entry.</param>
    /// <param name="charBuffer">the buffer used to read the string.</param>
    /// <returns>the ConstantDynamic corresponding to the specified CONSTANT_Dynamic entry.</returns>
    private ConstantDynamic ReadConstantDynamic(int constantPoolEntryIndex, char[] charBuffer)
    {
        ConstantDynamic constantDynamic = constantDynamicValues[constantPoolEntryIndex];
        if (constantDynamic != null)
        {
            return constantDynamic;
        }
        int cpInfoOffset = cpInfoOffsets[constantPoolEntryIndex];
        int nameAndTypeCpInfoOffset = cpInfoOffsets[ReadUnsignedShort(cpInfoOffset + 2)];
        string name = ReadUTF8(nameAndTypeCpInfoOffset, charBuffer);
        string descriptor = ReadUTF8(nameAndTypeCpInfoOffset + 2, charBuffer);
        int bootstrapMethodOffset = bootstrapMethodOffsets[ReadUnsignedShort(cpInfoOffset)];
        Handle handle = (Handle)ReadConst(ReadUnsignedShort(bootstrapMethodOffset), charBuffer);
        object[] bootstrapMethodArguments = new object[ReadUnsignedShort(bootstrapMethodOffset + 2)];
        bootstrapMethodOffset += 4;
        for (int i = 0; i < bootstrapMethodArguments.Length; i++)
        {
            bootstrapMethodArguments[i] = ReadConst(ReadUnsignedShort(bootstrapMethodOffset), charBuffer);
            bootstrapMethodOffset += 2;
        }
        return constantDynamicValues[constantPoolEntryIndex] =
            new ConstantDynamic(name, descriptor, handle, bootstrapMethodArguments);
    }

    /// <summary>
    /// Reads a numeric or string constant pool entry in this <see cref="ClassReader"/>.
    /// </summary>
    /// <param name="constantPoolEntryIndex">the index of a constant pool entry.</param>
    /// <param name="charBuffer">the buffer used to read strings.</param>
    /// <returns>the corresponding object (Integer, Float, Long, Double, String, Type, Handle or ConstantDynamic).</returns>
    public object ReadConst(int constantPoolEntryIndex, char[] charBuffer)
    {
        int cpInfoOffset = cpInfoOffsets[constantPoolEntryIndex];
        switch (classFileBuffer[cpInfoOffset - 1])
        {
            case Symbol.CONSTANT_INTEGER_TAG:
                return ReadInt(cpInfoOffset);
            case Symbol.CONSTANT_FLOAT_TAG:
                return BitConverter.Int32BitsToSingle(ReadInt(cpInfoOffset));
            case Symbol.CONSTANT_LONG_TAG:
                return ReadLong(cpInfoOffset);
            case Symbol.CONSTANT_DOUBLE_TAG:
                return BitConverter.Int64BitsToDouble(ReadLong(cpInfoOffset));
            case Symbol.CONSTANT_CLASS_TAG:
                return Type.GetObjectType(ReadUTF8(cpInfoOffset, charBuffer));
            case Symbol.CONSTANT_STRING_TAG:
                return ReadUTF8(cpInfoOffset, charBuffer);
            case Symbol.CONSTANT_METHOD_TYPE_TAG:
                return Type.GetMethodType(ReadUTF8(cpInfoOffset, charBuffer));
            case Symbol.CONSTANT_METHOD_HANDLE_TAG:
                int referenceKind = ReadByte(cpInfoOffset);
                int referenceCpInfoOffset = cpInfoOffsets[ReadUnsignedShort(cpInfoOffset + 1)];
                int nameAndTypeCpInfoOffset = cpInfoOffsets[ReadUnsignedShort(referenceCpInfoOffset + 2)];
                string owner = ReadClass(referenceCpInfoOffset, charBuffer);
                string name = ReadUTF8(nameAndTypeCpInfoOffset, charBuffer);
                string descriptor = ReadUTF8(nameAndTypeCpInfoOffset + 2, charBuffer);
                bool isInterface =
                    classFileBuffer[referenceCpInfoOffset - 1] == Symbol.CONSTANT_INTERFACE_METHODREF_TAG;
                return new Handle(referenceKind, owner, name, descriptor, isInterface);
            case Symbol.CONSTANT_DYNAMIC_TAG:
                return ReadConstantDynamic(constantPoolEntryIndex, charBuffer);
            default:
                throw new ArgumentException();
        }
    }
}
