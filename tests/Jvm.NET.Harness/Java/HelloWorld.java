public class HelloWorld {
    public static void main(String[] args) {
        System.out.println("Hello from Java! Args count: " + args.length);
        for (int i = 0; i < args.length; i++) {
            System.out.println("  arg[" + i + "] = " + args[i]);
        }
    }

    public static int add(int a, int b) {
        return a + b;
    }

    public static String echo(String s) {
        return "Echo: " + s;
    }
}
