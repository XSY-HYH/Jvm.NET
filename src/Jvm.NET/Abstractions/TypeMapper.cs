namespace Jvm.NET.Abstractions;

/// <summary>
/// Java 类型与 .NET 类型之间的双向转换辅助类。
/// 处理基本类型、字符串、对象引用的自动映射。
/// </summary>
public static class TypeMapper
{
    // ---- CLR → JvmValue ----

    /// <summary>
    /// 将 .NET 值转换为 <see cref="JvmValue"/>。
    /// 支持 bool/byte/char/short/int/long/float/double/string/JvmValue/JvmObject。
    /// </summary>
    public static JvmValue FromClr(object? value, IJvmInvoker invoker)
    {
        if (value is null) return JvmValue.Null;

        return value switch
        {
            bool b => JvmValue.FromBoolean(b),
            byte b => JvmValue.FromByte(b),
            char c => JvmValue.FromChar(c),
            short s => JvmValue.FromShort(s),
            int i => JvmValue.FromInt(i),
            long l => JvmValue.FromLong(l),
            float f => JvmValue.FromFloat(f),
            double d => JvmValue.FromDouble(d),
            string s => invoker.NewString(s),
            JvmValue v => v,
            JvmObject obj => JvmValue.FromObject(obj.Handle),
            _ => throw new NotSupportedException($"不支持的 CLR 类型: {value.GetType()}"),
        };
    }

    /// <summary>将 .NET 值数组转换为 <see cref="JvmValue"/> 数组。</summary>
    public static JvmValue[] FromClrArray(object?[] values, IJvmInvoker invoker)
    {
        if (values is null) return Array.Empty<JvmValue>();
        var result = new JvmValue[values.Length];
        for (int i = 0; i < values.Length; i++)
            result[i] = FromClr(values[i], invoker);
        return result;
    }

    // ---- JvmValue → CLR ----

    /// <summary>
    /// 将 <see cref="JvmValue"/> 转换为指定 .NET 类型。
    /// 支持 int/long/double/float/bool/string/JvmObject。
    /// </summary>
    public static T? ToClr<T>(JvmValue value, IJvmInvoker invoker)
    {
        var t = typeof(T);
        object? result = ToClr(value, t, invoker);
        return (T?)result;
    }

    /// <summary>非泛型版本：将 <see cref="JvmValue"/> 转换为指定 .NET 类型。</summary>
    public static object? ToClr(JvmValue value, Type targetType, IJvmInvoker invoker)
    {
        if (value.Type == JvmValueType.Null)
        {
            return targetType.IsValueType ? null : null;
        }

        if (targetType == typeof(string))
            return ToString(value, invoker);

        if (targetType == typeof(JvmObject) || targetType == typeof(JvmClass))
            return value.Type == JvmValueType.Object && value.ObjectHandle != IntPtr.Zero
                ? new JvmObject(value.ObjectHandle, new JvmClass(IntPtr.Zero, "java/lang/Object"))
                : null;

        return targetType switch
        {
            _ when targetType == typeof(int) => value.AsInt(),
            _ when targetType == typeof(long) => value.AsLong(),
            _ when targetType == typeof(double) => value.AsDouble(),
            _ when targetType == typeof(float) => value.AsFloat(),
            _ when targetType == typeof(bool) => value.AsBoolean(),
            _ when targetType == typeof(byte) => value.AsByte(),
            _ when targetType == typeof(char) => value.AsChar(),
            _ when targetType == typeof(short) => value.AsShort(),
            _ => throw new NotSupportedException($"不支持的目标类型: {targetType}"),
        };
    }

    /// <summary>
    /// 将 <see cref="JvmValue"/> 转换为字符串。
    /// 如果值是对象引用且非空，调用 <see cref="IJvmInvoker.GetString"/> 读取 Java String。
    /// 如果是基本类型，直接转换为字符串。
    /// </summary>
    public static string? ToString(JvmValue value, IJvmInvoker invoker)
    {
        if (value.Type == JvmValueType.Null) return null;
        if (value.Type != JvmValueType.Object) return value.AsLong().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (value.ObjectHandle == IntPtr.Zero) return null;
        return invoker.GetString(value.ObjectHandle);
    }

