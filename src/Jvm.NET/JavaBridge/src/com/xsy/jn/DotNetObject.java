package com.xsy.jn;

/**
 * .NET 对象的 Java 代理。
 *
 * <p>Java 代码通过此类透明地调用 .NET 对象的方法、访问字段。
 * 内部通过 {@link Bridge} 的 native 方法路由到 .NET 侧。</p>
 *
 * <p>对象生命周期：Java 代理被 GC 回收时自动调用
 * {@link Bridge#unregisterDotNetObject(long)} 释放 .NET 侧引用。</p>
 */
public final class DotNetObject {

    private final long _handle;

    /**
     * 包装已有的 .NET 对象句柄。
     *
     * @param handle .NET 对象句柄（非零）
     * @throws IllegalArgumentException 如果 handle 为 0
     */
    public DotNetObject(long handle) {
        if (handle == 0) {
            throw new IllegalArgumentException("handle cannot be zero");
        }
        _handle = handle;
    }

    /**
     * 从 .NET 类型创建新实例并返回代理。
     *
     * @param typeName    .NET 完全限定类型名
     * @param assemblyName .NET 程序集名（可为 {@code null}）
     * @throws RuntimeException 如果创建失败
     */
    public DotNetObject(String typeName, String assemblyName) {
        _handle = Bridge.registerDotNetObject(typeName, assemblyName);
        if (_handle == 0) {
            throw new RuntimeException("Failed to create .NET object: " + typeName);
        }
    }

    /** 返回底层 .NET 对象句柄。 */
    public long getHandle() {
        return _handle;
    }

    /**
     * 调用 .NET 对象的实例方法。
     *
     * @param methodName 方法名
     * @param signature  .NET 方法签名
     * @param args       参数列表
     * @return 返回值（void 方法返回 {@code null}）
     */
    public Object invoke(String methodName, String signature, Object... args) {
        return Bridge.invokeDotNetMethod(_handle, methodName, signature, args);
    }

    /** 便捷方法：调用无参数方法。 */
    public Object invoke(String methodName) {
        return Bridge.invokeDotNetMethod(_handle, methodName, "()", null);
    }

    /**
     * 获取 .NET 对象字段值。
     *
     * @param fieldName 字段名
     * @return 字段值（基本类型装箱）
     */
    public Object getField(String fieldName) {
        return Bridge.getDotNetField(_handle, fieldName);
    }

    /**
     * 设置 .NET 对象字段值。
     *
     * @param fieldName 字段名
     * @param value     新值
     */
    public void setField(String fieldName, Object value) {
        Bridge.setDotNetField(_handle, fieldName, value);
    }

    /**
     * 检查此对象是否为指定 .NET 类型的实例。
     *
     * @param typeName .NET 完全限定类型名
     * @return 如果可赋值则返回 {@code true}
     */
    public boolean isInstanceOf(String typeName) {
        return Bridge.isInstanceOf(_handle, typeName);
    }

    @Override
    protected void finalize() throws Throwable {
        try {
            if (_handle != 0) {
                Bridge.unregisterDotNetObject(_handle);
            }
        } finally {
            super.finalize();
        }
    }

    @Override
    public String toString() {
        return "DotNetObject(handle=" + _handle + ")";
    }
}
