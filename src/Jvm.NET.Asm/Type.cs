// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

using System.Reflection;
using System.Text;

namespace Jvm.NET.Asm;

/// <summary>
/// A Java field or method type. This class can be used to make it easier to manipulate type and
/// method descriptors.
/// </summary>
public sealed class Type
{
    /// <summary>The sort of the <c>void</c> type. See <see cref="GetSort"/>.</summary>
    public const int VOID = 0;

    /// <summary>The sort of the <c>boolean</c> type. See <see cref="GetSort"/>.</summary>
    public const int BOOLEAN = 1;

    /// <summary>The sort of the <c>char</c> type. See <see cref="GetSort"/>.</summary>
    public const int CHAR = 2;

    /// <summary>The sort of the <c>byte</c> type. See <see cref="GetSort"/>.</summary>
    public const int BYTE = 3;

    /// <summary>The sort of the <c>short</c> type. See <see cref="GetSort"/>.</summary>
    public const int SHORT = 4;

    /// <summary>The sort of the <c>int</c> type. See <see cref="GetSort"/>.</summary>
    public const int INT = 5;

    /// <summary>The sort of the <c>float</c> type. See <see cref="GetSort"/>.</summary>
    public const int FLOAT = 6;

    /// <summary>The sort of the <c>long</c> type. See <see cref="GetSort"/>.</summary>
    public const int LONG = 7;

    /// <summary>The sort of the <c>double</c> type. See <see cref="GetSort"/>.</summary>
    public const int DOUBLE = 8;

    /// <summary>The sort of array reference types. See <see cref="GetSort"/>.</summary>
    public const int ARRAY = 9;

    /// <summary>The sort of object reference types. See <see cref="GetSort"/>.</summary>
    public const int OBJECT = 10;

    /// <summary>The sort of method types. See <see cref="GetSort"/>.</summary>
    public const int METHOD = 11;

    /// <summary>The (private) sort of object reference types represented with an internal name.</summary>
    private const int INTERNAL = 12;

    /// <summary>The descriptors of the primitive types.</summary>
    private static readonly string PRIMITIVE_DESCRIPTORS = "VZCBSIFJD";

    /// <summary>The <c>void</c> type.</summary>
    public static readonly Type VOID_TYPE = new Type(VOID, PRIMITIVE_DESCRIPTORS, VOID, VOID + 1);

    /// <summary>The <c>boolean</c> type.</summary>
    public static readonly Type BOOLEAN_TYPE =
        new Type(BOOLEAN, PRIMITIVE_DESCRIPTORS, BOOLEAN, BOOLEAN + 1);

    /// <summary>The <c>char</c> type.</summary>
    public static readonly Type CHAR_TYPE = new Type(CHAR, PRIMITIVE_DESCRIPTORS, CHAR, CHAR + 1);

    /// <summary>The <c>byte</c> type.</summary>
    public static readonly Type BYTE_TYPE = new Type(BYTE, PRIMITIVE_DESCRIPTORS, BYTE, BYTE + 1);

    /// <summary>The <c>short</c> type.</summary>
    public static readonly Type SHORT_TYPE =
        new Type(SHORT, PRIMITIVE_DESCRIPTORS, SHORT, SHORT + 1);

    /// <summary>The <c>int</c> type.</summary>
    public static readonly Type INT_TYPE = new Type(INT, PRIMITIVE_DESCRIPTORS, INT, INT + 1);

    /// <summary>The <c>float</c> type.</summary>
    public static readonly Type FLOAT_TYPE =
        new Type(FLOAT, PRIMITIVE_DESCRIPTORS, FLOAT, FLOAT + 1);

    /// <summary>The <c>long</c> type.</summary>
    public static readonly Type LONG_TYPE = new Type(LONG, PRIMITIVE_DESCRIPTORS, LONG, LONG + 1);

    /// <summary>The <c>double</c> type.</summary>
    public static readonly Type DOUBLE_TYPE =
        new Type(DOUBLE, PRIMITIVE_DESCRIPTORS, DOUBLE, DOUBLE + 1);

