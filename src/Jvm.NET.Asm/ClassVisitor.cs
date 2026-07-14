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
/// A visitor to visit a Java class. The methods of this class must be called in the following order:
/// <c>Visit</c> [ <c>VisitSource</c> ] [ <c>VisitModule</c> ][ <c>VisitNestHost</c> ][
/// <c>VisitOuterClass</c> ] ( <c>VisitAnnotation</c> | <c>VisitTypeAnnotation</c> |
/// <c>VisitAttribute</c> )* ( <c>VisitNestMember</c> | [ <c>VisitPermittedSubclass</c> ] |
/// <c>VisitInnerClass</c> | <c>VisitRecordComponent</c> | <c>VisitField</c> | <c>VisitMethod</c> )*
/// <c>VisitEnd</c>.
/// <para><b>Author:</b> Eric Bruneton</para>
/// </summary>
public abstract class ClassVisitor
{
    /// <summary>
    /// The ASM API version implemented by this visitor. The value of this field must be one of the
    /// <c>ASM</c><i>x</i> values in <see cref="Opcodes"/>.
    /// </summary>
    protected readonly int Api;

    /// <summary>The class visitor to which this visitor must delegate method calls. May be <c>null</c>.</summary>
    protected ClassVisitor? Cv;

    /// <summary>
    /// Constructs a new <see cref="ClassVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of the
    /// <c>ASM</c><i>x</i> values in <see cref="Opcodes"/>.</param>
    protected ClassVisitor(int api)
        : this(api, null)
    {
    }

