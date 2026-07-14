// ASM: a very small and fast Java bytecode manipulation framework
// Copyright (c) 2000-2011 INRIA, France Telecom
// All rights reserved.
//
// BSD 3-Clause License. See LICENSE.txt in the ASM source tree.
//
// C# port for Jvm.NET.

using System.Runtime.CompilerServices;

namespace Jvm.NET.Asm;

/// <summary>
/// The constant pool entries, the BootstrapMethods attribute entries and the (ASM specific) type
/// table entries of a class.
/// </summary>
/// <remarks>
/// See <see href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.4">JVMS 4.4</see>
/// and <see href="https://docs.oracle.com/javase/specs/jvms/se9/html/jvms-4.html#jvms-4.7.23">JVMS 4.7.23</see>.
/// </remarks>
internal sealed class SymbolTable
{
    /// <summary>
    /// The ClassWriter to which this SymbolTable belongs. This is only used to get access to
    /// <c>ClassWriter.GetCommonSuperClass</c> and to serialize custom attributes.
    /// </summary>
    internal readonly object? ClassWriter; // TODO: ClassWriter

    /// <summary>
    /// The ClassReader from which this SymbolTable was constructed, or <c>null</c> if it was
    /// constructed from scratch.
    /// </summary>
    private readonly object? sourceClassReader; // TODO: ClassReader

    /// <summary>The major version number of the class to which this symbol table belongs.</summary>
    private int majorVersion;

    /// <summary>The internal name of the class to which this symbol table belongs.</summary>
    private string? className;

    /// <summary>
    /// The total number of <see cref="Entry"/> instances in <see cref="entries"/>. This includes
    /// entries that are accessible (recursively) via <see cref="Entry.Next"/>.
    /// </summary>
    private int entryCount;

    /// <summary>
    /// A hash set of all the entries in this SymbolTable (this includes the constant pool entries,
    /// the bootstrap method entries and the type table entries). Each <see cref="Entry"/> instance is
    /// stored at the array index given by its hash code modulo the array size. If several entries
    /// must be stored at the same array index, they are linked together via their
    /// <see cref="Entry.Next"/> field. The factory methods of this class make sure that this table
    /// does not contain duplicated entries.
    /// </summary>
    private Entry[] entries;

    /// <summary>
    /// The number of constant pool items in <see cref="constantPool"/>, plus 1. The first constant
    /// pool item has index 1, and long and double items count for two items.
    /// </summary>
    private int constantPoolCount;

    /// <summary>
    /// The content of the ClassFile's constant_pool JVMS structure corresponding to this
    /// SymbolTable. The ClassFile's constant_pool_count field is <i>not</i> included.
    /// </summary>
    private ByteVector constantPool;

    /// <summary>
    /// The number of bootstrap methods in <see cref="bootstrapMethods"/>. Corresponds to the
    /// BootstrapMethods_attribute's num_bootstrap_methods field value.
    /// </summary>
    private int bootstrapMethodCount;

    /// <summary>
    /// The content of the BootstrapMethods attribute 'bootstrap_methods' array corresponding to this
    /// SymbolTable. Note that the first 6 bytes of the BootstrapMethods_attribute, and its
    /// num_bootstrap_methods field, are <i>not</i> included.
    /// </summary>
    private ByteVector? bootstrapMethods;

    /// <summary>
    /// The actual number of elements in <see cref="typeTable"/>. These elements are stored from index
    /// 0 to typeCount (excluded). The other array entries are empty.
    /// </summary>
    private int typeCount;

    /// <summary>
    /// An ASM specific type table used to temporarily store internal names that will not necessarily
    /// be stored in the constant pool. This type table is used by the control flow and data flow
    /// analysis algorithm used to compute stack map frames from scratch. This array stores
    /// <see cref="Symbol.TYPE_TAG"/>, <see cref="Symbol.UNINITIALIZED_TYPE_TAG"/>,
    /// <see cref="Symbol.FORWARD_UNINITIALIZED_TYPE_TAG"/> and
    /// <see cref="Symbol.MERGED_TYPE_TAG"/> entries. The type symbol at index <c>i</c> has its
    /// <see cref="Symbol.Index"/> equal to <c>i</c> (and vice versa).
    /// </summary>
    private Entry[]? typeTable;

    /// <summary>
    /// The actual number of <see cref="LabelEntry"/> in <see cref="labelTable"/>. These elements are
    /// stored from index 0 to labelCount (excluded). The other array entries are empty. These label
    /// entries are also stored in the <see cref="labelEntries"/> hash set.
    /// </summary>
    private int labelCount;

    /// <summary>
    /// The labels corresponding to the "forward uninitialized" types in the ASM specific
    /// <see cref="typeTable"/> (see <see cref="Symbol.FORWARD_UNINITIALIZED_TYPE_TAG"/>). The label
    /// entry at index <c>i</c> has its <see cref="LabelEntry.Index"/> equal to <c>i</c> (and vice
    /// versa).
    /// </summary>
    private LabelEntry[]? labelTable;

    /// <summary>
    /// A hash set of all the <see cref="LabelEntry"/> elements in the <see cref="labelTable"/>. Each
    /// <see cref="LabelEntry"/> instance is stored at the array index given by its hash code modulo
    /// the array size. If several entries must be stored at the same array index, they are linked
    /// together via their <see cref="LabelEntry.Next"/> field. The
    /// <see cref="GetOrAddLabelEntry(Label)"/> method ensures that this table does not contain
    /// duplicated entries.
    /// </summary>
    private LabelEntry[]? labelEntries;

    /// <summary>
    /// Constructs a new, empty SymbolTable for the given ClassWriter.
    /// </summary>
    /// <param name="classWriter">a ClassWriter.</param>
    internal SymbolTable(object? classWriter) // TODO: ClassWriter
    {
        ClassWriter = classWriter;
        sourceClassReader = null;
        entries = new Entry[256];
        constantPoolCount = 1;
        constantPool = new ByteVector();
    }

    /// <summary>
    /// Constructs a new SymbolTable for the given ClassWriter, initialized with the constant pool
    /// and bootstrap methods of the given ClassReader.
    /// </summary>
    /// <param name="classWriter">a ClassWriter.</param>
    /// <param name="classReader">the ClassReader whose constant pool and bootstrap methods must be
    /// copied to initialize the SymbolTable.</param>
    internal SymbolTable(object? classWriter, object? classReader) // TODO: ClassWriter, ClassReader
    {
        ClassWriter = classWriter;
        sourceClassReader = classReader;
        // TODO: ClassReader - copy the constant pool binary content and bootstrap methods when
        // ClassReader is ported. The full implementation is deferred until then.
        throw new NotImplementedException("// TODO: ClassReader not ported");
    }

    /// <summary>
    /// Read the BootstrapMethods 'bootstrap_methods' array binary content and add them as entries of
    /// the SymbolTable.
    /// </summary>
    /// <param name="classReader">the ClassReader whose bootstrap methods must be copied to
    /// initialize the SymbolTable.</param>
    /// <param name="charBuffer">a buffer used to read strings in the constant pool.</param>
    private void CopyBootstrapMethods(object? classReader, char[] charBuffer) // TODO: ClassReader
    {
        // TODO: ClassReader - copy the BootstrapMethods content when ClassReader is ported.
        throw new NotImplementedException("// TODO: ClassReader not ported");
    }

