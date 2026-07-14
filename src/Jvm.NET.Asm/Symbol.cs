// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

namespace Jvm.NET.Asm;

/// <summary>
/// An entry of the constant pool, of the BootstrapMethods attribute, or of the (ASM specific) type
/// table of a class.
/// </summary>
/// <remarks>
/// See <see href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.4">JVMS 4.4</see>
/// and <see href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.7.23">JVMS 4.7.23</see>.
/// </remarks>
internal class Symbol
{
    // Tag values for the constant pool entries (using the same order as in the JVMS).

    /// <summary>The tag value of CONSTANT_Class_info JVMS structures.</summary>
    public const int CONSTANT_CLASS_TAG = 7;

    /// <summary>The tag value of CONSTANT_Fieldref_info JVMS structures.</summary>
    public const int CONSTANT_FIELDREF_TAG = 9;

    /// <summary>The tag value of CONSTANT_Methodref_info JVMS structures.</summary>
    public const int CONSTANT_METHODREF_TAG = 10;

    /// <summary>The tag value of CONSTANT_InterfaceMethodref_info JVMS structures.</summary>
    public const int CONSTANT_INTERFACE_METHODREF_TAG = 11;

    /// <summary>The tag value of CONSTANT_String_info JVMS structures.</summary>
    public const int CONSTANT_STRING_TAG = 8;

    /// <summary>The tag value of CONSTANT_Integer_info JVMS structures.</summary>
    public const int CONSTANT_INTEGER_TAG = 3;

    /// <summary>The tag value of CONSTANT_Float_info JVMS structures.</summary>
    public const int CONSTANT_FLOAT_TAG = 4;

    /// <summary>The tag value of CONSTANT_Long_info JVMS structures.</summary>
    public const int CONSTANT_LONG_TAG = 5;

    /// <summary>The tag value of CONSTANT_Double_info JVMS structures.</summary>
    public const int CONSTANT_DOUBLE_TAG = 6;

    /// <summary>The tag value of CONSTANT_NameAndType_info JVMS structures.</summary>
    public const int CONSTANT_NAME_AND_TYPE_TAG = 12;

    /// <summary>The tag value of CONSTANT_Utf8_info JVMS structures.</summary>
    public const int CONSTANT_UTF8_TAG = 1;

    /// <summary>The tag value of CONSTANT_MethodHandle_info JVMS structures.</summary>
    public const int CONSTANT_METHOD_HANDLE_TAG = 15;

    /// <summary>The tag value of CONSTANT_MethodType_info JVMS structures.</summary>
    public const int CONSTANT_METHOD_TYPE_TAG = 16;

    /// <summary>The tag value of CONSTANT_Dynamic_info JVMS structures.</summary>
    public const int CONSTANT_DYNAMIC_TAG = 17;

    /// <summary>The tag value of CONSTANT_InvokeDynamic_info JVMS structures.</summary>
    public const int CONSTANT_INVOKE_DYNAMIC_TAG = 18;

    /// <summary>The tag value of CONSTANT_Module_info JVMS structures.</summary>
    public const int CONSTANT_MODULE_TAG = 19;

    /// <summary>The tag value of CONSTANT_Package_info JVMS structures.</summary>
    public const int CONSTANT_PACKAGE_TAG = 20;

    // Tag values for the BootstrapMethods attribute entries (ASM specific tag).

    /// <summary>The tag value of the BootstrapMethods attribute entries.</summary>
    public const int BOOTSTRAP_METHOD_TAG = 64;

    // Tag values for the type table entries (ASM specific tags).

    /// <summary>The tag value of a normal type entry in the (ASM specific) type table of a class.</summary>
    public const int TYPE_TAG = 128;

    /// <summary>
    /// The tag value of an uninitialized type entry in the type table of a class. This type is used
    /// for the normal case where the NEW instruction is before the &lt;init&gt; constructor call (in
    /// bytecode offset order), i.e. when the label of the NEW instruction is resolved when the
    /// constructor call is visited. If the NEW instruction is after the constructor call, use the
    /// <see cref="FORWARD_UNINITIALIZED_TYPE_TAG"/> tag value instead.
    /// </summary>
    public const int UNINITIALIZED_TYPE_TAG = 129;

