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
/// A dynamically extensible vector of bytes. Roughly equivalent to a
/// <c>BinaryWriter</c> on top of a <c>MemoryStream</c>, but more efficient.
/// </summary>
public class ByteVector
{
    /// <summary>The content buffer. Only the first <see cref="Length"/> bytes are valid.</summary>
    internal byte[] Data;

    /// <summary>The actual number of bytes in this vector.</summary>
    internal int Length;

    /// <summary>Constructs a new <see cref="ByteVector"/> with default capacity (64).</summary>
    public ByteVector()
    {
        Data = new byte[64];
    }

    /// <summary>Constructs a new <see cref="ByteVector"/> with the given initial capacity.</summary>
    public ByteVector(int initialCapacity)
    {
        Data = new byte[initialCapacity];
    }

    /// <summary>Constructs a new <see cref="ByteVector"/> wrapping the given data.</summary>
    internal ByteVector(byte[] data)
    {
        Data = data;
        Length = data.Length;
    }

    /// <summary>Returns the actual number of bytes in this vector.</summary>
    public int Size => Length;

    /// <summary>Puts a byte into this vector. Returns this for chaining.</summary>
    public ByteVector PutByte(int byteValue)
    {
        int currentLength = Length;
        if (currentLength + 1 > Data.Length)
            Enlarge(1);
        Data[currentLength++] = (byte)byteValue;
        Length = currentLength;
        return this;
    }

    /// <summary>Puts two bytes. Internal fast path.</summary>
    internal ByteVector Put11(int byteValue1, int byteValue2)
    {
        int currentLength = Length;
        if (currentLength + 2 > Data.Length)
            Enlarge(2);
        var currentData = Data;
        currentData[currentLength++] = (byte)byteValue1;
        currentData[currentLength++] = (byte)byteValue2;
        Length = currentLength;
        return this;
    }

    /// <summary>Puts a short (2 bytes, big-endian). Returns this for chaining.</summary>
    public ByteVector PutShort(int shortValue)
    {
        int currentLength = Length;
        if (currentLength + 2 > Data.Length)
            Enlarge(2);
        var currentData = Data;
        currentData[currentLength++] = (byte)((int)((uint)shortValue >> 8));
        currentData[currentLength++] = (byte)shortValue;
        Length = currentLength;
        return this;
    }

    /// <summary>Puts a byte and a short (3 bytes). Internal fast path.</summary>
    internal ByteVector Put12(int byteValue, int shortValue)
    {
        int currentLength = Length;
        if (currentLength + 3 > Data.Length)
            Enlarge(3);
        var currentData = Data;
        currentData[currentLength++] = (byte)byteValue;
        currentData[currentLength++] = (byte)((int)((uint)shortValue >> 8));
        currentData[currentLength++] = (byte)shortValue;
        Length = currentLength;
        return this;
    }

    /// <summary>Puts two bytes and a short (4 bytes). Internal fast path.</summary>
    internal ByteVector Put112(int byteValue1, int byteValue2, int shortValue)
    {
        int currentLength = Length;
        if (currentLength + 4 > Data.Length)
            Enlarge(4);
        var currentData = Data;
        currentData[currentLength++] = (byte)byteValue1;
        currentData[currentLength++] = (byte)byteValue2;
        currentData[currentLength++] = (byte)((int)((uint)shortValue >> 8));
        currentData[currentLength++] = (byte)shortValue;
        Length = currentLength;
        return this;
    }

    /// <summary>Puts an int (4 bytes, big-endian). Returns this for chaining.</summary>
    public ByteVector PutInt(int intValue)
    {
        int currentLength = Length;
        if (currentLength + 4 > Data.Length)
            Enlarge(4);
        var currentData = Data;
        currentData[currentLength++] = (byte)((int)((uint)intValue >> 24));
        currentData[currentLength++] = (byte)((int)((uint)intValue >> 16));
        currentData[currentLength++] = (byte)((int)((uint)intValue >> 8));
        currentData[currentLength++] = (byte)intValue;
        Length = currentLength;
        return this;
    }

    /// <summary>Puts one byte and two shorts (5 bytes). Internal fast path.</summary>
    internal ByteVector Put122(int byteValue, int shortValue1, int shortValue2)
    {
        int currentLength = Length;
        if (currentLength + 5 > Data.Length)
            Enlarge(5);
        var currentData = Data;
        currentData[currentLength++] = (byte)byteValue;
        currentData[currentLength++] = (byte)((int)((uint)shortValue1 >> 8));
        currentData[currentLength++] = (byte)shortValue1;
        currentData[currentLength++] = (byte)((int)((uint)shortValue2 >> 8));
        currentData[currentLength++] = (byte)shortValue2;
        Length = currentLength;
        return this;
    }