    /// <summary>
    /// Returns the ClassReader from which this SymbolTable was constructed.
    /// </summary>
    /// <returns>the ClassReader from which this SymbolTable was constructed, or <c>null</c> if it
    /// was constructed from scratch.</returns>
    internal object? GetSource() // TODO: ClassReader
    {
        return sourceClassReader;
    }

    /// <summary>
    /// Returns the major version of the class to which this symbol table belongs.
    /// </summary>
    /// <returns>the major version of the class to which this symbol table belongs.</returns>
    internal int GetMajorVersion()
    {
        return majorVersion;
    }

    /// <summary>
    /// Returns the internal name of the class to which this symbol table belongs.
    /// </summary>
    /// <returns>the internal name of the class to which this symbol table belongs.</returns>
    internal string? GetClassName()
    {
        return className;
    }

    /// <summary>
    /// Sets the major version and the name of the class to which this symbol table belongs. Also
    /// adds the class name to the constant pool.
    /// </summary>
    /// <param name="majorVersion">a major ClassFile version number.</param>
    /// <param name="className">an internal class name.</param>
    /// <returns>the constant pool index of a new or already existing Symbol with the given class
    /// name.</returns>
    internal int SetMajorVersionAndClassName(int majorVersion, string className)
    {
        this.majorVersion = majorVersion;
        this.className = className;
        return AddConstantClass(className).Index;
    }

    /// <summary>
    /// Returns the number of items in this symbol table's constant_pool array (plus 1).
    /// </summary>
    /// <returns>the number of items in this symbol table's constant_pool array (plus 1).</returns>
    internal int GetConstantPoolCount()
    {
        return constantPoolCount;
    }

    /// <summary>
    /// Returns the length in bytes of this symbol table's constant_pool array.
    /// </summary>
    /// <returns>the length in bytes of this symbol table's constant_pool array.</returns>
    internal int GetConstantPoolLength()
    {
        return constantPool.Length;
    }

    /// <summary>
    /// Puts this symbol table's constant_pool array in the given ByteVector, preceded by the
    /// constant_pool_count value.
    /// </summary>
    /// <param name="output">where the JVMS ClassFile's constant_pool array must be put.</param>
    internal void PutConstantPool(ByteVector output)
    {
        output.PutShort(constantPoolCount).PutByteArray(constantPool.Data, 0, constantPool.Length);
    }

    /// <summary>
    /// Returns the size in bytes of this symbol table's BootstrapMethods attribute. Also adds the
    /// attribute name in the constant pool.
    /// </summary>
    /// <returns>the size in bytes of this symbol table's BootstrapMethods attribute.</returns>
    internal int ComputeBootstrapMethodsSize()
    {
        if (bootstrapMethods != null)
        {
            AddConstantUtf8("BootstrapMethods"); // TODO: Constants.BOOTSTRAP_METHODS
            return 8 + bootstrapMethods.Length;
        }
        else
        {
            return 0;
        }
    }

