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
/// A visitor to visit a Java module. The methods of this class must be called in the following
/// order: ( <c>VisitMainClass</c> | ( <c>VisitPackage</c> | <c>VisitRequire</c> | <c>VisitExport</c> |
/// <c>VisitOpen</c> | <c>VisitUse</c> | <c>VisitProvide</c> )* ) <c>VisitEnd</c>.
/// <para><b>Author:</b> Remi Forax</para>
/// <para><b>Author:</b> Eric Bruneton</para>
/// </summary>
public abstract class ModuleVisitor
{
    /// <summary>
    /// The ASM API version implemented by this visitor. The value of this field must be one of
    /// <see cref="Opcodes.ASM6"/> or <see cref="Opcodes.ASM7"/>.</summary>
    protected readonly int Api;

    /// <summary>
    /// The module visitor to which this visitor must delegate method calls. May be <c>null</c>.
    /// </summary>
    protected ModuleVisitor? Mv;

    /// <summary>
    /// Constructs a new <see cref="ModuleVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of
    /// <see cref="Opcodes.ASM6"/> or <see cref="Opcodes.ASM7"/>.</param>
    protected ModuleVisitor(int api)
        : this(api, null)
    {
    }

    /// <summary>
    /// Constructs a new <see cref="ModuleVisitor"/>.
    /// </summary>
    /// <param name="api">the ASM API version implemented by this visitor. Must be one of
    /// <see cref="Opcodes.ASM6"/> or <see cref="Opcodes.ASM7"/>.</param>
    /// <param name="moduleVisitor">the module visitor to which this visitor must delegate method
    /// calls. May be <c>null</c>.</param>
    protected ModuleVisitor(int api, ModuleVisitor? moduleVisitor)
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
        this.Mv = moduleVisitor;
    }

    /// <summary>
    /// The module visitor to which this visitor must delegate method calls. May be <c>null</c>.
    /// </summary>
    /// <returns>the module visitor to which this visitor must delegate method calls, or
    /// <c>null</c>.</returns>
    public ModuleVisitor? GetDelegate()
    {
        return Mv;
    }

    /// <summary>
    /// Visits the main class of the current module.
    /// </summary>
    /// <param name="mainClass">the internal name of the main class of the current module (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    public virtual void VisitMainClass(string? mainClass)
    {
        if (Mv != null)
        {
            Mv.VisitMainClass(mainClass);
        }
    }

    /// <summary>
    /// Visits a package of the current module.
    /// </summary>
    /// <param name="packaze">the internal name of a package (see <see cref="Type.GetInternalName()"/>).</param>
    public virtual void VisitPackage(string? packaze)
    {
        if (Mv != null)
        {
            Mv.VisitPackage(packaze);
        }
    }

    /// <summary>
    /// Visits a dependence of the current module.
    /// </summary>
    /// <param name="module">the fully qualified name (using dots) of the dependence.</param>
    /// <param name="access">the access flag of the dependence among <c>ACC_TRANSITIVE</c>,
    /// <c>ACC_STATIC_PHASE</c>, <c>ACC_SYNTHETIC</c> and <c>ACC_MANDATED</c>.</param>
    /// <param name="version">the module version at compile time, or <c>null</c>.</param>
    public virtual void VisitRequire(string? module, int access, string? version)
    {
        if (Mv != null)
        {
            Mv.VisitRequire(module, access, version);
        }
    }

    /// <summary>
    /// Visits an exported package of the current module.
    /// </summary>
    /// <param name="packaze">the internal name of the exported package (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="access">the access flag of the exported package, valid values are among
    /// <c>ACC_SYNTHETIC</c> and <c>ACC_MANDATED</c>.</param>
    /// <param name="modules">the fully qualified names (using dots) of the modules that can access the
    /// public classes of the exported package, or <c>null</c>.</param>
    public virtual void VisitExport(string? packaze, int access, params string[]? modules)
    {
        if (Mv != null)
        {
            Mv.VisitExport(packaze, access, modules);
        }
    }

    /// <summary>
    /// Visits an open package of the current module.
    /// </summary>
    /// <param name="packaze">the internal name of the opened package (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="access">the access flag of the opened package, valid values are among
    /// <c>ACC_SYNTHETIC</c> and <c>ACC_MANDATED</c>.</param>
    /// <param name="modules">the fully qualified names (using dots) of the modules that can use deep
    /// reflection to the classes of the open package, or <c>null</c>.</param>
    public virtual void VisitOpen(string? packaze, int access, params string[]? modules)
    {
        if (Mv != null)
        {
            Mv.VisitOpen(packaze, access, modules);
        }
    }

    /// <summary>
    /// Visits a service used by the current module. The name must be the internal name of an interface
    /// or a class.
    /// </summary>
    /// <param name="service">the internal name of the service (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    public virtual void VisitUse(string? service)
    {
        if (Mv != null)
        {
            Mv.VisitUse(service);
        }
    }

    /// <summary>
    /// Visits an implementation of a service.
    /// </summary>
    /// <param name="service">the internal name of the service (see
    /// <see cref="Type.GetInternalName()"/>).</param>
    /// <param name="providers">the internal names (see <see cref="Type.GetInternalName()"/>) of the
    /// implementations of the service (there is at least one provider).</param>
    public virtual void VisitProvide(string? service, params string[]? providers)
    {
        if (Mv != null)
        {
            Mv.VisitProvide(service, providers);
        }
    }

    /// <summary>
    /// Visits the end of the module. This method, which is the last one to be called, is used to
    /// inform the visitor that everything have been visited.
    /// </summary>
    public virtual void VisitEnd()
    {
        if (Mv != null)
        {
            Mv.VisitEnd();
        }
    }
}