    /// <summary>
    /// The tag value of an uninitialized type entry in the type table of a class. This type is used
    /// for the unusual case where the NEW instruction is after the &lt;init&gt; constructor call (in
    /// bytecode offset order), i.e. when the label of the NEW instruction is not resolved when the
    /// constructor call is visited. If the NEW instruction is before the constructor call, use the
    /// <see cref="UNINITIALIZED_TYPE_TAG"/> tag value instead.
    /// </summary>
    public const int FORWARD_UNINITIALIZED_TYPE_TAG = 130;

    /// <summary>The tag value of a merged type entry in the (ASM specific) type table of a class.</summary>
    public const int MERGED_TYPE_TAG = 131;

    // Instance fields.

    /// <summary>
    /// The index of this symbol in the constant pool, in the BootstrapMethods attribute, or in the
    /// (ASM specific) type table of a class (depending on the <see cref="Tag"/> value).
    /// </summary>
    internal readonly int Index;

    /// <summary>
    /// A tag indicating the type of this symbol. Must be one of the static tag values defined in this
    /// class.
    /// </summary>
    internal readonly int Tag;

    /// <summary>
    /// The internal name of the owner class of this symbol. Only used for
    /// <see cref="CONSTANT_FIELDREF_TAG"/>, <see cref="CONSTANT_METHODREF_TAG"/>,
    /// <see cref="CONSTANT_INTERFACE_METHODREF_TAG"/>, and <see cref="CONSTANT_METHOD_HANDLE_TAG"/>
    /// symbols.
    /// </summary>
    internal readonly string? Owner;

    /// <summary>
    /// The name of the class field or method corresponding to this symbol. Only used for
    /// <see cref="CONSTANT_FIELDREF_TAG"/>, <see cref="CONSTANT_METHODREF_TAG"/>,
    /// <see cref="CONSTANT_INTERFACE_METHODREF_TAG"/>, <see cref="CONSTANT_NAME_AND_TYPE_TAG"/>,
    /// <see cref="CONSTANT_METHOD_HANDLE_TAG"/>, <see cref="CONSTANT_DYNAMIC_TAG"/> and
    /// <see cref="CONSTANT_INVOKE_DYNAMIC_TAG"/> symbols.
    /// </summary>
    internal readonly string? Name;

    /// <summary>
    /// The string value of this symbol. This is:
    /// <list type="bullet">
    ///   <item>a field or method descriptor for <see cref="CONSTANT_FIELDREF_TAG"/>,
    ///       <see cref="CONSTANT_METHODREF_TAG"/>,
    ///       <see cref="CONSTANT_INTERFACE_METHODREF_TAG"/>,
    ///       <see cref="CONSTANT_NAME_AND_TYPE_TAG"/>, <see cref="CONSTANT_METHOD_HANDLE_TAG"/>,
    ///       <see cref="CONSTANT_METHOD_TYPE_TAG"/>, <see cref="CONSTANT_DYNAMIC_TAG"/> and
    ///       <see cref="CONSTANT_INVOKE_DYNAMIC_TAG"/> symbols,</item>
    ///   <item>an arbitrary string for <see cref="CONSTANT_UTF8_TAG"/> and
    ///       <see cref="CONSTANT_STRING_TAG"/> symbols,</item>
    ///   <item>an internal class name for <see cref="CONSTANT_CLASS_TAG"/>, <see cref="TYPE_TAG"/>,
    ///       <see cref="UNINITIALIZED_TYPE_TAG"/> and
    ///       <see cref="FORWARD_UNINITIALIZED_TYPE_TAG"/> symbols,</item>
    ///   <item><c>null</c> for the other types of symbol.</item>
    /// </list>
    /// </summary>
    internal readonly string? Value;

