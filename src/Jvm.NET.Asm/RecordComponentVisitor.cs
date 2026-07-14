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
/// A visitor to visit a record component. The methods of this class must be called in the following
/// order: ( <c>VisitAnnotation</c> | <c>VisitTypeAnnotation</c> | <c>VisitAttribute</c> )*
/// <c>VisitEnd</c>.
/// <para><b>Author:</b> Remi Forax</para>
/// <para><b>Author:</b> Eric Bruneton</para>
/// </summary>
public abstract class RecordComponentVisitor
{
    /// <summary>
    /// The ASM API version implemented by this visitor. The value of this field must be one of
    /// <see cref="Opcodes.ASM8"/> or <see cref="Opcodes.ASM9"/>.</summary>
    protected readonly int Api;

    /// <summary>
    /// The record visitor to which this visitor must delegate method calls. May be <c>null</c>.
    /// </summary>
    protected RecordComponentVisitor? Delegate;

    /// <summary>
    /// Constructs a new <see cref="RecordComponentVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of
    /// <see cref="Opcodes.ASM8"/> or <see cref="Opcodes.ASM9"/>.</param>
    protected RecordComponentVisitor(int api)
        : this(api, null)
    {
    }

    /// <summary>
    /// Constructs a new <see cref="RecordComponentVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of
    /// <see cref="Opcodes.ASM8"/>.</param>
    /// <param name="recordComponentVisitor">the record component visitor to which this visitor must
    /// delegate method calls. May be <c>null</c>.</param>
    protected RecordComponentVisitor(int api, RecordComponentVisitor? recordComponentVisitor)
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
        this.Delegate = recordComponentVisitor;
    }

    /// <summary>
    /// The record visitor to which this visitor must delegate method calls. May be <c>null</c>.
    /// </summary>
    /// <returns>the record visitor to which this visitor must delegate method calls, or
    /// <c>null</c>.</returns>
    public RecordComponentVisitor? GetDelegate()
    {
        return Delegate;
    }

    /// <summary>
    /// Visits an annotation of the record component.
    /// </summary>
    /// <param name="descriptor">the class descriptor of the annotation class.</param>
    /// <param name="visible"><c>true</c> if the annotation is visible at runtime.</param>
    /// <returns>a visitor to visit the annotation values, or <c>null</c> if this visitor is not
    /// interested in visiting this annotation.</returns>
    public virtual AnnotationVisitor? VisitAnnotation(string? descriptor, bool visible)
    {
        if (Delegate != null)
        {
            return Delegate.VisitAnnotation(descriptor, visible);
        }
        return null;
    }

    /// <summary>
    /// Visits an annotation on a type in the record component signature.
    /// </summary>
    /// <param name="typeRef">a reference to the annotated type. The sort of this type reference must
    /// be <see cref="TypeReference.CLASS_TYPE_PARAMETER"/>,
    /// <see cref="TypeReference.CLASS_TYPE_PARAMETER_BOUND"/> or
    /// <see cref="TypeReference.CLASS_EXTENDS"/>. See <see cref="TypeReference"/>.</param>
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
        if (Delegate != null)
        {
            return Delegate.VisitTypeAnnotation(typeRef, typePath, descriptor, visible);
        }
        return null;
    }

    /// <summary>
    /// Visits a non standard attribute of the record component.
    /// </summary>
    /// <param name="attribute">an attribute.</param>
    public virtual void VisitAttribute(Attribute? attribute) // TODO: Attribute
    {
        if (Delegate != null)
        {
            Delegate.VisitAttribute(attribute);
        }
    }

    /// <summary>
    /// Visits the end of the record component. This method, which is the last one to be called, is
    /// used to inform the visitor that everything have been visited.
    /// </summary>
    public virtual void VisitEnd()
    {
        if (Delegate != null)
        {
            Delegate.VisitEnd();
        }
    }
}