    // -----------------------------------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// The sort of this type. Either <see cref="VOID"/>, <see cref="BOOLEAN"/>, <see cref="CHAR"/>,
    /// <see cref="BYTE"/>, <see cref="SHORT"/>, <see cref="INT"/>, <see cref="FLOAT"/>,
    /// <see cref="LONG"/>, <see cref="DOUBLE"/>, <see cref="ARRAY"/>, <see cref="OBJECT"/>,
    /// <see cref="METHOD"/> or <see cref="INTERNAL"/>.
    /// </summary>
    private readonly int sort;

    /// <summary>
    /// A buffer containing the value of this field or method type. This value is an internal name
    /// for <see cref="OBJECT"/> and <see cref="INTERNAL"/> types, and a field or method descriptor
    /// in the other cases.
    /// <para>
    /// For <see cref="OBJECT"/> types, this field also contains the descriptor: the characters in
    /// [<see cref="valueBegin"/>, <see cref="valueEnd"/>) contain the internal name, and those in
    /// [<see cref="valueBegin"/> - 1, <see cref="valueEnd"/> + 1) contain the descriptor.
    /// </para>
    /// </summary>
    private readonly string valueBuffer;

    /// <summary>
    /// The beginning index, inclusive, of the value of this Java field or method type in
    /// <see cref="valueBuffer"/>. This value is an internal name for <see cref="OBJECT"/> and
    /// <see cref="INTERNAL"/> types, and a field or method descriptor in the other cases.
    /// </summary>
    private readonly int valueBegin;

    /// <summary>
    /// The end index, exclusive, of the value of this Java field or method type in
    /// <see cref="valueBuffer"/>. This value is an internal name for <see cref="OBJECT"/> and
    /// <see cref="INTERNAL"/> types, and a field or method descriptor in the other cases.
    /// </summary>
    private readonly int valueEnd;

    /// <summary>
    /// Constructs a reference type.
    /// </summary>
    /// <param name="sort">the sort of this type, see <see cref="sort"/>.</param>
    /// <param name="valueBuffer">a buffer containing the value of this field or method type.</param>
    /// <param name="valueBegin">the beginning index, inclusive, of the value of this field or
    /// method type in valueBuffer.</param>
    /// <param name="valueEnd">the end index, exclusive, of the value of this field or method type
    /// in valueBuffer.</param>
    private Type(int sort, string valueBuffer, int valueBegin, int valueEnd)
    {
        this.sort = sort;
        this.valueBuffer = valueBuffer;
        this.valueBegin = valueBegin;
        this.valueEnd = valueEnd;
    }

    // -----------------------------------------------------------------------------------------------
    // Methods to get Type(s) from a descriptor, a reflected Method or Constructor, other types, etc.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the <see cref="Type"/> corresponding to the given type descriptor.
    /// </summary>
    /// <param name="typeDescriptor">a field or method type descriptor.</param>
    /// <returns>the <see cref="Type"/> corresponding to the given type descriptor.</returns>
    public static Type GetType(string typeDescriptor)
    {
        return GetTypeInternal(typeDescriptor, 0, typeDescriptor.Length);
    }