    /// <summary>
    /// 将 Java String 数组（<c>String[]</c>）转换为 .NET 字符串数组。
    /// </summary>
    public static string[] ToStringArray(JvmValue arrayValue, IJvmInvoker invoker)
    {
        if (arrayValue.Type != JvmValueType.Object || arrayValue.ObjectHandle == IntPtr.Zero)
            return Array.Empty<string>();

        var length = invoker.GetArrayLength(arrayValue);
        var result = new string[length];
        for (int i = 0; i < length; i++)
        {
            var element = invoker.GetObjectArrayElement(arrayValue, i);
            result[i] = invoker.GetString(element.ObjectHandle);
        }
        return result;
    }

    // ---- 装箱 / 拆箱 ----
    // Java 集合（List/Map）的元素类型是 Object，CLR 值类型（int/long/double 等）
    // 传给 Java Object 参数时必须装箱为对应的 java.lang.Integer/Long/Double 等包装类，
    // 否则 JVM 会把基本类型的位模式当作对象指针解引用导致崩溃。

    /// <summary>
    /// 将基本类型的 JvmValue 装箱为对应的 Java 包装类对象（Integer/Long/Double 等）。
    /// 用于把 CLR 值类型传给 Java Object 参数（如 <c>Map.put(key, Object)</c>）。
    /// 如果 value 已经是 Object 或 Null，原样返回。
    /// </summary>
    public static JvmValue Box(JvmValue value, IJvmInvoker invoker)
    {
        return value.Type switch
        {
            JvmValueType.Boolean => BoxPrimitive(value, "java.lang.Boolean", "Z", invoker),
            JvmValueType.Byte => BoxPrimitive(value, "java.lang.Byte", "B", invoker),
            JvmValueType.Char => BoxPrimitive(value, "java.lang.Character", "C", invoker),
            JvmValueType.Short => BoxPrimitive(value, "java.lang.Short", "S", invoker),
            JvmValueType.Int => BoxPrimitive(value, "java.lang.Integer", "I", invoker),
            JvmValueType.Long => BoxPrimitive(value, "java.lang.Long", "J", invoker),
            JvmValueType.Float => BoxPrimitive(value, "java.lang.Float", "F", invoker),
            JvmValueType.Double => BoxPrimitive(value, "java.lang.Double", "D", invoker),
            _ => value,
        };
    }

    private static JvmValue BoxPrimitive(JvmValue value, string className, string primitiveSig, IJvmInvoker invoker)
    {
        using var clazz = invoker.LoadClass(className);
        var boxedSig = "L" + className.Replace('.', '/') + ";";
        return invoker.InvokeStatic(clazz, "valueOf", $"({primitiveSig}){boxedSig}", value);
    }

    /// <summary>
    /// 将 Java 包装类对象（Integer/Long/Double 等）拆箱为基本类型的 JvmValue。
    /// 如果 value 已经是基本类型或 Null，原样返回。
    /// 通过 <c>getClass().getName()</c> 识别包装类，再调用对应的 <c>xxxValue()</c> 方法。
    /// </summary>
    public static JvmValue Unbox(JvmValue value, IJvmInvoker invoker)
    {
        if (value.Type != JvmValueType.Object || value.ObjectHandle == IntPtr.Zero)
            return value;

        var obj = new JvmObject(value.ObjectHandle, new JvmClass(IntPtr.Zero, "java/lang/Object"));

        // 调用 getClass().getName() 获取运行时类名
        var classObj = invoker.InvokeVirtual(obj, "getClass", "()Ljava/lang/Class;");
        var classObjWrapper = new JvmObject(classObj.ObjectHandle, new JvmClass(IntPtr.Zero, "java/lang/Class"));
        var nameValue = invoker.InvokeVirtual(classObjWrapper, "getName", "()Ljava/lang/String;");
        var className = invoker.GetString(nameValue.ObjectHandle);

        return className switch
        {
            "java.lang.Boolean" => invoker.InvokeVirtual(obj, "booleanValue", "()Z"),
            "java.lang.Byte" => invoker.InvokeVirtual(obj, "byteValue", "()B"),
            "java.lang.Character" => invoker.InvokeVirtual(obj, "charValue", "()C"),
            "java.lang.Short" => invoker.InvokeVirtual(obj, "shortValue", "()S"),
            "java.lang.Integer" => invoker.InvokeVirtual(obj, "intValue", "()I"),
            "java.lang.Long" => invoker.InvokeVirtual(obj, "longValue", "()J"),
            "java.lang.Float" => invoker.InvokeVirtual(obj, "floatValue", "()F"),
            "java.lang.Double" => invoker.InvokeVirtual(obj, "doubleValue", "()D"),
            _ => value, // 非包装类，原样返回
        };
    }
}
