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
/// A <see cref="FieldVisitor"/> that generates a corresponding 'field_info' structure, as defined in the
/// Java Virtual Machine Specification (JVMS).
/// </para><b>Author:</b> Eric Bruneton
/// </summary>
/// <remarks>
/// See <see href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.5">JVMS 4.5</see>.
/// </remarks>
internal sealed class FieldWriter : FieldVisitor
{
    /// <summary>Where the constants used in this FieldWriter must be stored.</summary>
    private readonly SymbolTable symbolTable;

    // Note: fields are ordered as in the field_info structure, and those related to attributes are
    // ordered as in Section 4.7 of the JVMS.

    /// <summary>
    /// The access_flags field of the field_info JVMS structure. This field can contain ASM specific
    /// access flags, such as <see cref="Opcodes.ACC_DEPRECATED"/>, which are removed when generating the
    /// ClassFile structure.
    /// </summary>
    private readonly int accessFlags;

    /// <summary>The name_index field of the field_info JVMS structure.</summary>
    private readonly int nameIndex;

    /// <summary>The descriptor_index field of the field_info JVMS structure.</summary>
    private readonly int descriptorIndex;

    /// <summary>
    /// The signature_index field of the Signature attribute of this field_info, or 0 if there is no
    /// Signature attribute.
    /// </summary>
    private int signatureIndex;

    /// <summary>
    /// The constantvalue_index field of the ConstantValue attribute of this field_info, or 0 if there
    /// is no ConstantValue attribute.
    /// </summary>
    private int constantValueIndex;

    /// <summary>
    /// The last runtime visible annotation of this field. The previous ones can be accessed with the
    /// <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeVisibleAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime invisible annotation of this field. The previous ones can be accessed with the
    /// <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeInvisibleAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime visible type annotation of this field. The previous ones can be accessed with
    /// the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeVisibleTypeAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The last runtime invisible type annotation of this field. The previous ones can be accessed
    /// with the <c>AnnotationWriter.previousAnnotation</c> field. May be <c>null</c>.
    /// </summary>
    private object? lastRuntimeInvisibleTypeAnnotation; // TODO: Replace with AnnotationWriter when ported

    /// <summary>
    /// The first non standard attribute of this field. The next ones can be accessed with the
    /// <see cref="Attribute.nextAttribute"/> field. May be <c>null</c>.
    /// <para><b>WARNING</b>: this list stores the attributes in the <i>reverse</i> order of their visit.
    /// firstAttribute is actually the last attribute visited in <see cref="VisitAttribute"/>. The
    /// <see cref="PutFieldInfo"/> method writes the attributes in the order defined by this list, i.e.
    /// in the reverse order specified by the user.</para>
    /// </summary>
    private Attribute? firstAttribute;