    /// <summary>
    /// Puts this symbol table's BootstrapMethods attribute in the given ByteVector. This includes
    /// the 6 attribute header bytes and the num_bootstrap_methods value.
    /// </summary>
    /// <param name="output">where the JVMS BootstrapMethods attribute must be put.</param>
    internal void PutBootstrapMethods(ByteVector output)
    {
        if (bootstrapMethods != null)
        {
            output
                .PutShort(AddConstantUtf8("BootstrapMethods")) // TODO: Constants.BOOTSTRAP_METHODS
                .PutInt(bootstrapMethods.Length + 2)
                .PutShort(bootstrapMethodCount)
                .PutByteArray(bootstrapMethods.Data, 0, bootstrapMethods.Length);
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Generic symbol table entries management.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the list of entries which can potentially have the given hash code.
    /// </summary>
    /// <param name="hashCode">a <see cref="Entry.HashCode"/> value.</param>
    /// <returns>the list of entries which can potentially have the given hash code. The list is
    /// stored via the <see cref="Entry.Next"/> field.</returns>
    private Entry? Get(int hashCode)
    {
        return entries[hashCode % entries.Length];
    }

    /// <summary>
    /// Puts the given entry in the <see cref="entries"/> hash set. This method does <i>not</i> check
    /// whether <see cref="entries"/> already contains a similar entry or not.
    /// <see cref="entries"/> is resized if necessary to avoid hash collisions (multiple entries
    /// needing to be stored at the same <see cref="entries"/> array index) as much as possible, with
    /// reasonable memory usage.
    /// </summary>
    /// <param name="entry">an Entry (which must not already be contained in
    /// <see cref="entries"/>).</param>
    /// <returns>the given entry</returns>
    private Entry Put(Entry entry)
    {
        if (entryCount > (entries.Length * 3) / 4)
        {
            int currentCapacity = entries.Length;
            int newCapacity = currentCapacity * 2 + 1;
            Entry[] newEntries = new Entry[newCapacity];
            for (int i = currentCapacity - 1; i >= 0; --i)
            {
                Entry? currentEntry = entries[i];
                while (currentEntry != null)
                {
                    int newCurrentEntryIndex = currentEntry.HashCode % newCapacity;
                    Entry? nextEntry = currentEntry.Next;
                    currentEntry.Next = newEntries[newCurrentEntryIndex];
                    newEntries[newCurrentEntryIndex] = currentEntry;
                    currentEntry = nextEntry;
                }
            }
            entries = newEntries;
        }
        entryCount++;
        int index = entry.HashCode % entries.Length;
        entry.Next = entries[index];
        return entries[index] = entry;
    }

    /// <summary>
    /// Adds the given entry in the <see cref="entries"/> hash set. This method does <i>not</i> check
    /// whether <see cref="entries"/> already contains a similar entry or not, and does <i>not</i>
    /// resize <see cref="entries"/> if necessary.
    /// </summary>
    /// <param name="entry">an Entry (which must not already be contained in
    /// <see cref="entries"/>).</param>
    private void Add(Entry entry)
    {
        entryCount++;
        int index = entry.HashCode % entries.Length;
        entry.Next = entries[index];
        entries[index] = entry;
    }

    // -----------------------------------------------------------------------------------------------
    // Constant pool entries management.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds a number or string constant to the constant pool of this symbol table. Does nothing if
    /// the constant pool already contains a similar item.
    /// </summary>
    /// <param name="value">the value of the constant to be added to the constant pool. This parameter
    /// must be an <c>int</c>, <c>byte</c>, <c>char</c>, <c>short</c>, <c>bool</c>,
    /// <c>float</c>, <c>long</c>, <c>double</c>, <c>string</c>, <see cref="Type"/> or Handle.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstant(object? value)
    {
        if (value is int intValue)
        {
            return AddConstantInteger(intValue);
        }
        else if (value is byte byteValue)
        {
            return AddConstantInteger(byteValue);
        }
        else if (value is char charValue)
        {
            return AddConstantInteger(charValue);
        }
        else if (value is short shortValue)
        {
            return AddConstantInteger(shortValue);
        }
        else if (value is bool boolValue)
        {
            return AddConstantInteger(boolValue ? 1 : 0);
        }
        else if (value is float floatValue)
        {
            return AddConstantFloat(floatValue);
        }
        else if (value is long longValue)
        {
            return AddConstantLong(longValue);
        }
        else if (value is double doubleValue)
        {
            return AddConstantDouble(doubleValue);
        }
        else if (value is string str)
        {
            return AddConstantString(str);
        }
        else if (value is Type type)
        {
            int typeSort = type.GetSort();
            if (typeSort == Type.OBJECT)
            {
                return AddConstantClass(type.GetInternalName());
            }
            else if (typeSort == Type.METHOD)
            {
                return AddConstantMethodType(type.GetDescriptor());
            }
            else
            {
                // type is a primitive or array type.
                return AddConstantClass(type.GetDescriptor());
            }
        }
        // TODO: Handle - uncomment when Handle is ported
        // else if (value is Handle handle)
        // {
        //     return AddConstantMethodHandle(
        //         handle.GetTag(),
        //         handle.GetOwner(),
        //         handle.GetName(),
        //         handle.GetDesc(),
        //         handle.IsInterface());
        // }
        // TODO: ConstantDynamic - uncomment when ConstantDynamic is ported
        // else if (value is ConstantDynamic constantDynamic)
        // {
        //     return AddConstantDynamic(
        //         constantDynamic.GetName(),
        //         constantDynamic.GetDescriptor(),
        //         constantDynamic.GetBootstrapMethod(),
        //         constantDynamic.GetBootstrapMethodArgumentsUnsafe());
        // }
        else
        {
            throw new ArgumentException("value " + value);
        }
    }

    /// <summary>
    /// Adds a CONSTANT_Class_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="value">the internal name of a class.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantClass(string value)
    {
        return AddConstantUtf8Reference(Symbol.CONSTANT_CLASS_TAG, value);
    }

    /// <summary>
    /// Adds a CONSTANT_Fieldref_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="owner">the internal name of a class.</param>
    /// <param name="name">a field name.</param>
    /// <param name="descriptor">a field descriptor.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantFieldref(string owner, string name, string descriptor)
    {
        return AddConstantMemberReference(Symbol.CONSTANT_FIELDREF_TAG, owner, name, descriptor);
    }

    /// <summary>
    /// Adds a CONSTANT_Methodref_info or CONSTANT_InterfaceMethodref_info to the constant pool of
    /// this symbol table. Does nothing if the constant pool already contains a similar item.
    /// </summary>
    /// <param name="owner">the internal name of a class.</param>
    /// <param name="name">a method name.</param>
    /// <param name="descriptor">a method descriptor.</param>
    /// <param name="isInterface">whether owner is an interface or not.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantMethodref(string owner, string name, string descriptor, bool isInterface)
    {
        int tag = isInterface ? Symbol.CONSTANT_INTERFACE_METHODREF_TAG : Symbol.CONSTANT_METHODREF_TAG;
        return AddConstantMemberReference(tag, owner, name, descriptor);
    }

    /// <summary>
    /// Adds a CONSTANT_Fieldref_info, CONSTANT_Methodref_info or CONSTANT_InterfaceMethodref_info to
    /// the constant pool of this symbol table. Does nothing if the constant pool already contains a
    /// similar item.
    /// </summary>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_FIELDREF_TAG"/>,
    /// <see cref="Symbol.CONSTANT_METHODREF_TAG"/> or
    /// <see cref="Symbol.CONSTANT_INTERFACE_METHODREF_TAG"/>.</param>
    /// <param name="owner">the internal name of a class.</param>
    /// <param name="name">a field or method name.</param>
    /// <param name="descriptor">a field or method descriptor.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    private Entry AddConstantMemberReference(int tag, string owner, string name, string descriptor)
    {
        int hashCode = Hash(tag, owner, name, descriptor);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == tag
                && entry.HashCode == hashCode
                && entry.Owner!.Equals(owner)
                && entry.Name!.Equals(name)
                && entry.Value!.Equals(descriptor))
            {
                return entry;
            }
            entry = entry.Next;
        }
        constantPool.Put122(
            tag, AddConstantClass(owner).Index, AddConstantNameAndType(name, descriptor));
        return Put(new Entry(constantPoolCount++, tag, owner, name, descriptor, 0, hashCode));
    }

    /// <summary>
    /// Adds a new CONSTANT_Fieldref_info, CONSTANT_Methodref_info or
    /// CONSTANT_InterfaceMethodref_info to the constant pool of this symbol table.
    /// </summary>
    /// <param name="index">the constant pool index of the new Symbol.</param>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_FIELDREF_TAG"/>,
    /// <see cref="Symbol.CONSTANT_METHODREF_TAG"/> or
    /// <see cref="Symbol.CONSTANT_INTERFACE_METHODREF_TAG"/>.</param>
    /// <param name="owner">the internal name of a class.</param>
    /// <param name="name">a field or method name.</param>
    /// <param name="descriptor">a field or method descriptor.</param>
    private void AddConstantMemberReference(int index, int tag, string owner, string name, string descriptor)
    {
        Add(new Entry(index, tag, owner, name, descriptor, 0, Hash(tag, owner, name, descriptor)));
    }

    /// <summary>
    /// Adds a CONSTANT_String_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="value">a string.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantString(string value)
    {
        return AddConstantUtf8Reference(Symbol.CONSTANT_STRING_TAG, value);
    }

    /// <summary>
    /// Adds a CONSTANT_Integer_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="value">an int.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantInteger(int value)
    {
        return AddConstantIntegerOrFloat(Symbol.CONSTANT_INTEGER_TAG, value);
    }

    /// <summary>
    /// Adds a CONSTANT_Float_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="value">a float.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantFloat(float value)
    {
        return AddConstantIntegerOrFloat(Symbol.CONSTANT_FLOAT_TAG, BitConverter.SingleToInt32Bits(value));
    }

