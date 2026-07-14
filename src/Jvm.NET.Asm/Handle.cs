// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

namespace Jvm.NET.Asm;

/// <summary>
/// A reference to a field or a method.
/// </summary>
public sealed class Handle
{
    /// <summary>
    /// The kind of field or method designated by this Handle. Should be <see cref="Opcodes.H_GETFIELD"/>,
    /// <see cref="Opcodes.H_GETSTATIC"/>, <see cref="Opcodes.H_PUTFIELD"/>, <see cref="Opcodes.H_PUTSTATIC"/>,
    /// <see cref="Opcodes.H_INVOKEVIRTUAL"/>, <see cref="Opcodes.H_INVOKESTATIC"/>, <see cref="Opcodes.H_INVOKESPECIAL"/>,
    /// <see cref="Opcodes.H_NEWINVOKESPECIAL"/> or <see cref="Opcodes.H_INVOKEINTERFACE"/>.
    /// </summary>
    private readonly int tag;

    /// <summary>The internal name of the class that owns the field or method designated by this handle.</summary>
    private readonly string owner;

    /// <summary>The name of the field or method designated by this handle.</summary>
    private readonly string name;

    /// <summary>The descriptor of the field or method designated by this handle.</summary>
    private readonly string descriptor;

    /// <summary>Whether the owner is an interface or not.</summary>
    private readonly bool isInterface;

    /// <summary>
    /// Constructs a new field or method handle.
    /// </summary>
    /// <param name="tag">the kind of field or method designated by this Handle. Must be
    /// <see cref="Opcodes.H_GETFIELD"/>, <see cref="Opcodes.H_GETSTATIC"/>, <see cref="Opcodes.H_PUTFIELD"/>,
    /// <see cref="Opcodes.H_PUTSTATIC"/>, <see cref="Opcodes.H_INVOKEVIRTUAL"/>, <see cref="Opcodes.H_INVOKESTATIC"/>,
    /// <see cref="Opcodes.H_INVOKESPECIAL"/>, <see cref="Opcodes.H_NEWINVOKESPECIAL"/> or
    /// <see cref="Opcodes.H_INVOKEINTERFACE"/>.</param>
    /// <param name="owner">the internal name of the class that owns the field or method designated by this
    /// handle.</param>
    /// <param name="name">the name of the field or method designated by this handle.</param>
    /// <param name="descriptor">the descriptor of the field or method designated by this handle.</param>
    [Obsolete("This constructor has been superseded by Handle(int, string, string, string, boolean).")]
    public Handle(int tag, string owner, string name, string descriptor)
        : this(tag, owner, name, descriptor, tag == Opcodes.H_INVOKEINTERFACE)
    {
    }

    /// <summary>
    /// Constructs a new field or method handle.
    /// </summary>
    /// <param name="tag">the kind of field or method designated by this Handle. Must be
    /// <see cref="Opcodes.H_GETFIELD"/>, <see cref="Opcodes.H_GETSTATIC"/>, <see cref="Opcodes.H_PUTFIELD"/>,
    /// <see cref="Opcodes.H_PUTSTATIC"/>, <see cref="Opcodes.H_INVOKEVIRTUAL"/>, <see cref="Opcodes.H_INVOKESTATIC"/>,
    /// <see cref="Opcodes.H_INVOKESPECIAL"/>, <see cref="Opcodes.H_NEWINVOKESPECIAL"/> or
    /// <see cref="Opcodes.H_INVOKEINTERFACE"/>.</param>
    /// <param name="owner">the internal name of the class that owns the field or method designated by this
    /// handle.</param>
    /// <param name="name">the name of the field or method designated by this handle.</param>
    /// <param name="descriptor">the descriptor of the field or method designated by this handle.</param>
    /// <param name="isInterface">whether the owner is an interface or not.</param>
    public Handle(int tag, string owner, string name, string descriptor, bool isInterface)
    {
        this.tag = tag;
        this.owner = owner;
        this.name = name;
        this.descriptor = descriptor;
        this.isInterface = isInterface;
    }

    /// <summary>
    /// Returns the kind of field or method designated by this handle.
    /// </summary>
    /// <returns><see cref="Opcodes.H_GETFIELD"/>, <see cref="Opcodes.H_GETSTATIC"/>, <see cref="Opcodes.H_PUTFIELD"/>,
    /// <see cref="Opcodes.H_PUTSTATIC"/>, <see cref="Opcodes.H_INVOKEVIRTUAL"/>, <see cref="Opcodes.H_INVOKESTATIC"/>,
    /// <see cref="Opcodes.H_INVOKESPECIAL"/>, <see cref="Opcodes.H_NEWINVOKESPECIAL"/> or
    /// <see cref="Opcodes.H_INVOKEINTERFACE"/>.</returns>
    public int GetTag()
    {
        return tag;
    }

    /// <summary>
    /// Returns the internal name of the class that owns the field or method designated by this handle.
    /// </summary>
    /// <returns>the internal name of the class that owns the field or method designated by this handle.</returns>
    public string GetOwner()
    {
        return owner;
    }

    /// <summary>
    /// Returns the name of the field or method designated by this handle.
    /// </summary>
    /// <returns>the name of the field or method designated by this handle.</returns>
    public string GetName()
    {
        return name;
    }

    /// <summary>
    /// Returns the descriptor of the field or method designated by this handle.
    /// </summary>
    /// <returns>the descriptor of the field or method designated by this handle.</returns>
    public string GetDesc()
    {
        return descriptor;
    }

    /// <summary>
    /// Returns true if the owner of the field or method designated by this handle is an interface.
    /// </summary>
    /// <returns>true if the owner of the field or method designated by this handle is an interface.</returns>
    public bool IsInterface()
    {
        return isInterface;
    }

    /// <summary>
    /// Tests if the given object is equal to this handle.
    /// </summary>
    /// <param name="obj">the object to be compared to this handle.</param>
    /// <returns><c>true</c> if the given object is equal to this handle.</returns>
    public override bool Equals(object? obj)
    {
        if (obj == this)
        {
            return true;
        }
        if (!(obj is Handle))
        {
            return false;
        }
        Handle handle = (Handle)obj;
        return tag == handle.tag
            && isInterface == handle.isInterface
            && owner.Equals(handle.owner)
            && name.Equals(handle.name)
            && descriptor.Equals(handle.descriptor);
    }

    /// <summary>
    /// Returns a hash code value for this handle.
    /// </summary>
    /// <returns>a hash code value for this handle.</returns>
    public override int GetHashCode()
    {
        return tag
            + (isInterface ? 64 : 0)
            + owner.GetHashCode() * name.GetHashCode() * descriptor.GetHashCode();
    }

    /// <summary>
    /// Returns the textual representation of this handle. The textual representation is:
    /// <list type="bullet">
    /// <item>for a reference to a class: owner "." name descriptor " (" tag ")",</item>
    /// <item>for a reference to an interface: owner "." name descriptor " (" tag " itf)".</item>
    /// </list>
    /// </summary>
    /// <returns>the textual representation of this handle.</returns>
    public override string ToString()
    {
        return owner + '.' + name + descriptor + " (" + tag + (isInterface ? " itf" : "") + ')';
    }
}
