using System.Collections;

namespace Jvm.NET.Abstractions;

/// <summary>
/// Java <c>java.util.Map</c> 的 .NET 包装，提供 <see cref="IDictionary{TKey, TValue}"/> 语义。
/// 通过 <see cref="IJvmInvoker"/> 调用 Map 的 get/put/size/remove 等方法。
/// </summary>
public sealed class JavaMap<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable
{
    private readonly IJvmInvoker _invoker;
    private readonly JvmObject _map;

    /// <summary>包装一个已有的 Java Map 对象。</summary>
    public JavaMap(IJvmInvoker invoker, JvmObject map)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _map = map ?? throw new ArgumentNullException(nameof(map));
    }

    /// <summary>创建一个新的 java.util.HashMap。</summary>
    public static JavaMap<TKey, TValue> NewHashMap(IJvmInvoker invoker)
    {
        var hashMapClass = invoker.LoadClass("java.util.HashMap");
        var obj = invoker.NewObject(hashMapClass, "()V");
        return new JavaMap<TKey, TValue>(invoker, obj);
    }

    public JvmObject UnderlyingObject => _map;

    public TValue this[TKey key]
    {
        get
        {
            var v = _invoker.InvokeVirtual(_map, "get",
                "(Ljava/lang/Object;)Ljava/lang/Object;", ConvertKeyToJava(key));
            return ConvertValueFromJava(v);
        }
        set => _invoker.InvokeVirtual(_map, "put",
            "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;",
            ConvertKeyToJava(key), ConvertValueToJava(value));
    }

    public ICollection<TKey> Keys
    {
        get
        {
            var keySet = _invoker.InvokeVirtual(_map, "keySet", "()Ljava/util/Set;");
            // 简化实现：通过迭代器遍历
            var result = new List<TKey>();
            var setObj = new JvmObject(keySet.ObjectHandle, new JvmClass(IntPtr.Zero, "java/util/Set"));
            var iter = _invoker.InvokeVirtual(setObj, "iterator", "()Ljava/util/Iterator;");
            var iterObj = new JvmObject(iter.ObjectHandle, new JvmClass(IntPtr.Zero, "java/util/Iterator"));
            while (true)
            {
                var hasNext = _invoker.InvokeVirtual(iterObj, "hasNext", "()Z");
                if (!hasNext.AsBoolean()) break;
                var next = _invoker.InvokeVirtual(iterObj, "next", "()Ljava/lang/Object;");
                result.Add(ConvertKeyFromJava(next));
            }
            return result;
        }
    }

    public ICollection<TValue> Values
    {
        get
        {
            var values = _invoker.InvokeVirtual(_map, "values", "()Ljava/util/Collection;");
            var result = new List<TValue>();
            var colObj = new JvmObject(values.ObjectHandle, new JvmClass(IntPtr.Zero, "java/util/Collection"));
            var iter = _invoker.InvokeVirtual(colObj, "iterator", "()Ljava/util/Iterator;");
            var iterObj = new JvmObject(iter.ObjectHandle, new JvmClass(IntPtr.Zero, "java/util/Iterator"));
            while (true)
            {
                var hasNext = _invoker.InvokeVirtual(iterObj, "hasNext", "()Z");
                if (!hasNext.AsBoolean()) break;
                var next = _invoker.InvokeVirtual(iterObj, "next", "()Ljava/lang/Object;");
                result.Add(ConvertValueFromJava(next));
            }
            return result;
        }
    }

    public int Count => _invoker.InvokeVirtual(_map, "size", "()I").AsInt();

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
        => _invoker.InvokeVirtual(_map, "put",
            "(Ljava/lang/Object;Ljava/lang/Object;)Ljava/lang/Object;",
            ConvertKeyToJava(key), ConvertValueToJava(value));

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public void Clear() => _invoker.InvokeVirtual(_map, "clear", "()V");

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        var v = _invoker.InvokeVirtual(_map, "containsKey",
            "(Ljava/lang/Object;)Z", ConvertKeyToJava(item.Key));
        return v.AsBoolean();
    }

    public bool ContainsKey(TKey key)
    {
        var v = _invoker.InvokeVirtual(_map, "containsKey",
            "(Ljava/lang/Object;)Z", ConvertKeyToJava(key));
        return v.AsBoolean();
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        int i = arrayIndex;
        foreach (var kvp in this)
        {
            if (i >= array.Length) break;
            array[i++] = kvp;
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        var entrySet = _invoker.InvokeVirtual(_map, "entrySet", "()Ljava/util/Set;");
        var setObj = new JvmObject(entrySet.ObjectHandle, new JvmClass(IntPtr.Zero, "java/util/Set"));
        var iter = _invoker.InvokeVirtual(setObj, "iterator", "()Ljava/util/Iterator;");
        var iterObj = new JvmObject(iter.ObjectHandle, new JvmClass(IntPtr.Zero, "java/util/Iterator"));
        while (true)
        {
            var hasNext = _invoker.InvokeVirtual(iterObj, "hasNext", "()Z");
            if (!hasNext.AsBoolean()) break;
            var next = _invoker.InvokeVirtual(iterObj, "next", "()Ljava/lang/Object;");
            var entryObj = new JvmObject(next.ObjectHandle, new JvmClass(IntPtr.Zero, "java/util/Map$Entry"));
            var key = _invoker.InvokeVirtual(entryObj, "getKey", "()Ljava/lang/Object;");
            var value = _invoker.InvokeVirtual(entryObj, "getValue", "()Ljava/lang/Object;");
            yield return new KeyValuePair<TKey, TValue>(ConvertKeyFromJava(key), ConvertValueFromJava(value));
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Remove(TKey key)
    {
        _invoker.InvokeVirtual(_map, "remove",
            "(Ljava/lang/Object;)Ljava/lang/Object;", ConvertKeyToJava(key));
        return true;
    }

    public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (!ContainsKey(key))
        {
            value = default!;
            return false;
        }
        value = this[key];
        return true;
    }

    public void Dispose() => _map.Dispose();

    // ---- 类型转换 ----
    // 当 TKey/TValue 是 CLR 值类型时，Java 侧的 Object 键值需要装箱/拆箱为对应的包装类。

    private JvmValue ConvertKeyToJava(TKey key)
    {
        var jvmValue = TypeMapper.FromClr(key, _invoker);
        return typeof(TKey).IsValueType ? TypeMapper.Box(jvmValue, _invoker) : jvmValue;
    }

    private JvmValue ConvertValueToJava(TValue value)
    {
        var jvmValue = TypeMapper.FromClr(value, _invoker);
        return typeof(TValue).IsValueType ? TypeMapper.Box(jvmValue, _invoker) : jvmValue;
    }

    private TKey ConvertKeyFromJava(JvmValue value)
    {
        if (typeof(TKey).IsValueType && value.Type == JvmValueType.Object)
            value = TypeMapper.Unbox(value, _invoker);
        return TypeMapper.ToClr<TKey>(value, _invoker) ?? default!;
    }

    private TValue ConvertValueFromJava(JvmValue value)
    {
        if (typeof(TValue).IsValueType && value.Type == JvmValueType.Object)
            value = TypeMapper.Unbox(value, _invoker);
        return TypeMapper.ToClr<TValue>(value, _invoker) ?? default!;
    }
}