    /// <summary>
    /// Constructs a new <see cref="ClassVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of the
    /// <c>ASM</c><i>x</i> values in <see cref="Opcodes"/>.</param>
    /// <param name="classVisitor">the class visitor to which this visitor must delegate method calls.
    /// May be <c>null</c>.</param>
    protected ClassVisitor(int api, ClassVisitor? classVisitor)
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
        this.Cv = classVisitor;
    }

    /// <summary>
    /// The class visitor to which this visitor must delegate method calls. May be <c>null</c>.
    /// </summary>
    /// <returns>the class visitor to which this visitor must delegate method calls, or <c>null</c>.</returns>
    public ClassVisitor? GetDelegate()
    {
        return Cv;
    }

    /// <summary>
    /// Visits the header of the class.
    /// </summary>
    /// <param name="version">the class version. The minor version is stored in the 16 most significant
    /// bits, and the major version in the 16 least significant bits.</param>
    /// <param name="access">the class's access flags (see <see cref="Opcodes"/>). This parameter also
    /// indicates if the class is deprecated <see cref="Opcodes.ACC_DEPRECATED"/> or a record
    /// <see cref="Opcodes.ACC_RECORD"/>.</param>
    /// <param name="name">the internal name of the class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="signature">the signature of this class. May be <c>null</c> if the class is not a
    /// generic one, and does not extend or implement generic classes or interfaces.</param>
    /// <param name="superName">the internal of name of the super class (see
    /// <see cref="Type.GetInternalName()"/>). For interfaces, the super class is
    /// <see cref="object"/>. May be <c>null</c>, but only for the <see cref="object"/> class.</param>
    /// <param name="interfaces">the internal names of the class's interfaces (see
    /// <see cref="Type.GetInternalName()"/>). May be <c>null</c>.</param>
    public virtual void Visit(
        int version,
        int access,
        string? name,
        string? signature,
        string? superName,
        string[]? interfaces)
    {
        if (Api < Opcodes.ASM8 && (access & Opcodes.ACC_RECORD) != 0)
        {
            throw new NotSupportedException("Records requires ASM8");
        }
        if (Cv != null)
        {
            Cv.Visit(version, access, name, signature, superName, interfaces);
        }
    }

    /// <summary>
    /// Visits the source of the class.
    /// </summary>
    /// <param name="source">the name of the source file from which the class was compiled. May be
    /// <c>null</c>.</param>
    /// <param name="debug">additional debug information to compute the correspondence between source
    /// and compiled elements of the class. May be <c>null</c>.</param>
    public virtual void VisitSource(string? source, string? debug)
    {
        if (Cv != null)
        {
            Cv.VisitSource(source, debug);
        }
    }

    /// <summary>
    /// Visit the module corresponding to the class.
    /// </summary>
    /// <param name="name">the fully qualified name (using dots) of the module.</param>
    /// <param name="access">the module access flags, among <c>ACC_OPEN</c>, <c>ACC_SYNTHETIC</c> and
    /// <c>ACC_MANDATED</c>.</param>
    /// <param name="version">the module version, or <c>null</c>.</param>
    /// <returns>a visitor to visit the module values, or <c>null</c> if this visitor is not interested
    /// in visiting this module.</returns>
    public virtual ModuleVisitor? VisitModule(string? name, int access, string? version)
    {
        if (Api < Opcodes.ASM6)
        {
            throw new NotSupportedException("Module requires ASM6");
        }
        if (Cv != null)
        {
            return Cv.VisitModule(name, access, version);
        }
        return null;
    }

    /// <summary>
    /// Visits the nest host class of the class. A nest is a set of classes of the same package that
    /// share access to their private members. One of these classes, called the host, lists the other
    /// members of the nest, which in turn should link to the host of their nest. This method must be
    /// called only once and only if the visited class is a non-host member of a nest. A class is
    /// implicitly its own nest, so it's invalid to call this method with the visited class name as
    /// argument.
    /// </summary>
    /// <param name="nestHost">the internal name of the host class of the nest (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    public virtual void VisitNestHost(string? nestHost)
    {
        if (Api < Opcodes.ASM7)
        {
            throw new NotSupportedException("NestHost requires ASM7");
        }
        if (Cv != null)
        {
            Cv.VisitNestHost(nestHost);
        }
    }

    /// <summary>
    /// Visits the enclosing class of the class. This method must be called only if this class is a
    /// local or anonymous class. See the JVMS 4.7.7 section for more details.
    /// </summary>
    /// <param name="owner">internal name of the enclosing class of the class (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="name">the name of the method that contains the class, or <c>null</c> if the class
    /// is not enclosed in a method or constructor of its enclosing class (e.g. if it is enclosed in
    /// an instance initializer, static initializer, instance variable initializer, or class variable
    /// initializer).</param>
    /// <param name="descriptor">the descriptor of the method that contains the class, or <c>null</c>
    /// if the class is not enclosed in a method or constructor of its enclosing class (e.g. if it is
    /// enclosed in an instance initializer, static initializer, instance variable initializer, or
    /// class variable initializer).</param>
    public virtual void VisitOuterClass(string? owner, string? name, string? descriptor)
    {
        if (Cv != null)
        {
            Cv.VisitOuterClass(owner, name, descriptor);
        }
    }

    /// <summary>
    /// Visits an annotation of the class.
    /// </summary>
    /// <param name="descriptor">the class descriptor of the annotation class.</param>
    /// <param name="visible"><c>true</c> if the annotation is visible at runtime.</param>
    /// <returns>a visitor to visit the annotation values, or <c>null</c> if this visitor is not
    /// interested in visiting this annotation.</returns>
    public virtual AnnotationVisitor? VisitAnnotation(string? descriptor, bool visible)
    {
        if (Cv != null)
        {
            return Cv.VisitAnnotation(descriptor, visible);
        }
        return null;
    }

    /// <summary>
    /// Visits an annotation on a type in the class signature.
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
        if (Api < Opcodes.ASM5)
        {
            throw new NotSupportedException("TypeAnnotation requires ASM5");
        }
        if (Cv != null)
        {
            return Cv.VisitTypeAnnotation(typeRef, typePath, descriptor, visible);
        }
        return null;
    }

    /// <summary>
    /// Visits a non standard attribute of the class.
    /// </summary>
    /// <param name="attribute">an attribute.</param>
    public virtual void VisitAttribute(Attribute? attribute) // TODO: Attribute
    {
        if (Cv != null)
        {
            Cv.VisitAttribute(attribute);
        }
    }

    /// <summary>
    /// Visits a member of the nest. A nest is a set of classes of the same package that share access
    /// to their private members. One of these classes, called the host, lists the other members of
    /// the nest, which in turn should link to the host of their nest. This method must be called only
    /// if the visited class is the host of a nest. A nest host is implicitly a member of its own nest,
    /// so it's invalid to call this method with the visited class name as argument.
    /// </summary>
    /// <param name="nestMember">the internal name of a nest member (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    public virtual void VisitNestMember(string? nestMember)
    {
        if (Api < Opcodes.ASM7)
        {
            throw new NotSupportedException("NestMember requires ASM7");
        }
        if (Cv != null)
        {
            Cv.VisitNestMember(nestMember);
        }
    }

    /// <summary>
    /// Visits a permitted subclasses. A permitted subclass is one of the allowed subclasses of the
    /// current class.
    /// </summary>
    /// <param name="permittedSubclass">the internal name of a permitted subclass (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    public virtual void VisitPermittedSubclass(string? permittedSubclass)
    {
        if (Api < Opcodes.ASM9)
        {
            throw new NotSupportedException("PermittedSubclasses requires ASM9");
        }
        if (Cv != null)
        {
            Cv.VisitPermittedSubclass(permittedSubclass);
        }
    }

    /// <summary>
    /// Visits information about an inner class. This inner class is not necessarily a member of the
    /// class being visited. More precisely, every class or interface C which is referenced by this
    /// class and which is not a package member must be visited with this method. This class must
    /// reference its nested class or interface members, and its enclosing class, if any. See the JVMS
    /// 4.7.6 section for more details.
    /// </summary>
    /// <param name="name">the internal name of C (see <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="outerName">the internal name of the class or interface C is a member of (see
    /// <see cref="Type.GetInternalName()"/>). Must be <c>null</c> if C is not the member of a class or
    /// interface (e.g. for local or anonymous classes).</param>
    /// <param name="innerName">the (simple) name of C. Must be <c>null</c> for anonymous inner
    /// classes.</param>
    /// <param name="access">the access flags of C originally declared in the source code from which
    /// this class was compiled.</param>
    public virtual void VisitInnerClass(string? name, string? outerName, string? innerName, int access)
    {
        if (Cv != null)
        {
            Cv.VisitInnerClass(name, outerName, innerName, access);
        }
    }

    /// <summary>
    /// Visits a record component of the class.
    /// </summary>
    /// <param name="name">the record component name.</param>
    /// <param name="descriptor">the record component descriptor (see <see cref="Type"/>).</param>
    /// <param name="signature">the record component signature. May be <c>null</c> if the record
    /// component type does not use generic types.</param>
    /// <returns>a visitor to visit this record component annotations and attributes, or <c>null</c>
    /// if this class visitor is not interested in visiting these annotations and attributes.</returns>
    public virtual RecordComponentVisitor? VisitRecordComponent(
        string? name, string? descriptor, string? signature)
    {
        if (Api < Opcodes.ASM8)
        {
            throw new NotSupportedException("Record requires ASM8");
        }
        if (Cv != null)
        {
            return Cv.VisitRecordComponent(name, descriptor, signature);
        }
        return null;
    }

    /// <summary>
    /// Visits a field of the class.
    /// </summary>
    /// <param name="access">the field's access flags (see <see cref="Opcodes"/>). This parameter also
    /// indicates if the field is synthetic and/or deprecated.</param>
    /// <param name="name">the field's name.</param>
    /// <param name="descriptor">the field's descriptor (see <see cref="Type"/>).</param>
    /// <param name="signature">the field's signature. May be <c>null</c> if the field's type does not
    /// use generic types.</param>
    /// <param name="value">the field's initial value. This parameter, which may be <c>null</c> if the
    /// field does not have an initial value, must be an <see cref="int"/>, a <see cref="float"/>, a
    /// <see cref="long"/>, a <see cref="double"/> or a <see cref="string"/> (for <c>int</c>,
    /// <c>float</c>, <c>long</c> or <c>string</c> fields respectively). <i>This parameter is only used
    /// for static fields</i>. Its value is ignored for non static fields, which must be initialized
    /// through bytecode instructions in constructors or methods.</param>
    /// <returns>a visitor to visit field annotations and attributes, or <c>null</c> if this class
    /// visitor is not interested in visiting these annotations and attributes.</returns>
    public virtual FieldVisitor? VisitField(
        int access, string? name, string? descriptor, string? signature, object? value)
    {
        if (Cv != null)
        {
            return Cv.VisitField(access, name, descriptor, signature, value);
        }
        return null;
    }

    /// <summary>
    /// Visits a method of the class. This method <i>must</i> return a new <see cref="MethodVisitor"/>
    /// instance (or <c>null</c>) each time it is called, i.e., it should not return a previously
    /// returned visitor.
    /// </summary>
    /// <param name="access">the method's access flags (see <see cref="Opcodes"/>). This parameter also
    /// indicates if the method is synthetic and/or deprecated.</param>
    /// <param name="name">the method's name.</param>
    /// <param name="descriptor">the method's descriptor (see <see cref="Type"/>).</param>
    /// <param name="signature">the method's signature. May be <c>null</c> if the method parameters,
    /// return type and exceptions do not use generic types.</param>
    /// <param name="exceptions">the internal names of the method's exception classes (see
    /// <see cref="Type.GetInternalName()"/>). May be <c>null</c>.</param>
    /// <returns>an object to visit the byte code of the method, or <c>null</c> if this class visitor
    /// is not interested in visiting the code of this method.</returns>
    public virtual MethodVisitor? VisitMethod(
        int access, string? name, string? descriptor, string? signature, string[]? exceptions)
    {
        if (Cv != null)
        {
            return Cv.VisitMethod(access, name, descriptor, signature, exceptions);
        }
        return null;
    }

    /// <summary>
    /// Visits the end of the class. This method, which is the last one to be called, is used to inform
    /// the visitor that all the fields and methods of the class have been visited.
    /// </summary>
    public virtual void VisitEnd()
    {
        if (Cv != null)
        {
            Cv.VisitEnd();
        }
    }
}
