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
/// The path to a type argument, wildcard bound, array element type, or static inner type within an
/// enclosing type.
/// </summary>
public sealed class TypePath
{
    /// <summary>A type path step that steps into the element type of an array type. See <see cref="GetStep"/>.</summary>
    public const int ARRAY_ELEMENT = 0;

    /// <summary>A type path step that steps into the nested type of a class type. See <see cref="GetStep"/>.</summary>
    public const int INNER_TYPE = 1;

    /// <summary>A type path step that steps into the bound of a wildcard type. See <see cref="GetStep"/>.</summary>
    public const int WILDCARD_BOUND = 2;

    /// <summary>A type path step that steps into a type argument of a generic type. See <see cref="GetStep"/>.</summary>
    public const int TYPE_ARGUMENT = 3;

    /// <summary>
    /// The byte array where the 'type_path' structure - as defined in the Java Virtual Machine
    /// Specification (JVMS) - corresponding to this TypePath is stored. The first byte of the
    /// structure in this array is given by <see cref="typePathOffset"/>.
    /// </summary>
    /// <seealso href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.7.20.2">JVMS 4.7.20.2</seealso>
    private readonly byte[] typePathContainer;

    /// <summary>The offset of the first byte of the type_path JVMS structure in <see cref="typePathContainer"/>.</summary>
    private readonly int typePathOffset;

    /// <summary>
    /// Constructs a new TypePath.
    /// </summary>
    /// <param name="typePathContainer">a byte array containing a type_path JVMS structure.</param>
    /// <param name="typePathOffset">the offset of the first byte of the type_path structure in
    /// <paramref name="typePathContainer"/>.</param>
    internal TypePath(byte[] typePathContainer, int typePathOffset)
    {
        this.typePathContainer = typePathContainer;
        this.typePathOffset = typePathOffset;
    }

    /// <summary>
    /// Returns the length of this path, i.e. its number of steps.
    /// </summary>
    /// <returns>the length of this path.</returns>
    public int GetLength()
    {
        // path_length is stored in the first byte of a type_path.
        return typePathContainer[typePathOffset];
    }

    /// <summary>
    /// Returns the value of the given step of this path.
    /// </summary>
    /// <param name="index">an index between 0 and <see cref="GetLength"/>, exclusive.</param>
    /// <returns>one of <see cref="ARRAY_ELEMENT"/>, <see cref="INNER_TYPE"/>, <see cref="WILDCARD_BOUND"/>, or
    /// <see cref="TYPE_ARGUMENT"/>.</returns>
    public int GetStep(int index)
    {
        // Returns the type_path_kind of the path element of the given index.
        return typePathContainer[typePathOffset + 2 * index + 1];
    }

    /// <summary>
    /// Returns the index of the type argument that the given step is stepping into. This method should
    /// only be used for steps whose value is <see cref="TYPE_ARGUMENT"/>.
    /// </summary>
    /// <param name="index">an index between 0 and <see cref="GetLength"/>, exclusive.</param>
    /// <returns>the index of the type argument that the given step is stepping into.</returns>
    public int GetStepArgument(int index)
    {
        // Returns the type_argument_index of the path element of the given index.
        return typePathContainer[typePathOffset + 2 * index + 2];
    }

    /// <summary>
    /// Converts a type path in string form, in the format used by <see cref="ToString"/>, into a TypePath
    /// object.
    /// </summary>
    /// <param name="typePath">a type path in string form, in the format used by <see cref="ToString"/>. May be
    /// <c>null</c> or empty.</param>
    /// <returns>the corresponding TypePath object, or <c>null</c> if the path is empty.</returns>
    public static TypePath? FromString(string? typePath)
    {
        if (typePath == null || typePath.Length == 0)
        {
            return null;
        }
        int typePathLength = typePath.Length;
        ByteVector output = new ByteVector(typePathLength);
        output.PutByte(0);
        int typePathIndex = 0;
        while (typePathIndex < typePathLength)
        {
            char c = typePath[typePathIndex++];
            if (c == '[')
            {
                output.Put11(ARRAY_ELEMENT, 0);
            }
            else if (c == '.')
            {
                output.Put11(INNER_TYPE, 0);
            }
            else if (c == '*')
            {
                output.Put11(WILDCARD_BOUND, 0);
            }
            else if (c >= '0' && c <= '9')
            {
                int typeArg = c - '0';
                while (typePathIndex < typePathLength)
                {
                    c = typePath[typePathIndex++];
                    if (c >= '0' && c <= '9')
                    {
                        typeArg = typeArg * 10 + c - '0';
                    }
                    else if (c == ';')
                    {
                        break;
                    }
                    else
                    {
                        throw new ArgumentException();
                    }
                }
                output.Put11(TYPE_ARGUMENT, typeArg);
            }
            else
            {
                throw new ArgumentException();
            }
        }
        output.Data[0] = (byte)(output.Length / 2);
        return new TypePath(output.Data, 0);
    }

    /// <summary>
    /// Returns a string representation of this type path. <see cref="ARRAY_ELEMENT"/> steps are represented
    /// with '[', <see cref="INNER_TYPE"/> steps with '.', <see cref="WILDCARD_BOUND"/> steps with '*' and
    /// <see cref="TYPE_ARGUMENT"/> steps with their type argument index in decimal form followed by ';'.
    /// </summary>
    /// <returns>a string representation of this type path.</returns>
    public override string ToString()
    {
        int length = GetLength();
        StringBuilder result = new StringBuilder(length * 2);
        for (int i = 0; i < length; ++i)
        {
            switch (GetStep(i))
            {
                case ARRAY_ELEMENT:
                    result.Append('[');
                    break;
                case INNER_TYPE:
                    result.Append('.');
                    break;
                case WILDCARD_BOUND:
                    result.Append('*');
                    break;
                case TYPE_ARGUMENT:
                    result.Append(GetStepArgument(i)).Append(';');
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Puts the type_path JVMS structure corresponding to the given TypePath into the given
    /// ByteVector.
    /// </summary>
    /// <param name="typePath">a TypePath instance, or <c>null</c> for empty paths.</param>
    /// <param name="output">where the type path must be put.</param>
    internal static void Put(TypePath? typePath, ByteVector output)
    {
        if (typePath == null)
        {
            output.PutByte(0);
        }
        else
        {
            int length = typePath.typePathContainer[typePath.typePathOffset] * 2 + 1;
            output.PutByteArray(typePath.typePathContainer, typePath.typePathOffset, length);
        }
    }
}
