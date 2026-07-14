using System.Runtime.InteropServices;

namespace Jvm.NET.Abstractions.Jdk21;

//
// jvalue 是 8 字节联合体，可以容纳所有 JNI 基础类型 + 对象引用。
// 我们用 long 作为内存布局（小端字节序下与 jboolean/jbyte/jchar/jshort/jint
// 的低位字节重叠；jlong/jfloat（前 4 字节）/jdouble/jobject 都能装下）。
//
// 实际 jfloat 的位布局会被原样拷贝到 long 的低 32 位，JNI 侧按 jfloat 读取时
// 能正确还原。jdouble 的位布局直接对应 long。
//
// 这种"用 long 容纳一切"的方式比 StructLayout Explicit 更简单且 blittable。
//
internal sealed unsafe class JValueArray : IDisposable
{
    private readonly long* _ptr;
    private readonly int _length;
    private bool _disposed;

    public JValueArray(JvmValue[] values)
    {
        _length = values?.Length ?? 0;
        _ptr = (long*)Marshal.AllocHGlobal(_length * sizeof(long));

        for (int i = 0; i < _length; i++)
        {
            var v = values![i];
            _ptr[i] = v.Type switch
            {
                JvmValueType.Null    => 0L,
                JvmValueType.Boolean => v.LongValue,
                JvmValueType.Byte    => v.LongValue,
                JvmValueType.Char    => v.LongValue,
                JvmValueType.Short   => v.LongValue,
                JvmValueType.Int     => v.LongValue,
                JvmValueType.Long    => v.LongValue,
                JvmValueType.Float   => FloatToLongBits((float)v.DoubleValue),
                JvmValueType.Double  => BitConverter.DoubleToInt64Bits(v.DoubleValue),
                JvmValueType.Object  => v.ObjectHandle.ToInt64(),
                _ => 0L,
            };
        }
    }

    public IntPtr RawPointer => (IntPtr)_ptr;
    public int Length => _length;

    private static long FloatToLongBits(float f)
    {
        // 把 float 的 32 位位布局放进 long 的低 32 位，高 32 位清零。
        // 与 jfloat 在 jvalue 联合体中的位置一致。
        long bits = (long)BitConverter.SingleToInt32Bits(f);
        return bits;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        if (_ptr != null)
            Marshal.FreeHGlobal((IntPtr)_ptr);
        _disposed = true;
    }
}
