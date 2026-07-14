namespace Jvm.NET.Abstractions;

/// <summary>
/// Discriminated union covering all primitive JVM value kinds plus object references.
/// Used for both method arguments and return values across <see cref="IJvmInvoker"/>.
/// </summary>
public enum JvmValueType
{
    Null,
    Boolean,
    Byte,
    Char,
    Short,
    Int,
    Long,
    Float,
    Double,
    Object,
}

/// <summary>
/// Lightweight, blittable value type representing a JVM argument or return value.
/// Object references are kept as raw <see cref="IntPtr"/> handles so that callers
/// can pin them without going through a managed wrapper.
/// </summary>
public readonly struct JvmValue
{
    public JvmValueType Type { get; }

    public long LongValue { get; }
    public double DoubleValue { get; }
    public IntPtr ObjectHandle { get; }

    private JvmValue(JvmValueType type, long l, double d, IntPtr handle)
    {
        Type = type; LongValue = l; DoubleValue = d; ObjectHandle = handle;
    }

    public static readonly JvmValue Null = new(JvmValueType.Null, 0, 0, IntPtr.Zero);

    public static JvmValue FromBoolean(bool v) => new(JvmValueType.Boolean, v ? 1 : 0, 0, IntPtr.Zero);
    public static JvmValue FromByte(byte v) => new(JvmValueType.Byte, v, 0, IntPtr.Zero);
    public static JvmValue FromChar(char v) => new(JvmValueType.Char, v, 0, IntPtr.Zero);
    public static JvmValue FromShort(short v) => new(JvmValueType.Short, v, 0, IntPtr.Zero);
    public static JvmValue FromInt(int v) => new(JvmValueType.Int, v, 0, IntPtr.Zero);
    public static JvmValue FromLong(long v) => new(JvmValueType.Long, v, 0, IntPtr.Zero);
    public static JvmValue FromFloat(float v) => new(JvmValueType.Float, 0, v, IntPtr.Zero);
    public static JvmValue FromDouble(double v) => new(JvmValueType.Double, 0, v, IntPtr.Zero);
    public static JvmValue FromObject(IntPtr handle) => new(JvmValueType.Object, 0, 0, handle);

    public bool AsBoolean() => LongValue != 0;
    public byte AsByte() => (byte)LongValue;
    public char AsChar() => (char)LongValue;
    public short AsShort() => (short)LongValue;
    public int AsInt() => (int)LongValue;
    public long AsLong() => LongValue;
    public float AsFloat() => (float)DoubleValue;
    public double AsDouble() => DoubleValue;
}
