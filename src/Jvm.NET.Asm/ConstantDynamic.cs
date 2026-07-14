// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

using System.Text;

namespace Jvm.NET.Asm;

/// <summary>
/// A constant whose value is computed at runtime, with a bootstrap method.
/// </summary>
public sealed class ConstantDynamic
{
    /// <summary>The constant name (can be arbitrary).</summary>
    private readonly string name;

    /// <summary>The constant type (must be a field descriptor).</summary>
    private readonly string descriptor;

    /// <summary>The bootstrap method to use to compute the constant value at runtime.</summary>
    private readonly Handle bootstrapMethod;

    /// <summary>
    /// The arguments to pass to the bootstrap method, in order to compute the constant value at
    /// runtime.
    /// </summary>
    private readonly object[] bootstrapMethodArguments;

    /// <summary>
    /// Constructs a new <see cref="ConstantDynamic"/>.
    /// </summary>
    /// <param name="name">the constant name (can be arbitrary).</param>
    /// <param name="descriptor">the constant type (must be a field descriptor).</param>
    /// <param name="bootstrapMethod">the bootstrap method to use to compute the constant value at runtime.</param>
    /// <param name="bootstrapMethodArguments">the arguments to pass to the bootstrap method, in order to
    /// compute the constant value at runtime.</param>
    public ConstantDynamic(
        string name,
        string descriptor,
        Handle bootstrapMethod,
        params object[] bootstrapMethodArguments)
    {
        this.name = name;
        this.descriptor = descriptor;
        this.bootstrapMethod = bootstrapMethod;
        this.bootstrapMethodArguments = bootstrapMethodArguments;
    }

    /// <summary>
    /// Returns the name of this constant.
    /// </summary>
    /// <returns>the name of this constant.</returns>
    public string GetName()
    {
        return name;
    }

    /// <summary>
    /// Returns the type of this constant.
    /// </summary>
    /// <returns>the type of this constant, as a field descriptor.</returns>
    public string GetDescriptor()
    {
        return descriptor;
    }

    /// <summary>
    /// Returns the bootstrap method used to compute the value of this constant.
    /// </summary>
    /// <returns>the bootstrap method used to compute the value of this constant.</returns>
    public Handle GetBootstrapMethod()
    {
        return bootstrapMethod;
    }

    /// <summary>
    /// Returns the number of arguments passed to the bootstrap method, in order to compute the value
    /// of this constant.
    /// </summary>
    /// <returns>the number of arguments passed to the bootstrap method, in order to compute the value
    /// of this constant.</returns>
    public int GetBootstrapMethodArgumentCount()
    {
        return bootstrapMethodArguments.Length;
    }

    /// <summary>
    /// Returns an argument passed to the bootstrap method, in order to compute the value of this
    /// constant.
    /// </summary>
    /// <param name="index">an argument index, between 0 and <see cref="GetBootstrapMethodArgumentCount"/>
    /// (exclusive).</param>
    /// <returns>the argument passed to the bootstrap method, with the given index.</returns>
    public object GetBootstrapMethodArgument(int index)
    {
        return bootstrapMethodArguments[index];
    }

    /// <summary>
    /// Returns the arguments to pass to the bootstrap method, in order to compute the value of this
    /// constant. WARNING: this array must not be modified, and must not be returned to the user.
    /// </summary>
    /// <returns>the arguments to pass to the bootstrap method, in order to compute the value of this
    /// constant.</returns>
    internal object[] GetBootstrapMethodArgumentsUnsafe()
    {
        return bootstrapMethodArguments;
    }

    /// <summary>
    /// Returns the size of this constant.
    /// </summary>
    /// <returns>the size of this constant, i.e., 2 for <c>long</c> and <c>double</c>, 1 otherwise.</returns>
    public int GetSize()
    {
        char firstCharOfDescriptor = descriptor[0];
        return (firstCharOfDescriptor == 'J' || firstCharOfDescriptor == 'D') ? 2 : 1;
    }

    /// <summary>
    /// Tests if the given object is equal to this constant dynamic.
    /// </summary>
    /// <param name="obj">the object to be compared to this constant dynamic.</param>
    /// <returns><c>true</c> if the given object is equal to this constant dynamic.</returns>
    public override bool Equals(object? obj)
    {
        if (obj == this)
        {
            return true;
        }
        if (!(obj is ConstantDynamic))
        {
            return false;
        }
        ConstantDynamic constantDynamic = (ConstantDynamic)obj;
        return name.Equals(constantDynamic.name)
            && descriptor.Equals(constantDynamic.descriptor)
            && bootstrapMethod.Equals(constantDynamic.bootstrapMethod)
            && ArrayEquals(bootstrapMethodArguments, constantDynamic.bootstrapMethodArguments);
    }

    /// <summary>
    /// Returns a hash code value for this constant dynamic.
    /// </summary>
    /// <returns>a hash code value for this constant dynamic.</returns>
    public override int GetHashCode()
    {
        return name.GetHashCode()
            ^ RotateLeft(descriptor.GetHashCode(), 8)
            ^ RotateLeft(bootstrapMethod.GetHashCode(), 16)
            ^ RotateLeft(ArrayHashCode(bootstrapMethodArguments), 24);
    }

    /// <summary>
    /// Returns the textual representation of this constant dynamic.
    /// </summary>
    /// <returns>the textual representation of this constant dynamic.</returns>
    public override string ToString()
    {
        return name
            + " : "
            + descriptor
            + ' '
            + bootstrapMethod
            + ' '
            + ArrayToString(bootstrapMethodArguments);
    }

    private static int RotateLeft(int value, int count)
    {
        uint u = (uint)value;
        count &= 31;
        if (count == 0)
        {
            return value;
        }
        return (int)((u << count) | (u >> (32 - count)));
    }

    private static bool ArrayEquals(object[] a, object[] a2)
    {
        if (a == a2)
        {
            return true;
        }
        if (a == null || a2 == null || a.Length != a2.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (!object.Equals(a[i], a2[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static int ArrayHashCode(object[] a)
    {
        if (a == null)
        {
            return 0;
        }
        int result = 1;
        foreach (object element in a)
        {
            result = 31 * result + (element == null ? 0 : element.GetHashCode());
        }
        return result;
    }

    private static string ArrayToString(object[] a)
    {
        if (a == null)
        {
            return "null";
        }
        if (a.Length == 0)
        {
            return "[]";
        }
        StringBuilder sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < a.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            sb.Append(a[i]?.ToString() ?? "null");
        }
        sb.Append(']');
        return sb.ToString();
    }
}
