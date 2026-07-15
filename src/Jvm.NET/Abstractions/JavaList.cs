using System.Collections;

namespace Jvm.NET.Abstractions;

/// <summary>
/// Java <c>java.util.List</c> 的 .NET 包装，提供 <see cref="IList{T}"/> 语义。
/// 通过 <see cref="IJvmInvoker"/> 调用 List 的 size/get/add/set/remove 等方法。
/// </summary>
/// <typeparam name="T">元素类型。支持 int/long/double/float/bool/string/JvmObject。</typeparam>
public sealed class JavaList<T> : IList<T>, IDisposable
{
    private readonly IJvmInvoker _invoker;
    private readonly JvmObject _list;

    /// <summary>包装一个已有的 Java List 对象。</summary>
    public JavaList(IJvmInvoker invoker, JvmObject list)
    {
        _invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        _list = list ?? throw new ArgumentNullException(nameof(list));
    }

    /// <summary>创建一个新的 java.util.ArrayList。</summary>
    public static JavaList<T> NewArrayList(IJvmInvoker invoker)
    {
        var arrayListClass = invoker.LoadClass("java.util.ArrayList");
        var obj = invoker.NewObject(arrayListClass, "()V");
        return new JavaList<T>(invoker, obj);
    }

    public JvmObject UnderlyingObject => _list;

    public T this[int index]
    {
        get
        {
            var v = _invoker.InvokeVirtual(_list, "get", "(I)Ljava/lang/Object;", JvmValue.FromInt(index));
            return ConvertFromJava(v);
        }
        set => _invoker.InvokeVirtual(_list, "set",
            "(ILjava/lang/Object;)Ljava/lang/Object;", JvmValue.FromInt(index), ConvertToJava(value));
    }

    public int Count => _invoker.InvokeVirtual(_list, "size", "()I").AsInt();

    public bool IsReadOnly => false;

    public void Add(T item)
        => _invoker.InvokeVirtual(_list, "add", "(Ljava/lang/Object;)Z", ConvertToJava(item));

    public void Clear() => _invoker.InvokeVirtual(_list, "clear", "()V");

    public bool Contains(T item)
    {
        var v = _invoker.InvokeVirtual(_list, "contains", "(Ljava/lang/Object;)Z", ConvertToJava(item));
        return v.AsBoolean();
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        int i = arrayIndex;
        foreach (var item in this)
        {
            if (i >= array.Length) break;
            array[i++] = item;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        var count = Count;
        for (int i = 0; i < count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int IndexOf(T item)
    {
        var v = _invoker.InvokeVirtual(_list, "indexOf", "(Ljava/lang/Object;)I", ConvertToJava(item));
        return v.AsInt();
    }

    public void Insert(int index, T item)
        => _invoker.InvokeVirtual(_list, "add", "(ILjava/lang/Object;)V", JvmValue.FromInt(index), ConvertToJava(item));

    public bool Remove(T item)
    {
        var v = _invoker.InvokeVirtual(_list, "remove", "(Ljava/lang/Object;)Z", ConvertToJava(item));
        return v.AsBoolean();
    }

    public void RemoveAt(int index)
        => _invoker.InvokeVirtual(_list, "remove", "(I)Ljava/lang/Object;", JvmValue.FromInt(index));

    public void Dispose() => _list.Dispose();

    // ---- 类型转换 ----
    // 当 T 是 CLR 值类型时，Java 侧的 Object 元素需要装箱/拆箱为对应的包装类。

    private JvmValue ConvertToJava(T value)
    {
        var jvmValue = TypeMapper.FromClr(value, _invoker);
        return typeof(T).IsValueType ? TypeMapper.Box(jvmValue, _invoker) : jvmValue;
    }

    private T ConvertFromJava(JvmValue value)
    {
        if (typeof(T).IsValueType && value.Type == JvmValueType.Object)
            value = TypeMapper.Unbox(value, _invoker);
        return TypeMapper.ToClr<T>(value, _invoker) ?? default!;
    }
}
