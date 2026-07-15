import java.util.*;

/**
 * 互操作测试目标类。包含字段访问、数组、集合、对象参数等场景，
 * 供 Jvm.NET 互操作 API（字段访问/数组/类型映射/JavaList/JavaMap）测试。
 */
public class InteropTarget {

    // ---- 实例字段 ----
    public int intValue;
    public String name;
    private static int s_counter = 0;

    // ---- 静态字段 ----
    public static final String VERSION = "jn-2.0";
    public static long s_total = 0;

    public InteropTarget(int value, String name) {
        this.intValue = value;
        this.name = name;
        s_counter++;
        s_total += value;
    }

    // ---- 数组返回 ----
    public int[] makeIntArray(int size, int fill) {
        int[] arr = new int[size];
        Arrays.fill(arr, fill);
        return arr;
    }

    public String[] makeStringArray() {
        return new String[]{ "alpha", "beta", "gamma" };
    }

    // ---- 集合返回 ----
    public List<String> makeList() {
        List<String> list = new ArrayList<>();
        list.add("one");
        list.add("two");
        list.add("three");
        return list;
    }

    public Map<String, Integer> makeMap() {
        Map<String, Integer> map = new HashMap<>();
        map.put("a", 1);
        map.put("b", 2);
        map.put("c", 3);
        return map;
    }

    // ---- 对象参数 ----
    public String greet(InteropTarget other) {
        return name + " greets " + other.name;
    }

    // ---- 类型检查辅助 ----
    public static boolean isString(Object obj) {
        return obj instanceof String;
    }

    // ---- 异常测试 ----
    public static void throwNamed(String message) {
        throw new IllegalStateException(message);
    }

    // ---- 计数器 ----
    public static int getCounter() {
        return s_counter;
    }
}