    /// <summary>
    /// The numeric value of this symbol. This is:
    /// <list type="bullet">
    ///   <item>the symbol's value for <see cref="CONSTANT_INTEGER_TAG"/>,
    ///       <see cref="CONSTANT_FLOAT_TAG"/>, <see cref="CONSTANT_LONG_TAG"/>,
    ///       <see cref="CONSTANT_DOUBLE_TAG"/>,</item>
    ///   <item>the CONSTANT_MethodHandle_info reference_kind field value for
    ///       <see cref="CONSTANT_METHOD_HANDLE_TAG"/> symbols (or this value left shifted by 8 bits
    ///       for reference_kind values larger than or equal to H_INVOKEVIRTUAL and if the method owner
    ///       is an interface),</item>
    ///   <item>the CONSTANT_InvokeDynamic_info bootstrap_method_attr_index field value for
    ///       <see cref="CONSTANT_INVOKE_DYNAMIC_TAG"/> symbols,</item>
    ///   <item>the offset of a bootstrap method in the BootstrapMethods boostrap_methods array, for
    ///       <see cref="CONSTANT_DYNAMIC_TAG"/> or <see cref="BOOTSTRAP_METHOD_TAG"/> symbols,</item>
    ///   <item>the bytecode offset of the NEW instruction that created an
    ///       ITEM_UNINITIALIZED type for <see cref="UNINITIALIZED_TYPE_TAG"/> symbols,</item>
    ///   <item>the index of the Label (in the <see cref="SymbolTable.LabelTable"/> table) of the NEW
    ///       instruction that created an ITEM_UNINITIALIZED type for
    ///       <see cref="FORWARD_UNINITIALIZED_TYPE_TAG"/> symbols,</item>
    ///   <item>the indices (in the class' type table) of two <see cref="TYPE_TAG"/> source types for
    ///       <see cref="MERGED_TYPE_TAG"/> symbols,</item>
    ///   <item>0 for the other types of symbol.</item>
    /// </list>
    /// </summary>
    internal readonly long Data;

    /// <summary>
    /// Additional information about this symbol, generally computed lazily. <i>Warning: the value of
    /// this field is ignored when comparing Symbol instances</i> (to avoid duplicate entries in a
    /// SymbolTable). Therefore, this field should only contain data that can be computed from the
    /// other fields of this class. It contains:
    /// <list type="bullet">
    ///   <item>the <see cref="Type.GetArgumentsAndReturnSizes(string)"/> of the symbol's method
    ///       descriptor for <see cref="CONSTANT_METHODREF_TAG"/>,
    ///       <see cref="CONSTANT_INTERFACE_METHODREF_TAG"/> and
    ///       <see cref="CONSTANT_INVOKE_DYNAMIC_TAG"/> symbols,</item>
    ///   <item>the index in the InnerClasses_attribute 'classes' array (plus one) corresponding to
    ///       this class, for <see cref="CONSTANT_CLASS_TAG"/> symbols,</item>
    ///   <item>the index (in the class' type table) of the merged type of the two source types for
    ///       <see cref="MERGED_TYPE_TAG"/> symbols,</item>
    ///   <item>0 for the other types of symbol, or if this field has not been computed yet.</item>
    /// </list>
    /// </summary>
    internal int Info;

    /// <summary>
    /// Constructs a new Symbol. This constructor can't be used directly because the Symbol class is
    /// abstract. Instead, use the factory methods of the <see cref="SymbolTable"/> class.
    /// </summary>
    /// <param name="index">the symbol index in the constant pool, in the BootstrapMethods attribute,
    /// or in the (ASM specific) type table of a class (depending on
    /// <paramref name="tag"/>).</param>
    /// <param name="tag">the symbol type. Must be one of the static tag values defined in this
    /// class.</param>
    /// <param name="owner">The internal name of the symbol's owner class. Maybe <c>null</c>.</param>
    /// <param name="name">The name of the symbol's corresponding class field or method. Maybe
    /// <c>null</c>.</param>
    /// <param name="value">The string value of this symbol. Maybe <c>null</c>.</param>
    /// <param name="data">The numeric value of this symbol.</param>
    internal Symbol(int index, int tag, string? owner, string? name, string? value, long data)
    {
        Index = index;
        Tag = tag;
        Owner = owner;
        Name = name;
        Value = value;
        Data = data;
    }

    /// <summary>
    /// Returns the result <see cref="Type.GetArgumentsAndReturnSizes(string)"/> on
    /// <see cref="Value"/>.
    /// </summary>
    /// <returns>the result <see cref="Type.GetArgumentsAndReturnSizes(string)"/> on
    /// <see cref="Value"/> (memoized in <see cref="Info"/> for efficiency). This should only be used
    /// for <see cref="CONSTANT_METHODREF_TAG"/>, <see cref="CONSTANT_INTERFACE_METHODREF_TAG"/> and
    /// <see cref="CONSTANT_INVOKE_DYNAMIC_TAG"/> symbols.</returns>
    internal int GetArgumentsAndReturnSizes()
    {
        if (Info == 0)
        {
            Info = Type.GetArgumentsAndReturnSizes(Value!);
        }
        return Info;
    }
}