    /// <summary>
    /// Returns the <see cref="Type"/> corresponding to the given class.
    /// </summary>
    /// <param name="clazz">a class.</param>
    /// <returns>the <see cref="Type"/> corresponding to the given class.</returns>
    public static Type GetType(System.Type clazz)
    {
        if (clazz.IsPrimitive)
        {
            if (clazz == typeof(int))
            {
                return INT_TYPE;
            }
            else if (clazz == typeof(void))
            {
                return VOID_TYPE;
            }
            else if (clazz == typeof(bool))
            {
                return BOOLEAN_TYPE;
            }
            else if (clazz == typeof(byte))
            {
                return BYTE_TYPE;
            }
            else if (clazz == typeof(char))
            {
                return CHAR_TYPE;
            }
            else if (clazz == typeof(short))
            {
                return SHORT_TYPE;
            }
            else if (clazz == typeof(double))
            {
                return DOUBLE_TYPE;
            }
            else if (clazz == typeof(float))
            {
                return FLOAT_TYPE;
            }
            else if (clazz == typeof(long))
            {
                return LONG_TYPE;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        else
        {
            return GetType(GetDescriptor(clazz));
        }
    }

    /// <summary>
    /// Returns the method <see cref="Type"/> corresponding to the given constructor.
    /// </summary>
    /// <param name="constructor">a <see cref="ConstructorInfo"/> object.</param>
    /// <returns>the method <see cref="Type"/> corresponding to the given constructor.</returns>
    public static Type GetType(ConstructorInfo constructor)
    {
        return GetType(GetConstructorDescriptor(constructor));
    }

    /// <summary>
    /// Returns the method <see cref="Type"/> corresponding to the given method.
    /// </summary>
    /// <param name="method">a <see cref="MethodInfo"/> object.</param>
    /// <returns>the method <see cref="Type"/> corresponding to the given method.</returns>
    public static Type GetType(MethodInfo method)
    {
        return GetType(GetMethodDescriptor(method));
    }

    /// <summary>
    /// Returns the type of the elements of this array type. This method should only be used for an
    /// array type.
    /// </summary>
    /// <returns>Returns the type of the elements of this array type.</returns>
    public Type GetElementType()
    {
        int numDimensions = GetDimensions();
        return GetTypeInternal(valueBuffer, valueBegin + numDimensions, valueEnd);
    }

    /// <summary>
    /// Returns the <see cref="Type"/> corresponding to the given internal name.
    /// </summary>
    /// <param name="internalName">an internal name (see <see cref="GetInternalName()"/>).</param>
    /// <returns>the <see cref="Type"/> corresponding to the given internal name.</returns>
    public static Type GetObjectType(string internalName)
    {
        return new Type(
            internalName[0] == '[' ? ARRAY : INTERNAL, internalName, 0, internalName.Length);
    }

    /// <summary>
    /// Returns the <see cref="Type"/> corresponding to the given method descriptor. Equivalent to
    /// <c>Type.GetType(methodDescriptor)</c>.
    /// </summary>
    /// <param name="methodDescriptor">a method descriptor.</param>
    /// <returns>the <see cref="Type"/> corresponding to the given method descriptor.</returns>
    public static Type GetMethodType(string methodDescriptor)
    {
        return new Type(METHOD, methodDescriptor, 0, methodDescriptor.Length);
    }

    /// <summary>
    /// Returns the method <see cref="Type"/> corresponding to the given argument and return types.
    /// </summary>
    /// <param name="returnType">the return type of the method.</param>
    /// <param name="argumentTypes">the argument types of the method.</param>
    /// <returns>the method <see cref="Type"/> corresponding to the given argument and return types.</returns>
    public static Type GetMethodType(Type returnType, params Type[] argumentTypes)
    {
        return GetType(GetMethodDescriptor(returnType, argumentTypes));
    }

    /// <summary>
    /// Returns the argument types of methods of this type. This method should only be used for
    /// method types.
    /// </summary>
    /// <returns>the argument types of methods of this type.</returns>
    public Type[] GetArgumentTypes()
    {
        return GetArgumentTypes(GetDescriptor());
    }

    /// <summary>
    /// Returns the <see cref="Type"/> values corresponding to the argument types of the given
    /// method descriptor.
    /// </summary>
    /// <param name="methodDescriptor">a method descriptor.</param>
    /// <returns>the <see cref="Type"/> values corresponding to the argument types of the given
    /// method descriptor.</returns>
    public static Type[] GetArgumentTypes(string methodDescriptor)
    {
        // First step: compute the number of argument types in methodDescriptor.
        int numArgumentTypes = GetArgumentCount(methodDescriptor);

        // Second step: create a Type instance for each argument type.
        Type[] argumentTypes = new Type[numArgumentTypes];
        // Skip the first character, which is always a '('.
        int currentOffset = 1;
        // Parse and create the argument types, one at each loop iteration.
        int currentArgumentTypeIndex = 0;
        while (methodDescriptor[currentOffset] != ')')
        {
            int currentArgumentTypeOffset = currentOffset;
            while (methodDescriptor[currentOffset] == '[')
            {
                currentOffset++;
            }
            if (methodDescriptor[currentOffset++] == 'L')
            {
                // Skip the argument descriptor content.
                int semiColumnOffset = methodDescriptor.IndexOf(';', currentOffset);
                currentOffset = Math.Max(currentOffset, semiColumnOffset + 1);
            }
            argumentTypes[currentArgumentTypeIndex++] =
                GetTypeInternal(methodDescriptor, currentArgumentTypeOffset, currentOffset);
        }
        return argumentTypes;
    }

    /// <summary>
    /// Returns the <see cref="Type"/> values corresponding to the argument types of the given
    /// method.
    /// </summary>
    /// <param name="method">a method.</param>
    /// <returns>the <see cref="Type"/> values corresponding to the argument types of the given
    /// method.</returns>
    public static Type[] GetArgumentTypes(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        Type[] types = new Type[parameters.Length];
        for (int i = parameters.Length - 1; i >= 0; --i)
        {
            types[i] = GetType(parameters[i].ParameterType);
        }
        return types;
    }

    /// <summary>
    /// Returns the return type of methods of this type. This method should only be used for method
    /// types.
    /// </summary>
    /// <returns>the return type of methods of this type.</returns>
    public Type GetReturnType()
    {
        return GetReturnType(GetDescriptor());
    }

    /// <summary>
    /// Returns the <see cref="Type"/> corresponding to the return type of the given method
    /// descriptor.
    /// </summary>
    /// <param name="methodDescriptor">a method descriptor.</param>
    /// <returns>the <see cref="Type"/> corresponding to the return type of the given method
    /// descriptor.</returns>
    public static Type GetReturnType(string methodDescriptor)
    {
        return GetTypeInternal(
            methodDescriptor, GetReturnTypeOffset(methodDescriptor), methodDescriptor.Length);
    }

    /// <summary>
    /// Returns the <see cref="Type"/> corresponding to the return type of the given method.
    /// </summary>
    /// <param name="method">a method.</param>
    /// <returns>the <see cref="Type"/> corresponding to the return type of the given method.</returns>
    public static Type GetReturnType(MethodInfo method)
    {
        return GetType(method.ReturnType);
    }

    /// <summary>
    /// Returns the start index of the return type of the given method descriptor.
    /// </summary>
    /// <param name="methodDescriptor">a method descriptor.</param>
    /// <returns>the start index of the return type of the given method descriptor.</returns>
    internal static int GetReturnTypeOffset(string methodDescriptor)
    {
        // Skip the first character, which is always a '('.
        int currentOffset = 1;
        // Skip the argument types, one at a each loop iteration.
        while (methodDescriptor[currentOffset] != ')')
        {
            while (methodDescriptor[currentOffset] == '[')
            {
                currentOffset++;
            }
            if (methodDescriptor[currentOffset++] == 'L')
            {
                // Skip the argument descriptor content.
                int semiColumnOffset = methodDescriptor.IndexOf(';', currentOffset);
                currentOffset = Math.Max(currentOffset, semiColumnOffset + 1);
            }
        }
        return currentOffset + 1;
    }

    /// <summary>
    /// Returns the <see cref="Type"/> corresponding to the given field or method descriptor.
    /// </summary>
    /// <param name="descriptorBuffer">a buffer containing the field or method descriptor.</param>
    /// <param name="descriptorBegin">the beginning index, inclusive, of the field or method
    /// descriptor in descriptorBuffer.</param>
    /// <param name="descriptorEnd">the end index, exclusive, of the field or method descriptor in
    /// descriptorBuffer.</param>
    /// <returns>the <see cref="Type"/> corresponding to the given type descriptor.</returns>
    private static Type GetTypeInternal(
        string descriptorBuffer, int descriptorBegin, int descriptorEnd)
    {
        switch (descriptorBuffer[descriptorBegin])
        {
            case 'V':
                return VOID_TYPE;
            case 'Z':
                return BOOLEAN_TYPE;
            case 'C':
                return CHAR_TYPE;
            case 'B':
                return BYTE_TYPE;
            case 'S':
                return SHORT_TYPE;
            case 'I':
                return INT_TYPE;
            case 'F':
                return FLOAT_TYPE;
            case 'J':
                return LONG_TYPE;
            case 'D':
                return DOUBLE_TYPE;
            case '[':
                return new Type(ARRAY, descriptorBuffer, descriptorBegin, descriptorEnd);
            case 'L':
                return new Type(OBJECT, descriptorBuffer, descriptorBegin + 1, descriptorEnd - 1);
            case '(':
                return new Type(METHOD, descriptorBuffer, descriptorBegin, descriptorEnd);
            default:
                throw new ArgumentException("Invalid descriptor: " + descriptorBuffer);
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Methods to get class names, internal names or descriptors.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the binary name of the class corresponding to this type. This method must not be
    /// used on method types.
    /// </summary>
    /// <returns>the binary name of the class corresponding to this type.</returns>
    public string GetClassName()
    {
        switch (sort)
        {
            case VOID:
                return "void";
            case BOOLEAN:
                return "boolean";
            case CHAR:
                return "char";
            case BYTE:
                return "byte";
            case SHORT:
                return "short";
            case INT:
                return "int";
            case FLOAT:
                return "float";
            case LONG:
                return "long";
            case DOUBLE:
                return "double";
            case ARRAY:
                StringBuilder stringBuilder = new StringBuilder(GetElementType().GetClassName());
                for (int i = GetDimensions(); i > 0; --i)
                {
                    stringBuilder.Append("[]");
                }
                return stringBuilder.ToString();
            case OBJECT:
            case INTERNAL:
                return valueBuffer.Substring(valueBegin, valueEnd - valueBegin).Replace('/', '.');
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Returns the internal name of the class corresponding to this object or array type. The
    /// internal name of a class is its fully qualified name (as returned by Class.getName(), where
    /// '.' are replaced by '/'). This method should only be used for an object or array type.
    /// </summary>
    /// <returns>the internal name of the class corresponding to this object type.</returns>
    public string GetInternalName()
    {
        return valueBuffer.Substring(valueBegin, valueEnd - valueBegin);
    }

    /// <summary>
    /// Returns the internal name of the given class. The internal name of a class is its fully
    /// qualified name, as returned by Class.getName(), where '.' are replaced by '/'.
    /// </summary>
    /// <param name="clazz">an object or array class.</param>
    /// <returns>the internal name of the given class.</returns>
    public static string GetInternalName(System.Type clazz)
    {
        return (clazz.FullName ?? clazz.Name).Replace('.', '/');
    }

    /// <summary>
    /// Returns the descriptor corresponding to this type.
    /// </summary>
    /// <returns>the descriptor corresponding to this type.</returns>
    public string GetDescriptor()
    {
        if (sort == OBJECT)
        {
            return valueBuffer.Substring(valueBegin - 1, valueEnd - valueBegin + 2);
        }
        else if (sort == INTERNAL)
        {
            return 'L' + valueBuffer.Substring(valueBegin, valueEnd - valueBegin) + ';';
        }
        else
        {
            return valueBuffer.Substring(valueBegin, valueEnd - valueBegin);
        }
    }

    /// <summary>
    /// Returns the descriptor corresponding to the given class.
    /// </summary>
    /// <param name="clazz">an object class, a primitive class or an array class.</param>
    /// <returns>the descriptor corresponding to the given class.</returns>
    public static string GetDescriptor(System.Type clazz)
    {
        StringBuilder stringBuilder = new StringBuilder();
        AppendDescriptor(clazz, stringBuilder);
        return stringBuilder.ToString();
    }

    /// <summary>
    /// Returns the descriptor corresponding to the given constructor.
    /// </summary>
    /// <param name="constructor">a <see cref="ConstructorInfo"/> object.</param>
    /// <returns>the descriptor of the given constructor.</returns>
    public static string GetConstructorDescriptor(ConstructorInfo constructor)
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append('(');
        ParameterInfo[] parameters = constructor.GetParameters();
        foreach (ParameterInfo parameter in parameters)
        {
            AppendDescriptor(parameter.ParameterType, stringBuilder);
        }
        return stringBuilder.Append(")V").ToString();
    }

    /// <summary>
    /// Returns the descriptor corresponding to the given argument and return types.
    /// </summary>
    /// <param name="returnType">the return type of the method.</param>
    /// <param name="argumentTypes">the argument types of the method.</param>
    /// <returns>the descriptor corresponding to the given argument and return types.</returns>
    public static string GetMethodDescriptor(Type returnType, params Type[] argumentTypes)
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append('(');
        foreach (Type argumentType in argumentTypes)
        {
            argumentType.AppendDescriptor(stringBuilder);
        }
        stringBuilder.Append(')');
        returnType.AppendDescriptor(stringBuilder);
        return stringBuilder.ToString();
    }

    /// <summary>
    /// Returns the descriptor corresponding to the given method.
    /// </summary>
    /// <param name="method">a <see cref="MethodInfo"/> object.</param>
    /// <returns>the descriptor of the given method.</returns>
    public static string GetMethodDescriptor(MethodInfo method)
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append('(');
        ParameterInfo[] parameters = method.GetParameters();
        foreach (ParameterInfo parameter in parameters)
        {
            AppendDescriptor(parameter.ParameterType, stringBuilder);
        }
        stringBuilder.Append(')');
        AppendDescriptor(method.ReturnType, stringBuilder);
        return stringBuilder.ToString();
    }

    /// <summary>
    /// Appends the descriptor corresponding to this type to the given string buffer.
    /// </summary>
    /// <param name="stringBuilder">the string builder to which the descriptor must be appended.</param>
    private void AppendDescriptor(StringBuilder stringBuilder)
    {
        if (sort == OBJECT)
        {
            stringBuilder.Append(valueBuffer, valueBegin - 1, valueEnd - valueBegin + 2);
        }
        else if (sort == INTERNAL)
        {
            stringBuilder.Append('L').Append(valueBuffer, valueBegin, valueEnd - valueBegin).Append(';');
        }
        else
        {
            stringBuilder.Append(valueBuffer, valueBegin, valueEnd - valueBegin);
        }
    }

    /// <summary>
    /// Appends the descriptor of the given class to the given string builder.
    /// </summary>
    /// <param name="clazz">the class whose descriptor must be computed.</param>
    /// <param name="stringBuilder">the string builder to which the descriptor must be appended.</param>
    private static void AppendDescriptor(System.Type clazz, StringBuilder stringBuilder)
    {
        System.Type currentClass = clazz;
        while (currentClass.IsArray)
        {
            stringBuilder.Append('[');
            currentClass = currentClass.GetElementType()!;
        }
        if (currentClass.IsPrimitive)
        {
            char descriptor;
            if (currentClass == typeof(int))
            {
                descriptor = 'I';
            }
            else if (currentClass == typeof(void))
            {
                descriptor = 'V';
            }
            else if (currentClass == typeof(bool))
            {
                descriptor = 'Z';
            }
            else if (currentClass == typeof(byte))
            {
                descriptor = 'B';
            }
            else if (currentClass == typeof(char))
            {
                descriptor = 'C';
            }
            else if (currentClass == typeof(short))
            {
                descriptor = 'S';
            }
            else if (currentClass == typeof(double))
            {
                descriptor = 'D';
            }
            else if (currentClass == typeof(float))
            {
                descriptor = 'F';
            }
            else if (currentClass == typeof(long))
            {
                descriptor = 'J';
            }
            else
            {
                throw new InvalidOperationException();
            }
            stringBuilder.Append(descriptor);
        }
        else
        {
            stringBuilder.Append('L').Append(GetInternalName(currentClass)).Append(';');
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Methods to get the sort, dimension, size, and opcodes corresponding to a Type or descriptor.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the sort of this type.
    /// </summary>
    /// <returns><see cref="VOID"/>, <see cref="BOOLEAN"/>, <see cref="CHAR"/>, <see cref="BYTE"/>,
    /// <see cref="SHORT"/>, <see cref="INT"/>, <see cref="FLOAT"/>, <see cref="LONG"/>,
    /// <see cref="DOUBLE"/>, <see cref="ARRAY"/>, <see cref="OBJECT"/> or <see cref="METHOD"/>.</returns>
    public int GetSort()
    {
        return sort == INTERNAL ? OBJECT : sort;
    }

    /// <summary>
    /// Returns the number of dimensions of this array type. This method should only be used for an
    /// array type.
    /// </summary>
    /// <returns>the number of dimensions of this array type.</returns>
    public int GetDimensions()
    {
        int numDimensions = 1;
        while (valueBuffer[valueBegin + numDimensions] == '[')
        {
            numDimensions++;
        }
        return numDimensions;
    }

    /// <summary>
    /// Returns the size of values of this type. This method must not be used for method types.
    /// </summary>
    /// <returns>the size of values of this type, i.e., 2 for <c>long</c> and <c>double</c>, 0 for
    /// <c>void</c> and 1 otherwise.</returns>
    public int GetSize()
    {
        switch (sort)
        {
            case VOID:
                return 0;
            case BOOLEAN:
            case CHAR:
            case BYTE:
            case SHORT:
            case INT:
            case FLOAT:
            case ARRAY:
            case OBJECT:
            case INTERNAL:
                return 1;
            case LONG:
            case DOUBLE:
                return 2;
            default:
                throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// Returns the number of arguments of this method type. This method should only be used for
    /// method types.
    /// </summary>
    /// <returns>the number of arguments of this method type. Each argument counts for 1, even long
    /// and double ones. The implicit this argument is not counted.</returns>
    public int GetArgumentCount()
    {
        return GetArgumentCount(GetDescriptor());
    }

    /// <summary>
    /// Returns the number of arguments in the given method descriptor.
    /// </summary>
    /// <param name="methodDescriptor">a method descriptor.</param>
    /// <returns>the number of arguments in the given method descriptor. Each argument counts for 1,
    /// even long and double ones. The implicit this argument is not counted.</returns>
    public static int GetArgumentCount(string methodDescriptor)
    {
        int argumentCount = 0;
        // Skip the first character, which is always a '('.
        int currentOffset = 1;
        // Parse the argument types, one at a each loop iteration.
        while (methodDescriptor[currentOffset] != ')')
        {
            while (methodDescriptor[currentOffset] == '[')
            {
                currentOffset++;
            }
            if (methodDescriptor[currentOffset++] == 'L')
            {
                // Skip the argument descriptor content.
                int semiColumnOffset = methodDescriptor.IndexOf(';', currentOffset);
                currentOffset = Math.Max(currentOffset, semiColumnOffset + 1);
            }
            ++argumentCount;
        }
        return argumentCount;
    }

    /// <summary>
    /// Returns the size of the arguments and of the return value of methods of this type. This
    /// method should only be used for method types.
    /// </summary>
    /// <returns>the size of the arguments of the method (plus one for the implicit this argument),
    /// argumentsSize, and the size of its return value, returnSize, packed into a single int i =
    /// <c>(argumentsSize &lt;&lt; 2) | returnSize</c> (argumentsSize is therefore equal to
    /// <c>i &gt;&gt; 2</c>, and returnSize to <c>i &amp; 0x03</c>). Long and double values have size
    /// 2, the others have size 1.</returns>
    public int GetArgumentsAndReturnSizes()
    {
        return GetArgumentsAndReturnSizes(GetDescriptor());
    }

    /// <summary>
    /// Computes the size of the arguments and of the return value of a method.
    /// </summary>
    /// <param name="methodDescriptor">a method descriptor.</param>
    /// <returns>the size of the arguments of the method (plus one for the implicit this argument),
    /// argumentsSize, and the size of its return value, returnSize, packed into a single int i =
    /// <c>(argumentsSize &lt;&lt; 2) | returnSize</c> (argumentsSize is therefore equal to
    /// <c>i &gt;&gt; 2</c>, and returnSize to <c>i &amp; 0x03</c>). Long and double values have size
    /// 2, the others have size 1.</returns>
    public static int GetArgumentsAndReturnSizes(string methodDescriptor)
    {
        int argumentsSize = 1;
        // Skip the first character, which is always a '('.
        int currentOffset = 1;
        int currentChar = methodDescriptor[currentOffset];
        // Parse the argument types and compute their size, one at a each loop iteration.
        while (currentChar != ')')
        {
            if (currentChar == 'J' || currentChar == 'D')
            {
                currentOffset++;
                argumentsSize += 2;
            }
            else
            {
                while (methodDescriptor[currentOffset] == '[')
                {
                    currentOffset++;
                }
                if (methodDescriptor[currentOffset++] == 'L')
                {
                    // Skip the argument descriptor content.
                    int semiColumnOffset = methodDescriptor.IndexOf(';', currentOffset);
                    currentOffset = Math.Max(currentOffset, semiColumnOffset + 1);
                }
                argumentsSize += 1;
            }
            currentChar = methodDescriptor[currentOffset];
        }
        currentChar = methodDescriptor[currentOffset + 1];
        if (currentChar == 'V')
        {
            return argumentsSize << 2;
        }
        else
        {
            int returnSize = (currentChar == 'J' || currentChar == 'D') ? 2 : 1;
            return (argumentsSize << 2) | returnSize;
        }
    }

    /// <summary>
    /// Returns a JVM instruction opcode adapted to this <see cref="Type"/>. This method must not be
    /// used for method types.
    /// </summary>
    /// <param name="opcode">a JVM instruction opcode. This opcode must be one of ILOAD, ISTORE,
    /// IALOAD, IASTORE, IADD, ISUB, IMUL, IDIV, IREM, INEG, ISHL, ISHR, IUSHR, IAND, IOR, IXOR and
    /// IRETURN.</param>
    /// <returns>an opcode that is similar to the given opcode, but adapted to this
    /// <see cref="Type"/>. For example, if this type is <c>float</c> and <paramref name="opcode"/>
    /// is IRETURN, this method returns FRETURN.</returns>
    public int GetOpcode(int opcode)
    {
        if (opcode == Opcodes.IALOAD || opcode == Opcodes.IASTORE)
        {
            switch (sort)
            {
                case BOOLEAN:
                case BYTE:
                    return opcode + (Opcodes.BALOAD - Opcodes.IALOAD);
                case CHAR:
                    return opcode + (Opcodes.CALOAD - Opcodes.IALOAD);
                case SHORT:
                    return opcode + (Opcodes.SALOAD - Opcodes.IALOAD);
                case INT:
                    return opcode;
                case FLOAT:
                    return opcode + (Opcodes.FALOAD - Opcodes.IALOAD);
                case LONG:
                    return opcode + (Opcodes.LALOAD - Opcodes.IALOAD);
                case DOUBLE:
                    return opcode + (Opcodes.DALOAD - Opcodes.IALOAD);
                case ARRAY:
                case OBJECT:
                case INTERNAL:
                    return opcode + (Opcodes.AALOAD - Opcodes.IALOAD);
                case METHOD:
                case VOID:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }
        else
        {
            switch (sort)
            {
                case VOID:
                    if (opcode != Opcodes.IRETURN)
                    {
                        throw new NotSupportedException();
                    }
                    return Opcodes.RETURN;
                case BOOLEAN:
                case BYTE:
                case CHAR:
                case SHORT:
                case INT:
                    return opcode;
                case FLOAT:
                    return opcode + (Opcodes.FRETURN - Opcodes.IRETURN);
                case LONG:
                    return opcode + (Opcodes.LRETURN - Opcodes.IRETURN);
                case DOUBLE:
                    return opcode + (Opcodes.DRETURN - Opcodes.IRETURN);
                case ARRAY:
                case OBJECT:
                case INTERNAL:
                    if (opcode != Opcodes.ILOAD && opcode != Opcodes.ISTORE && opcode != Opcodes.IRETURN)
                    {
                        throw new NotSupportedException();
                    }
                    return opcode + (Opcodes.ARETURN - Opcodes.IRETURN);
                case METHOD:
                    throw new NotSupportedException();
                default:
                    throw new InvalidOperationException();
            }
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Equals, hashCode and toString.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Tests if the given object is equal to this type.
    /// </summary>
    /// <param name="obj">the object to be compared to this type.</param>
    /// <returns><c>true</c> if the given object is equal to this type.</returns>
    public override bool Equals(object? obj)
    {
        if (this == obj)
        {
            return true;
        }
        if (!(obj is Type))
        {
            return false;
        }
        Type other = (Type)obj;
        if ((sort == INTERNAL ? OBJECT : sort) != (other.sort == INTERNAL ? OBJECT : other.sort))
        {
            return false;
        }
        int begin = valueBegin;
        int end = valueEnd;
        int otherBegin = other.valueBegin;
        int otherEnd = other.valueEnd;
        // Compare the values.
        if (end - begin != otherEnd - otherBegin)
        {
            return false;
        }
        for (int i = begin, j = otherBegin; i < end; i++, j++)
        {
            if (valueBuffer[i] != other.valueBuffer[j])
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns a hash code value for this type.
    /// </summary>
    /// <returns>a hash code value for this type.</returns>
    public override int GetHashCode()
    {
        int hashCode = 13 * (sort == INTERNAL ? OBJECT : sort);
        if (sort >= ARRAY)
        {
            for (int i = valueBegin, end = valueEnd; i < end; i++)
            {
                hashCode = 17 * (hashCode + valueBuffer[i]);
            }
        }
        return hashCode;
    }

    /// <summary>
    /// Returns a string representation of this type.
    /// </summary>
    /// <returns>the descriptor of this type.</returns>
    public override string ToString()
    {
        return GetDescriptor();
    }
}