    /// <summary>
    /// Adds a CONSTANT_Integer_info or CONSTANT_Float_info to the constant pool of this symbol
    /// table. Does nothing if the constant pool already contains a similar item.
    /// </summary>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_INTEGER_TAG"/> or
    /// <see cref="Symbol.CONSTANT_FLOAT_TAG"/>.</param>
    /// <param name="value">an int or float.</param>
    /// <returns>a constant pool constant with the given tag and primitive values.</returns>
    private Symbol AddConstantIntegerOrFloat(int tag, int value)
    {
        int hashCode = Hash(tag, value);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == tag && entry.HashCode == hashCode && entry.Data == value)
            {
                return entry;
            }
            entry = entry.Next;
        }
        constantPool.PutByte(tag).PutInt(value);
        return Put(new Entry(constantPoolCount++, tag, value, hashCode));
    }

    /// <summary>
    /// Adds a new CONSTANT_Integer_info or CONSTANT_Float_info to the constant pool of this symbol
    /// table.
    /// </summary>
    /// <param name="index">the constant pool index of the new Symbol.</param>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_INTEGER_TAG"/> or
    /// <see cref="Symbol.CONSTANT_FLOAT_TAG"/>.</param>
    /// <param name="value">an int or float.</param>
    private void AddConstantIntegerOrFloat(int index, int tag, int value)
    {
        Add(new Entry(index, tag, value, Hash(tag, value)));
    }

    /// <summary>
    /// Adds a CONSTANT_Long_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="value">a long.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantLong(long value)
    {
        return AddConstantLongOrDouble(Symbol.CONSTANT_LONG_TAG, value);
    }

    /// <summary>
    /// Adds a CONSTANT_Double_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="value">a double.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantDouble(double value)
    {
        return AddConstantLongOrDouble(Symbol.CONSTANT_DOUBLE_TAG, BitConverter.DoubleToInt64Bits(value));
    }

    /// <summary>
    /// Adds a CONSTANT_Long_info or CONSTANT_Double_info to the constant pool of this symbol table.
    /// Does nothing if the constant pool already contains a similar item.
    /// </summary>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_LONG_TAG"/> or
    /// <see cref="Symbol.CONSTANT_DOUBLE_TAG"/>.</param>
    /// <param name="value">a long or double.</param>
    /// <returns>a constant pool constant with the given tag and primitive values.</returns>
    private Symbol AddConstantLongOrDouble(int tag, long value)
    {
        int hashCode = Hash(tag, value);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == tag && entry.HashCode == hashCode && entry.Data == value)
            {
                return entry;
            }
            entry = entry.Next;
        }
        int index = constantPoolCount;
        constantPool.PutByte(tag).PutLong(value);
        constantPoolCount += 2;
        return Put(new Entry(index, tag, value, hashCode));
    }

    /// <summary>
    /// Adds a new CONSTANT_Long_info or CONSTANT_Double_info to the constant pool of this symbol
    /// table.
    /// </summary>
    /// <param name="index">the constant pool index of the new Symbol.</param>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_LONG_TAG"/> or
    /// <see cref="Symbol.CONSTANT_DOUBLE_TAG"/>.</param>
    /// <param name="value">a long or double.</param>
    private void AddConstantLongOrDouble(int index, int tag, long value)
    {
        Add(new Entry(index, tag, value, Hash(tag, value)));
    }

    /// <summary>
    /// Adds a CONSTANT_NameAndType_info to the constant pool of this symbol table. Does nothing if
    /// the constant pool already contains a similar item.
    /// </summary>
    /// <param name="name">a field or method name.</param>
    /// <param name="descriptor">a field or method descriptor.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal int AddConstantNameAndType(string name, string descriptor)
    {
        int tag = Symbol.CONSTANT_NAME_AND_TYPE_TAG;
        int hashCode = Hash(tag, name, descriptor);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == tag
                && entry.HashCode == hashCode
                && entry.Name!.Equals(name)
                && entry.Value!.Equals(descriptor))
            {
                return entry.Index;
            }
            entry = entry.Next;
        }
        constantPool.Put122(tag, AddConstantUtf8(name), AddConstantUtf8(descriptor));
        return Put(new Entry(constantPoolCount++, tag, name, descriptor, hashCode)).Index;
    }

    /// <summary>
    /// Adds a new CONSTANT_NameAndType_info to the constant pool of this symbol table.
    /// </summary>
    /// <param name="index">the constant pool index of the new Symbol.</param>
    /// <param name="name">a field or method name.</param>
    /// <param name="descriptor">a field or method descriptor.</param>
    private void AddConstantNameAndType(int index, string name, string descriptor)
    {
        int tag = Symbol.CONSTANT_NAME_AND_TYPE_TAG;
        Add(new Entry(index, tag, name, descriptor, Hash(tag, name, descriptor)));
    }

    /// <summary>
    /// Adds a CONSTANT_Utf8_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="value">a string.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal int AddConstantUtf8(string value)
    {
        int hashCode = Hash(Symbol.CONSTANT_UTF8_TAG, value);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == Symbol.CONSTANT_UTF8_TAG
                && entry.HashCode == hashCode
                && entry.Value!.Equals(value))
            {
                return entry.Index;
            }
            entry = entry.Next;
        }
        constantPool.PutByte(Symbol.CONSTANT_UTF8_TAG).PutUTF8(value);
        return Put(new Entry(constantPoolCount++, Symbol.CONSTANT_UTF8_TAG, value, hashCode)).Index;
    }

    /// <summary>
    /// Adds a new CONSTANT_String_info to the constant pool of this symbol table.
    /// </summary>
    /// <param name="index">the constant pool index of the new Symbol.</param>
    /// <param name="value">a string.</param>
    private void AddConstantUtf8(int index, string value)
    {
        Add(new Entry(index, Symbol.CONSTANT_UTF8_TAG, value, Hash(Symbol.CONSTANT_UTF8_TAG, value)));
    }

    /// <summary>
    /// Adds a CONSTANT_MethodHandle_info to the constant pool of this symbol table. Does nothing if
    /// the constant pool already contains a similar item.
    /// </summary>
    /// <param name="referenceKind">one of <see cref="Opcodes.H_GETFIELD"/>,
    /// <see cref="Opcodes.H_GETSTATIC"/>, <see cref="Opcodes.H_PUTFIELD"/>,
    /// <see cref="Opcodes.H_PUTSTATIC"/>, <see cref="Opcodes.H_INVOKEVIRTUAL"/>,
    /// <see cref="Opcodes.H_INVOKESTATIC"/>, <see cref="Opcodes.H_INVOKESPECIAL"/>,
    /// <see cref="Opcodes.H_NEWINVOKESPECIAL"/> or <see cref="Opcodes.H_INVOKEINTERFACE"/>.</param>
    /// <param name="owner">the internal name of a class of interface.</param>
    /// <param name="name">a field or method name.</param>
    /// <param name="descriptor">a field or method descriptor.</param>
    /// <param name="isInterface">whether owner is an interface or not.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantMethodHandle(int referenceKind, string owner, string name, string descriptor, bool isInterface)
    {
        int tag = Symbol.CONSTANT_METHOD_HANDLE_TAG;
        int data = GetConstantMethodHandleSymbolData(referenceKind, isInterface);
        // Note that we don't need to include isInterface in the hash computation, because it is
        // redundant with owner (we can't have the same owner with different isInterface values).
        int hashCode = Hash(tag, owner, name, descriptor, data);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == tag
                && entry.HashCode == hashCode
                && entry.Data == data
                && entry.Owner!.Equals(owner)
                && entry.Name!.Equals(name)
                && entry.Value!.Equals(descriptor))
            {
                return entry;
            }
            entry = entry.Next;
        }
        if (referenceKind <= Opcodes.H_PUTSTATIC)
        {
            constantPool.Put112(tag, referenceKind, AddConstantFieldref(owner, name, descriptor).Index);
        }
        else
        {
            constantPool.Put112(tag, referenceKind, AddConstantMethodref(owner, name, descriptor, isInterface).Index);
        }
        return Put(new Entry(constantPoolCount++, tag, owner, name, descriptor, data, hashCode));
    }

    /// <summary>
    /// Adds a new CONSTANT_MethodHandle_info to the constant pool of this symbol table.
    /// </summary>
    /// <param name="index">the constant pool index of the new Symbol.</param>
    /// <param name="referenceKind">one of <see cref="Opcodes.H_GETFIELD"/>,
    /// <see cref="Opcodes.H_GETSTATIC"/>, <see cref="Opcodes.H_PUTFIELD"/>,
    /// <see cref="Opcodes.H_PUTSTATIC"/>, <see cref="Opcodes.H_INVOKEVIRTUAL"/>,
    /// <see cref="Opcodes.H_INVOKESTATIC"/>, <see cref="Opcodes.H_INVOKESPECIAL"/>,
    /// <see cref="Opcodes.H_NEWINVOKESPECIAL"/> or <see cref="Opcodes.H_INVOKEINTERFACE"/>.</param>
    /// <param name="owner">the internal name of a class of interface.</param>
    /// <param name="name">a field or method name.</param>
    /// <param name="descriptor">a field or method descriptor.</param>
    /// <param name="isInterface">whether owner is an interface or not.</param>
    private void AddConstantMethodHandle(int index, int referenceKind, string owner, string name, string descriptor, bool isInterface)
    {
        int tag = Symbol.CONSTANT_METHOD_HANDLE_TAG;
        int data = GetConstantMethodHandleSymbolData(referenceKind, isInterface);
        int hashCode = Hash(tag, owner, name, descriptor, data);
        Add(new Entry(index, tag, owner, name, descriptor, data, hashCode));
    }

    /// <summary>
    /// Returns the <see cref="Symbol.Data"/> field for a CONSTANT_MethodHandle_info Symbol.
    /// </summary>
    /// <param name="referenceKind">one of <see cref="Opcodes.H_GETFIELD"/>,
    /// <see cref="Opcodes.H_GETSTATIC"/>, <see cref="Opcodes.H_PUTFIELD"/>,
    /// <see cref="Opcodes.H_PUTSTATIC"/>, <see cref="Opcodes.H_INVOKEVIRTUAL"/>,
    /// <see cref="Opcodes.H_INVOKESTATIC"/>, <see cref="Opcodes.H_INVOKESPECIAL"/>,
    /// <see cref="Opcodes.H_NEWINVOKESPECIAL"/> or <see cref="Opcodes.H_INVOKEINTERFACE"/>.</param>
    /// <param name="isInterface">whether owner is an interface or not.</param>
    private static int GetConstantMethodHandleSymbolData(int referenceKind, bool isInterface)
    {
        if (referenceKind > Opcodes.H_PUTSTATIC && isInterface)
        {
            return referenceKind << 8;
        }
        return referenceKind;
    }

    /// <summary>
    /// Adds a CONSTANT_MethodType_info to the constant pool of this symbol table. Does nothing if
    /// the constant pool already contains a similar item.
    /// </summary>
    /// <param name="methodDescriptor">a method descriptor.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantMethodType(string methodDescriptor)
    {
        return AddConstantUtf8Reference(Symbol.CONSTANT_METHOD_TYPE_TAG, methodDescriptor);
    }

    /// <summary>
    /// Adds a CONSTANT_Dynamic_info to the constant pool of this symbol table. Also adds the
    /// related bootstrap method to the BootstrapMethods of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="name">a method name.</param>
    /// <param name="descriptor">a field descriptor.</param>
    /// <param name="bootstrapMethodHandle">a bootstrap method handle.</param>
    /// <param name="bootstrapMethodArguments">the bootstrap method arguments.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantDynamic(string name, string descriptor, object? bootstrapMethodHandle, params object?[] bootstrapMethodArguments) // TODO: Handle, ConstantDynamic
    {
        Symbol bootstrapMethod = AddBootstrapMethod(bootstrapMethodHandle, bootstrapMethodArguments);
        return AddConstantDynamicOrInvokeDynamicReference(
            Symbol.CONSTANT_DYNAMIC_TAG, name, descriptor, bootstrapMethod.Index);
    }

    /// <summary>
    /// Adds a CONSTANT_InvokeDynamic_info to the constant pool of this symbol table. Also adds the
    /// related bootstrap method to the BootstrapMethods of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="name">a method name.</param>
    /// <param name="descriptor">a method descriptor.</param>
    /// <param name="bootstrapMethodHandle">a bootstrap method handle.</param>
    /// <param name="bootstrapMethodArguments">the bootstrap method arguments.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantInvokeDynamic(string name, string descriptor, object? bootstrapMethodHandle, params object?[] bootstrapMethodArguments) // TODO: Handle
    {
        Symbol bootstrapMethod = AddBootstrapMethod(bootstrapMethodHandle, bootstrapMethodArguments);
        return AddConstantDynamicOrInvokeDynamicReference(
            Symbol.CONSTANT_INVOKE_DYNAMIC_TAG, name, descriptor, bootstrapMethod.Index);
    }

    /// <summary>
    /// Adds a CONSTANT_Dynamic or a CONSTANT_InvokeDynamic_info to the constant pool of this symbol
    /// table. Does nothing if the constant pool already contains a similar item.
    /// </summary>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_DYNAMIC_TAG"/> or
    /// <see cref="Symbol.CONSTANT_INVOKE_DYNAMIC_TAG"/>.</param>
    /// <param name="name">a method name.</param>
    /// <param name="descriptor">a field descriptor for CONSTANT_DYNAMIC_TAG) or a method descriptor
    /// for CONSTANT_INVOKE_DYNAMIC_TAG.</param>
    /// <param name="bootstrapMethodIndex">the index of a bootstrap method in the BootstrapMethods
    /// attribute.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    private Symbol AddConstantDynamicOrInvokeDynamicReference(int tag, string name, string descriptor, int bootstrapMethodIndex)
    {
        int hashCode = Hash(tag, name, descriptor, bootstrapMethodIndex);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == tag
                && entry.HashCode == hashCode
                && entry.Data == bootstrapMethodIndex
                && entry.Name!.Equals(name)
                && entry.Value!.Equals(descriptor))
            {
                return entry;
            }
            entry = entry.Next;
        }
        constantPool.Put122(tag, bootstrapMethodIndex, AddConstantNameAndType(name, descriptor));
        return Put(
            new Entry(constantPoolCount++, tag, null, name, descriptor, bootstrapMethodIndex, hashCode));
    }

    /// <summary>
    /// Adds a new CONSTANT_Dynamic_info or CONSTANT_InvokeDynamic_info to the constant pool of this
    /// symbol table.
    /// </summary>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_DYNAMIC_TAG"/> or
    /// <see cref="Symbol.CONSTANT_INVOKE_DYNAMIC_TAG"/>.</param>
    /// <param name="index">the constant pool index of the new Symbol.</param>
    /// <param name="name">a method name.</param>
    /// <param name="descriptor">a field descriptor for CONSTANT_DYNAMIC_TAG or a method descriptor
    /// for CONSTANT_INVOKE_DYNAMIC_TAG.</param>
    /// <param name="bootstrapMethodIndex">the index of a bootstrap method in the BootstrapMethods
    /// attribute.</param>
    private void AddConstantDynamicOrInvokeDynamicReference(int tag, int index, string name, string descriptor, int bootstrapMethodIndex)
    {
        int hashCode = Hash(tag, name, descriptor, bootstrapMethodIndex);
        Add(new Entry(index, tag, null, name, descriptor, bootstrapMethodIndex, hashCode));
    }

    /// <summary>
    /// Adds a CONSTANT_Module_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="moduleName">a fully qualified name (using dots) of a module.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantModule(string moduleName)
    {
        return AddConstantUtf8Reference(Symbol.CONSTANT_MODULE_TAG, moduleName);
    }

    /// <summary>
    /// Adds a CONSTANT_Package_info to the constant pool of this symbol table. Does nothing if the
    /// constant pool already contains a similar item.
    /// </summary>
    /// <param name="packageName">the internal name of a package.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddConstantPackage(string packageName)
    {
        return AddConstantUtf8Reference(Symbol.CONSTANT_PACKAGE_TAG, packageName);
    }

    /// <summary>
    /// Adds a CONSTANT_Class_info, CONSTANT_String_info, CONSTANT_MethodType_info,
    /// CONSTANT_Module_info or CONSTANT_Package_info to the constant pool of this symbol table. Does
    /// nothing if the constant pool already contains a similar item.
    /// </summary>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_CLASS_TAG"/>,
    /// <see cref="Symbol.CONSTANT_STRING_TAG"/>, <see cref="Symbol.CONSTANT_METHOD_TYPE_TAG"/>,
    /// <see cref="Symbol.CONSTANT_MODULE_TAG"/> or <see cref="Symbol.CONSTANT_PACKAGE_TAG"/>.</param>
    /// <param name="value">an internal class name, an arbitrary string, a method descriptor, a module
    /// or a package name, depending on tag.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    private Symbol AddConstantUtf8Reference(int tag, string value)
    {
        int hashCode = Hash(tag, value);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == tag && entry.HashCode == hashCode && entry.Value!.Equals(value))
            {
                return entry;
            }
            entry = entry.Next;
        }
        constantPool.Put12(tag, AddConstantUtf8(value));
        return Put(new Entry(constantPoolCount++, tag, value, hashCode));
    }

    /// <summary>
    /// Adds a new CONSTANT_Class_info, CONSTANT_String_info, CONSTANT_MethodType_info,
    /// CONSTANT_Module_info or CONSTANT_Package_info to the constant pool of this symbol table.
    /// </summary>
    /// <param name="index">the constant pool index of the new Symbol.</param>
    /// <param name="tag">one of <see cref="Symbol.CONSTANT_CLASS_TAG"/>,
    /// <see cref="Symbol.CONSTANT_STRING_TAG"/>, <see cref="Symbol.CONSTANT_METHOD_TYPE_TAG"/>,
    /// <see cref="Symbol.CONSTANT_MODULE_TAG"/> or <see cref="Symbol.CONSTANT_PACKAGE_TAG"/>.</param>
    /// <param name="value">an internal class name, an arbitrary string, a method descriptor, a module
    /// or a package name, depending on tag.</param>
    private void AddConstantUtf8Reference(int index, int tag, string value)
    {
        Add(new Entry(index, tag, value, Hash(tag, value)));
    }

    // -----------------------------------------------------------------------------------------------
    // Bootstrap method entries management.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds a bootstrap method to the BootstrapMethods attribute of this symbol table. Does nothing
    /// if the BootstrapMethods already contains a similar bootstrap method.
    /// </summary>
    /// <param name="bootstrapMethodHandle">a bootstrap method handle.</param>
    /// <param name="bootstrapMethodArguments">the bootstrap method arguments.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    internal Symbol AddBootstrapMethod(object? bootstrapMethodHandle, params object?[] bootstrapMethodArguments) // TODO: Handle
    {
        ByteVector bootstrapMethodsAttribute = bootstrapMethods!;
        if (bootstrapMethodsAttribute == null)
        {
            bootstrapMethodsAttribute = bootstrapMethods = new ByteVector();
        }

        // The bootstrap method arguments can be Constant_Dynamic values, which reference other
        // bootstrap methods. We must therefore add the bootstrap method arguments to the constant
        // pool and BootstrapMethods attribute first, so that the BootstrapMethods attribute is not
        // modified while adding the given bootstrap method to it, in the rest of this method.
        int numBootstrapArguments = bootstrapMethodArguments.Length;
        int[] bootstrapMethodArgumentIndexes = new int[numBootstrapArguments];
        for (int i = 0; i < numBootstrapArguments; i++)
        {
            bootstrapMethodArgumentIndexes[i] = AddConstant(bootstrapMethodArguments[i]).Index;
        }

        // Write the bootstrap method in the BootstrapMethods table. This is necessary to be able to
        // compare it with existing ones, and will be reverted below if there is already a similar
        // bootstrap method.
        int bootstrapMethodOffset = bootstrapMethodsAttribute.Length;
        bootstrapMethodsAttribute.PutShort(
            AddConstantMethodHandle(
                ((Handle)bootstrapMethodHandle!).GetTag(),
                ((Handle)bootstrapMethodHandle!).GetOwner(),
                ((Handle)bootstrapMethodHandle!).GetName(),
                ((Handle)bootstrapMethodHandle!).GetDesc(),
                ((Handle)bootstrapMethodHandle!).IsInterface())
                .Index);

        bootstrapMethodsAttribute.PutShort(numBootstrapArguments);
        for (int i = 0; i < numBootstrapArguments; i++)
        {
            bootstrapMethodsAttribute.PutShort(bootstrapMethodArgumentIndexes[i]);
        }

        // Compute the length and the hash code of the bootstrap method.
        int bootstrapMethodlength = bootstrapMethodsAttribute.Length - bootstrapMethodOffset;
        int hashCode = bootstrapMethodHandle!.GetHashCode();
        foreach (object? bootstrapMethodArgument in bootstrapMethodArguments)
        {
            hashCode ^= bootstrapMethodArgument!.GetHashCode();
        }
        hashCode &= 0x7FFFFFFF;

        // Add the bootstrap method to the symbol table or revert the above changes.
        return AddBootstrapMethod(bootstrapMethodOffset, bootstrapMethodlength, hashCode);
    }

    /// <summary>
    /// Adds a bootstrap method to the BootstrapMethods attribute of this symbol table. Does nothing
    /// if the BootstrapMethods already contains a similar bootstrap method (more precisely, reverts
    /// the content of <see cref="bootstrapMethods"/> to remove the last, duplicate bootstrap
    /// method).
    /// </summary>
    /// <param name="offset">the offset of the last bootstrap method in
    /// <see cref="bootstrapMethods"/>, in bytes.</param>
    /// <param name="length">the length of this bootstrap method in
    /// <see cref="bootstrapMethods"/>, in bytes.</param>
    /// <param name="hashCode">the hash code of this bootstrap method.</param>
    /// <returns>a new or already existing Symbol with the given value.</returns>
    private Symbol AddBootstrapMethod(int offset, int length, int hashCode)
    {
        byte[] bootstrapMethodsData = bootstrapMethods!.Data;
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == Symbol.BOOTSTRAP_METHOD_TAG && entry.HashCode == hashCode)
            {
                int otherOffset = (int)entry.Data;
                bool isSameBootstrapMethod = true;
                for (int i = 0; i < length; ++i)
                {
                    if (bootstrapMethodsData[offset + i] != bootstrapMethodsData[otherOffset + i])
                    {
                        isSameBootstrapMethod = false;
                        break;
                    }
                }
                if (isSameBootstrapMethod)
                {
                    bootstrapMethods.Length = offset; // Revert to old position.
                    return entry;
                }
            }
            entry = entry.Next;
        }
        return Put(new Entry(bootstrapMethodCount++, Symbol.BOOTSTRAP_METHOD_TAG, offset, hashCode));
    }

    // -----------------------------------------------------------------------------------------------
    // Type table entries management.
    // -----------------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the type table element whose index is given.
    /// </summary>
    /// <param name="typeIndex">a type table index.</param>
    /// <returns>the type table element whose index is given.</returns>
    internal Symbol GetType(int typeIndex)
    {
        return typeTable![typeIndex];
    }

    /// <summary>
    /// Returns the label corresponding to the "forward uninitialized" type table element whose
    /// index is given.
    /// </summary>
    /// <param name="typeIndex">the type table index of a "forward uninitialized" type table
    /// element.</param>
    /// <returns>the label corresponding of the NEW instruction which created this "forward
    /// uninitialized" type.</returns>
    internal Label GetForwardUninitializedLabel(int typeIndex)
    {
        return labelTable![(int)typeTable![typeIndex].Data].Label;
    }

    /// <summary>
    /// Adds a type in the type table of this symbol table. Does nothing if the type table already
    /// contains a similar type.
    /// </summary>
    /// <param name="value">an internal class name.</param>
    /// <returns>the index of a new or already existing type Symbol with the given value.</returns>
    internal int AddType(string value)
    {
        int hashCode = Hash(Symbol.TYPE_TAG, value);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == Symbol.TYPE_TAG && entry.HashCode == hashCode && entry.Value!.Equals(value))
            {
                return entry.Index;
            }
            entry = entry.Next;
        }
        return AddTypeInternal(new Entry(typeCount, Symbol.TYPE_TAG, value, hashCode));
    }

    /// <summary>
    /// Adds an uninitialized type in the type table of this symbol table. Does nothing if the type
    /// table already contains a similar type.
    /// </summary>
    /// <param name="value">an internal class name.</param>
    /// <param name="bytecodeOffset">the bytecode offset of the NEW instruction that created this
    /// uninitialized type value.</param>
    /// <returns>the index of a new or already existing type Symbol with the given value.</returns>
    internal int AddUninitializedType(string value, int bytecodeOffset)
    {
        int hashCode = Hash(Symbol.UNINITIALIZED_TYPE_TAG, value, bytecodeOffset);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == Symbol.UNINITIALIZED_TYPE_TAG
                && entry.HashCode == hashCode
                && entry.Data == bytecodeOffset
                && entry.Value!.Equals(value))
            {
                return entry.Index;
            }
            entry = entry.Next;
        }
        return AddTypeInternal(
            new Entry(typeCount, Symbol.UNINITIALIZED_TYPE_TAG, value, bytecodeOffset, hashCode));
    }

    /// <summary>
    /// Adds a "forward uninitialized" type in the type table of this symbol table. Does nothing if
    /// the type table already contains a similar type.
    /// </summary>
    /// <param name="value">an internal class name.</param>
    /// <param name="label">the label of the NEW instruction that created this uninitialized type
    /// value. If the label is resolved, use the <see cref="AddUninitializedType"/> method
    /// instead.</param>
    /// <returns>the index of a new or already existing type <see cref="Symbol"/> with the given
    /// value.</returns>
    internal int AddForwardUninitializedType(string value, Label label)
    {
        int labelIndex = GetOrAddLabelEntry(label).Index;
        int hashCode = Hash(Symbol.FORWARD_UNINITIALIZED_TYPE_TAG, value, labelIndex);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == Symbol.FORWARD_UNINITIALIZED_TYPE_TAG
                && entry.HashCode == hashCode
                && entry.Data == labelIndex
                && entry.Value!.Equals(value))
            {
                return entry.Index;
            }
            entry = entry.Next;
        }
        return AddTypeInternal(
            new Entry(typeCount, Symbol.FORWARD_UNINITIALIZED_TYPE_TAG, value, labelIndex, hashCode));
    }

    /// <summary>
    /// Adds a merged type in the type table of this symbol table. Does nothing if the type table
    /// already contains a similar type.
    /// </summary>
    /// <param name="typeTableIndex1">a <see cref="Symbol.TYPE_TAG"/> type, specified by its index in
    /// the type table.</param>
    /// <param name="typeTableIndex2">another <see cref="Symbol.TYPE_TAG"/> type, specified by its
    /// index in the type table.</param>
    /// <returns>the index of a new or already existing <see cref="Symbol.TYPE_TAG"/> type Symbol,
    /// corresponding to the common super class of the given types.</returns>
    internal int AddMergedType(int typeTableIndex1, int typeTableIndex2)
    {
        long data =
            typeTableIndex1 < typeTableIndex2
                ? typeTableIndex1 | (((long)typeTableIndex2) << 32)
                : typeTableIndex2 | (((long)typeTableIndex1) << 32);
        int hashCode = Hash(Symbol.MERGED_TYPE_TAG, typeTableIndex1 + typeTableIndex2);
        Entry? entry = Get(hashCode);
        while (entry != null)
        {
            if (entry.Tag == Symbol.MERGED_TYPE_TAG && entry.HashCode == hashCode && entry.Data == data)
            {
                return entry.Info;
            }
            entry = entry.Next;
        }
        string type1 = typeTable![typeTableIndex1].Value!;
        string type2 = typeTable[typeTableIndex2].Value!;
        // TODO: ClassWriter - uncomment when ClassWriter is ported
        // int commonSuperTypeIndex = AddType(((ClassWriter)ClassWriter!).GetCommonSuperClass(type1, type2));
        // Put(new Entry(typeCount, Symbol.MERGED_TYPE_TAG, data, hashCode)).Info = commonSuperTypeIndex;
        // return commonSuperTypeIndex;
        throw new NotImplementedException("// TODO: ClassWriter.GetCommonSuperClass not ported");
    }

    /// <summary>
    /// Adds the given type Symbol to <see cref="typeTable"/>.
    /// </summary>
    /// <param name="entry">a <see cref="Symbol.TYPE_TAG"/> or
    /// <see cref="Symbol.UNINITIALIZED_TYPE_TAG"/> type symbol. The index of this Symbol must be
    /// equal to the current value of <see cref="typeCount"/>.</param>
    /// <returns>the index in <see cref="typeTable"/> where the given type was added, which is also
    /// equal to entry's index by hypothesis.</returns>
    private int AddTypeInternal(Entry entry)
    {
        if (typeTable == null)
        {
            typeTable = new Entry[16];
        }
        if (typeCount == typeTable.Length)
        {
            Entry[] newTypeTable = new Entry[2 * typeTable.Length];
            Array.Copy(typeTable, 0, newTypeTable, 0, typeTable.Length);
            typeTable = newTypeTable;
        }
        typeTable[typeCount++] = entry;
        return Put(entry).Index;
    }

    /// <summary>
    /// Returns the <see cref="LabelEntry"/> corresponding to the given label. Creates a new one if
    /// there is no such entry.
    /// </summary>
    /// <param name="label">the <see cref="Label"/> of a NEW instruction which created an uninitialized
    /// type, in the case where this NEW instruction is after the &lt;init&gt; constructor call (in
    /// bytecode offset order). See <see cref="Symbol.FORWARD_UNINITIALIZED_TYPE_TAG"/>.</param>
    /// <returns>the <see cref="LabelEntry"/> corresponding to <paramref name="label"/>.</returns>
    private LabelEntry GetOrAddLabelEntry(Label label)
    {
        if (labelEntries == null)
        {
            labelEntries = new LabelEntry[16];
            labelTable = new LabelEntry[16];
        }
        int hashCode = RuntimeHelpers.GetHashCode(label);
        LabelEntry? labelEntry = labelEntries[hashCode % labelEntries.Length];
        while (labelEntry != null && labelEntry.Label != label)
        {
            labelEntry = labelEntry.Next;
        }
        if (labelEntry != null)
        {
            return labelEntry;
        }

        if (labelCount > (labelEntries.Length * 3) / 4)
        {
            int currentCapacity = labelEntries.Length;
            int newCapacity = currentCapacity * 2 + 1;
            LabelEntry[] newLabelEntries = new LabelEntry[newCapacity];
            for (int i = currentCapacity - 1; i >= 0; --i)
            {
                LabelEntry? currentEntry = labelEntries[i];
                while (currentEntry != null)
                {
                    int newCurrentEntryIndex = RuntimeHelpers.GetHashCode(currentEntry.Label) % newCapacity;
                    LabelEntry? nextEntry = currentEntry.Next;
                    currentEntry.Next = newLabelEntries[newCurrentEntryIndex];
                    newLabelEntries[newCurrentEntryIndex] = currentEntry;
                    currentEntry = nextEntry;
                }
            }
            labelEntries = newLabelEntries;
        }
        if (labelCount == labelTable!.Length)
        {
            LabelEntry[] newLabelTable = new LabelEntry[2 * labelTable.Length];
            Array.Copy(labelTable, 0, newLabelTable, 0, labelTable.Length);
            labelTable = newLabelTable;
        }

        labelEntry = new LabelEntry(labelCount, label);
        int index = hashCode % labelEntries.Length;
        labelEntry.Next = labelEntries[index];
        labelEntries[index] = labelEntry;
        labelTable[labelCount++] = labelEntry;
        return labelEntry;
    }

    // -----------------------------------------------------------------------------------------------
    // Static helper methods to compute hash codes.
    // -----------------------------------------------------------------------------------------------

    private static int Hash(int tag, int value)
    {
        return 0x7FFFFFFF & (tag + value);
    }

    private static int Hash(int tag, long value)
    {
        return 0x7FFFFFFF & (tag + (int)value + (int)((ulong)value >> 32));
    }

    private static int Hash(int tag, string value)
    {
        return 0x7FFFFFFF & (tag + value.GetHashCode());
    }

    private static int Hash(int tag, string value1, int value2)
    {
        return 0x7FFFFFFF & (tag + value1.GetHashCode() + value2);
    }

    private static int Hash(int tag, string value1, string value2)
    {
        return 0x7FFFFFFF & (tag + value1.GetHashCode() * value2.GetHashCode());
    }

    private static int Hash(int tag, string value1, string value2, int value3)
    {
        return 0x7FFFFFFF & (tag + value1.GetHashCode() * value2.GetHashCode() * (value3 + 1));
    }

    private static int Hash(int tag, string value1, string value2, string value3)
    {
        return 0x7FFFFFFF & (tag + value1.GetHashCode() * value2.GetHashCode() * value3.GetHashCode());
    }

    private static int Hash(int tag, string value1, string value2, string value3, int value4)
    {
        return 0x7FFFFFFF & (tag + value1.GetHashCode() * value2.GetHashCode() * value3.GetHashCode() * value4);
    }

    /// <summary>
    /// An entry of a SymbolTable. This concrete and private subclass of <see cref="Symbol"/> adds
    /// two fields which are only used inside SymbolTable, to implement hash sets of symbols (in
    /// order to avoid duplicate symbols). See <see cref="entries"/>.
    /// </summary>
    private sealed class Entry : Symbol
    {
        /// <summary>The hash code of this entry.</summary>
        internal readonly int HashCode;

        /// <summary>
        /// Another entry (and so on recursively) having the same hash code (modulo the size of
        /// <see cref="SymbolTable.entries"/>) as this one.
        /// </summary>
        internal Entry? Next;

        internal Entry(int index, int tag, string? owner, string? name, string? value, long data, int hashCode)
            : base(index, tag, owner, name, value, data)
        {
            HashCode = hashCode;
        }

        internal Entry(int index, int tag, string? value, int hashCode)
            : base(index, tag, null, null, value, 0)
        {
            HashCode = hashCode;
        }

        internal Entry(int index, int tag, string? value, long data, int hashCode)
            : base(index, tag, null, null, value, data)
        {
            HashCode = hashCode;
        }

        internal Entry(int index, int tag, string? name, string? value, int hashCode)
            : base(index, tag, null, name, value, 0)
        {
            HashCode = hashCode;
        }

        internal Entry(int index, int tag, long data, int hashCode)
            : base(index, tag, null, null, null, data)
        {
            HashCode = hashCode;
        }
    }

    /// <summary>
    /// A label corresponding to a "forward uninitialized" type in the ASM specific
    /// <see cref="SymbolTable.typeTable"/> (see
    /// <see cref="Symbol.FORWARD_UNINITIALIZED_TYPE_TAG"/>).
    /// </summary>
    private sealed class LabelEntry
    {
        /// <summary>The index of this label entry in the <see cref="SymbolTable.labelTable"/>
        /// array.</summary>
        internal readonly int Index;

        /// <summary>The value of this label entry.</summary>
        internal readonly Label Label;

        /// <summary>
        /// Another entry (and so on recursively) having the same hash code (modulo the size of
        /// <see cref="SymbolTable.labelEntries"/>) as this one.
        /// </summary>
        internal LabelEntry? Next;

        internal LabelEntry(int index, Label label)
        {
            Index = index;
            Label = label;
        }
    }
}
