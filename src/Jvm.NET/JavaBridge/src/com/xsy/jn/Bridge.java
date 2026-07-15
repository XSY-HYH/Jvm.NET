package com.xsy.jn;

/**
 * Jvm.NET Java 桥接入口。
 *
 * <p>类加载时通过静态初始化器通知 .NET 侧完成桥接注册。
 * .NET 侧通过 {@code RegisterNatives} 注册以下 native 方法的实现，
 * 使 Java 代码可以透明地调用 .NET 对象。</p>
 *
 * <p>此 jar 仅在 {@code InteropMode.WithJar} 模式下使用。
 * 纯 JNI+ASM 模式（{@code NativeOnly}）无需此 jar。</p>
 */
public final class Bridge {

    static {
        // 触发 .NET 侧的初始化回调（由 RegisterNatives 注册）
        onBridgeInitialized();
    }

    private Bridge() {
        throw new UnsupportedOperationException("Bridge is a static utility class");
    }

    // ---- 生命周期回调 ----

    /**
     * .NET 侧注册的初始化回调，类加载时自动调用。
     * .NET 侧可在此完成对象映射表初始化等准备工作。
     */
    private static native void onBridgeInitialized();

    // ---- .NET 对象管理 ----

    /**
     * 注册 .NET 对象到 Java 侧，返回 Java 代理句柄。
     *
     * @param typeName    .NET 完全限定类型名（如 {@code System.Text.StringBuilder}）
     * @param assemblyName .NET 程序集名（可为 {@code null}）
     * @return .NET 对象句柄（非零），失败返回 0
     */
    public static native long registerDotNetObject(String typeName, String assemblyName);

    /**
     * 注销 .NET 对象，释放 .NET 侧的对象引用。
     *
     * @param handle {@link #registerDotNetObject} 返回的句柄
     */
    public static native void unregisterDotNetObject(long handle);

    /**
     * 调用 .NET 对象的实例方法。
     *
     * @param handle      .NET 对象句柄
     * @param methodName  方法名
     * @param signature   方法签名（.NET 风格，如 {@code "(System.String)System.Int32"}）
     * @param args        参数列表（可为 {@code null}）
     * @return 返回值（基本类型装箱，void 返回 {@code null}）
     */
    public static native Object invokeDotNetMethod(long handle, String methodName, String signature, Object[] args);

    /**
     * 获取 .NET 对象字段值。
     *
     * @param handle    .NET 对象句柄
     * @param fieldName 字段名
     * @return 字段值（基本类型装箱）
     */
    public static native Object getDotNetField(long handle, String fieldName);

    /**
     * 设置 .NET 对象字段值。
     *
     * @param handle    .NET 对象句柄
     * @param fieldName 字段名
     * @param value     新值（基本类型需装箱）
     */
    public static native void setDotNetField(long handle, String fieldName, Object value);

    /**
     * 检查 .NET 对象是否为指定类型的实例。
     *
     * @param handle   .NET 对象句柄
     * @param typeName .NET 完全限定类型名
     * @return 如果对象可赋值到指定类型则返回 {@code true}
     */
    public static native boolean isInstanceOf(long handle, String typeName);
}