    /// <summary>Puts a long (8 bytes, big-endian). Returns this for chaining.</summary>
    public ByteVector PutLong(long longValue)
    {
        int currentLength = Length;
        if (currentLength + 8 > Data.Length)
            Enlarge(8);
        var currentData = Data;
        int intValue = (int)((ulong)longValue >> 32);
        currentData[currentLength++] = (byte)((int)((uint)intValue >> 24));
        currentData[currentLength++] = (byte)((int)((uint)intValue >> 16));
        currentData[currentLength++] = (byte)((int)((uint)intValue >> 8));
        currentData[currentLength++] = (byte)intValue;
        intValue = (int)longValue;
        currentData[currentLength++] = (byte)((int)((uint)intValue >> 24));
        currentData[currentLength++] = (byte)((int)((uint)intValue >> 16));
        currentData[currentLength++] = (byte)((int)((uint)intValue >> 8));
        currentData[currentLength++] = (byte)intValue;
        Length = currentLength;
        return this;
    }

    /// <summary>
    /// Puts a modified UTF-8 string into this vector. The string length is encoded
    /// in 2 bytes before the encoded characters. Returns this for chaining.
    /// </summary>
    public ByteVector PutUTF8(string stringValue)
    {
        int charLength = stringValue.Length;
        if (charLength > 65535)
            throw new ArgumentException("UTF8 string too large");

        int currentLength = Length;
        if (currentLength + 2 + charLength > Data.Length)
            Enlarge(2 + charLength);
        var currentData = Data;

        // 乐观算法：假设字节长度等于字符长度（最常见情况），直接开始序列化。
        // 如果发现假设错误，切换到通用方法。
        currentData[currentLength++] = (byte)((int)((uint)charLength >> 8));
        currentData[currentLength++] = (byte)charLength;
        for (int i = 0; i < charLength; i++)
        {
            char charValue = stringValue[i];
            if (charValue >= '\u0001' && charValue <= '\u007F')
            {
                currentData[currentLength++] = (byte)charValue;
            }
            else
            {
                Length = currentLength;
                return EncodeUtf8(stringValue, i, 65535);
            }
        }
        Length = currentLength;
        return this;
    }

    /// <summary>
    /// Encodes the remaining characters of stringValue (from offset onward) as modified UTF-8.
    /// The byte length is stored at the position reserved by PutUTF8.
    /// </summary>
    internal ByteVector EncodeUtf8(string stringValue, int offset, int maxByteLength)
    {
        int charLength = stringValue.Length;
        int byteLength = offset;
        for (int i = offset; i < charLength; i++)
        {
            char charValue = stringValue[i];
            if (charValue >= 0x0001 && charValue <= 0x007F)
                byteLength++;
            else if (charValue <= 0x07FF)
                byteLength += 2;
            else
                byteLength += 3;
        }
        if (byteLength > maxByteLength)
            throw new ArgumentException("UTF8 string too large");

        // 存储 byteLength 到之前预留的位置
        int byteLengthOffset = Length - offset - 2;
        if (byteLengthOffset >= 0)
        {
            Data[byteLengthOffset] = (byte)((int)((uint)byteLength >> 8));
            Data[byteLengthOffset + 1] = (byte)byteLength;
        }
        if (Length + byteLength - offset > Data.Length)
            Enlarge(byteLength - offset);

        int currentLength = Length;
        for (int i = offset; i < charLength; i++)
        {
            char charValue = stringValue[i];
            if (charValue >= 0x0001 && charValue <= 0x007F)
            {
                Data[currentLength++] = (byte)charValue;
            }
            else if (charValue <= 0x07FF)
            {
                Data[currentLength++] = (byte)(0xC0 | ((charValue >> 6) & 0x1F));
                Data[currentLength++] = (byte)(0x80 | (charValue & 0x3F));
            }
            else
            {
                Data[currentLength++] = (byte)(0xE0 | ((charValue >> 12) & 0xF));
                Data[currentLength++] = (byte)(0x80 | ((charValue >> 6) & 0x3F));
                Data[currentLength++] = (byte)(0x80 | (charValue & 0x3F));
            }
        }
        Length = currentLength;
        return this;
    }

    /// <summary>
    /// Puts an array of bytes into this vector. If <paramref name="byteArrayValue"/> is null,
    /// puts <paramref name="byteLength"/> zero bytes.
    /// </summary>
    public ByteVector PutByteArray(byte[]? byteArrayValue, int byteOffset, int byteLength)
    {
        if (Length + byteLength > Data.Length)
            Enlarge(byteLength);
        if (byteArrayValue != null)
            Array.Copy(byteArrayValue, byteOffset, Data, Length, byteLength);
        Length += byteLength;
        return this;
    }

    /// <summary>Enlarges this vector so that it can receive <paramref name="size"/> more bytes.</summary>
    private void Enlarge(int size)
    {
        if (Length > Data.Length)
            throw new InvalidOperationException("Internal error: length > capacity");

        int doubleCapacity = 2 * Data.Length;
        int minimalCapacity = Length + size;
        int newCapacity = doubleCapacity > minimalCapacity ? doubleCapacity : minimalCapacity;
        var newData = new byte[newCapacity];
        Array.Copy(Data, 0, newData, 0, Length);
        Data = newData;
    }

    /// <summary>Returns a copy of the valid bytes as a new array.</summary>
    public byte[] ToArray()
    {
        var result = new byte[Length];
        Array.Copy(Data, 0, result, 0, Length);
        return result;
    }
}