    // -----------------------------------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Constructs a new <see cref="FieldWriter"/>.
    /// </summary>
    /// <param name="symbolTable">where the constants used in this FieldWriter must be stored.</param>
    /// <param name="access">the field's access flags (see <see cref="Opcodes"/>).</param>
    /// <param name="name">the field's name.</param>
    /// <param name="descriptor">the field's descriptor (see <see cref="Type"/>).</param>
    /// <param name="signature">the field's signature. May be <c>null</c>.</param>
    /// <param name="constantValue">the field's constant value. May be <c>null</c>.</param>
    internal FieldWriter(
        SymbolTable symbolTable,
        int access,
        string name,
        string descriptor,
        string? signature,
        object? constantValue)
        : base(Opcodes.ASM9)
    {
        this.symbolTable = symbolTable;
        this.accessFlags = access;
        this.nameIndex = symbolTable.AddConstantUtf8(name);
        this.descriptorIndex = symbolTable.AddConstantUtf8(descriptor);
        if (signature != null)
        {
            this.signatureIndex = symbolTable.AddConstantUtf8(signature);
        }
        if (constantValue != null)
        {
            this.constantValueIndex = symbolTable.AddConstant(constantValue).Index;
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Linked list pointer
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// The next FieldWriter in the linked list of fields, accessed via the protected
    /// <see cref="FieldVisitor.Fv"/> field. Used by <see cref="ClassWriter"/> to chain fields.
    /// </summary>
    internal FieldWriter? NextField
    {
        get => (FieldWriter?)Fv;
        set => Fv = value;
    }

    // -----------------------------------------------------------------------------------------------
    // Implementation of the FieldVisitor abstract class
    // -----------------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public override AnnotationVisitor? VisitAnnotation(string? descriptor, bool visible)
    {
        throw new NotImplementedException("AnnotationWriter not ported yet.");
    }

    /// <inheritdoc/>
    public override AnnotationVisitor? VisitTypeAnnotation(
        int typeRef, TypePath? typePath, string? descriptor, bool visible)
    {
        throw new NotImplementedException("AnnotationWriter not ported yet.");
    }

    /// <inheritdoc/>
    public override void VisitAttribute(Attribute? attribute)
    {
        // Store the attributes in the <i>reverse</i> order of their visit by this method.
        attribute.nextAttribute = firstAttribute;
        firstAttribute = attribute;
    }

    /// <inheritdoc/>
    public override void VisitEnd()
    {
        // Nothing to do.
    }

    // -----------------------------------------------------------------------------------------------
    // Utility methods
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the size of the field_info JVMS structure generated by this FieldWriter. Also adds the
    /// names of the attributes of this field in the constant pool.
    /// </summary>
    /// <returns>the size in bytes of the field_info JVMS structure.</returns>
    internal int ComputeFieldInfoSize()
    {
        // The access_flags, name_index, descriptor_index and attributes_count fields use 8 bytes.
        int size = 8;
        // For ease of reference, we use here the same attribute order as in Section 4.7 of the JVMS.
        if (constantValueIndex != 0)
        {
            // ConstantValue attributes always use 8 bytes.
            symbolTable.AddConstantUtf8(Constants.CONSTANT_VALUE);
            size += 8;
        }
        size += Attribute.ComputeAttributesSize(symbolTable, accessFlags, signatureIndex);
        // TODO: Replace with AnnotationWriter when ported
        // size += AnnotationWriter.ComputeAnnotationsSize(
        //     lastRuntimeVisibleAnnotation,
        //     lastRuntimeInvisibleAnnotation,
        //     lastRuntimeVisibleTypeAnnotation,
        //     lastRuntimeInvisibleTypeAnnotation);
        if (firstAttribute != null)
        {
            size += firstAttribute.ComputeAttributesSize(symbolTable);
        }
        return size;
    }

    /// <summary>
    /// Puts the content of the field_info JVMS structure generated by this FieldWriter into the given
    /// <see cref="ByteVector"/>.
    /// </summary>
    /// <param name="output">where the field_info structure must be put.</param>
    internal void PutFieldInfo(ByteVector output)
    {
        bool useSyntheticAttribute = symbolTable.GetMajorVersion() < Opcodes.V1_5;
        // Put the access_flags, name_index and descriptor_index fields.
        int mask = useSyntheticAttribute ? Opcodes.ACC_SYNTHETIC : 0;
        output.PutShort(accessFlags & ~mask).PutShort(nameIndex).PutShort(descriptorIndex);
        // Compute and put the attributes_count field.
        // For ease of reference, we use here the same attribute order as in Section 4.7 of the JVMS.
        int attributesCount = 0;
        if (constantValueIndex != 0)
        {
            ++attributesCount;
        }
        if ((accessFlags & Opcodes.ACC_SYNTHETIC) != 0 && useSyntheticAttribute)
        {
            ++attributesCount;
        }
        if (signatureIndex != 0)
        {
            ++attributesCount;
        }
        if ((accessFlags & Opcodes.ACC_DEPRECATED) != 0)
        {
            ++attributesCount;
        }
        if (lastRuntimeVisibleAnnotation != null)
        {
            ++attributesCount;
        }
        if (lastRuntimeInvisibleAnnotation != null)
        {
            ++attributesCount;
        }
        if (lastRuntimeVisibleTypeAnnotation != null)
        {
            ++attributesCount;
        }
        if (lastRuntimeInvisibleTypeAnnotation != null)
        {
            ++attributesCount;
        }
        if (firstAttribute != null)
        {
            attributesCount += firstAttribute.GetAttributeCount();
        }
        output.PutShort(attributesCount);
        // Put the field_info attributes.
        // For ease of reference, we use here the same attribute order as in Section 4.7 of the JVMS.
        if (constantValueIndex != 0)
        {
            output
                .PutShort(symbolTable.AddConstantUtf8(Constants.CONSTANT_VALUE))
                .PutInt(2)
                .PutShort(constantValueIndex);
        }
        Attribute.PutAttributes(symbolTable, accessFlags, signatureIndex, output);
        // TODO: Replace with AnnotationWriter when ported
        // AnnotationWriter.PutAnnotations(
        //     symbolTable,
        //     lastRuntimeVisibleAnnotation,
        //     lastRuntimeInvisibleAnnotation,
        //     lastRuntimeVisibleTypeAnnotation,
        //     lastRuntimeInvisibleTypeAnnotation,
        //     output);
        if (firstAttribute != null)
        {
            firstAttribute.PutAttributes(symbolTable, output);
        }
    }

    /// <summary>
    /// Collects the attributes of this field into the given set of attribute prototypes.
    /// </summary>
    /// <param name="attributePrototypes">a set of attribute prototypes.</param>
    internal void CollectAttributePrototypes(Attribute.Set attributePrototypes)
    {
        attributePrototypes.AddAttributes(firstAttribute);
    }
}
