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

namespace Jvm.NET.Asm;

/// <summary>
/// A visitor to visit a Java annotation. The methods of this class must be called in the following
/// order: ( <c>Visit</c> | <c>VisitEnum</c> | <c>VisitAnnotation</c> | <c>VisitArray</c> )*
/// <c>VisitEnd</c>.
/// <para><b>Author:</b> Eric Bruneton</para>
/// <para><b>Author:</b> Eugene Kuleshov</para>
/// </summary>
public abstract class AnnotationVisitor
{
    /// <summary>
    /// The ASM API version implemented by this visitor. The value of this field must be one of the
    /// <c>ASM</c><i>x</i> values in <see cref="Opcodes"/>.
    /// </summary>
    protected readonly int Api;

    /// <summary>
    /// The annotation visitor to which this visitor must delegate method calls. May be <c>null</c>.
    /// </summary>
    protected AnnotationVisitor? Av;

    /// <summary>
    /// Constructs a new <see cref="AnnotationVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of the
    /// <c>ASM</c><i>x</i> values in <see cref="Opcodes"/>.</param>
    protected AnnotationVisitor(int api)
        : this(api, null)
    {
    }

    /// <summary>
    /// Constructs a new <see cref="AnnotationVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of the
    /// <c>ASM</c><i>x</i> values in <see cref="Opcodes"/>.</param>
    /// <param name="annotationVisitor">the annotation visitor to which this visitor must delegate
    /// method calls. May be <c>null</c>.</param>
    protected AnnotationVisitor(int api, AnnotationVisitor? annotationVisitor)
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
        this.Av = annotationVisitor;
    }

    /// <summary>
    /// The annotation visitor to which this visitor must delegate method calls. May be <c>null</c>.
    /// </summary>
    /// <returns>the annotation visitor to which this visitor must delegate method calls, or
    /// <c>null</c>.</returns>
    public AnnotationVisitor? GetDelegate()
    {
        return Av;
    }

    /// <summary>
    /// Visits a primitive value of the annotation.
    /// </summary>
    /// <param name="name">the value name.</param>
    /// <param name="value">the actual value, whose type must be <see cref="byte"/>, <see cref="bool"/>,
    /// <see cref="char"/>, <see cref="short"/>, <see cref="int"/>, <see cref="long"/>,
    /// <see cref="float"/>, <see cref="double"/>, <see cref="string"/> or <see cref="Type"/> of
    /// <see cref="Type.OBJECT"/> or <see cref="Type.ARRAY"/> sort. This value can also be an array of
    /// byte, boolean, short, char, int, long, float or double values (this is equivalent to using
    /// <see cref="VisitArray"/> and visiting each array element in turn, but is more convenient).</param>
    public virtual void Visit(string? name, object? value)
    {
        if (Av != null)
        {
            Av.Visit(name, value);
        }
    }

    /// <summary>
    /// Visits an enumeration value of the annotation.
    /// </summary>
    /// <param name="name">the value name.</param>
    /// <param name="descriptor">the class descriptor of the enumeration class.</param>
    /// <param name="value">the actual enumeration value.</param>
    public virtual void VisitEnum(string? name, string? descriptor, string? value)
    {
        if (Av != null)
        {
            Av.VisitEnum(name, descriptor, value);
        }
    }

    /// <summary>
    /// Visits a nested annotation value of the annotation.
    /// </summary>
    /// <param name="name">the value name.</param>
    /// <param name="descriptor">the class descriptor of the nested annotation class.</param>
    /// <returns>a visitor to visit the actual nested annotation value, or <c>null</c> if this visitor
    /// is not interested in visiting this nested annotation. <i>The nested annotation value must be
    /// fully visited before calling other methods on this annotation visitor</i>.</returns>
    public virtual AnnotationVisitor? VisitAnnotation(string? name, string? descriptor)
    {
        if (Av != null)
        {
            return Av.VisitAnnotation(name, descriptor);
        }
        return null;
    }

    /// <summary>
    /// Visits an array value of the annotation. Note that arrays of primitive values (such as byte,
    /// boolean, short, char, int, long, float or double) can be passed as value to <see cref="Visit"/>.
    /// This is what <c>ClassReader</c> does for non empty arrays of primitive values.
    /// </summary>
    /// <param name="name">the value name.</param>
    /// <returns>a visitor to visit the actual array value elements, or <c>null</c> if this visitor is
    /// not interested in visiting these values. The 'name' parameters passed to the methods of this
    /// visitor are ignored. <i>All the array values must be visited before calling other methods on
    /// this annotation visitor</i>.</returns>
    public virtual AnnotationVisitor? VisitArray(string? name)
    {
        if (Av != null)
        {
            return Av.VisitArray(name);
        }
        return null;
    }

    /// <summary>Visits the end of the annotation.</summary>
    public virtual void VisitEnd()
    {
        if (Av != null)
        {
            Av.VisitEnd();
        }
    }
}
