#!/usr/bin/env bash
# 构建 Jvm.NET Java 桥接 jar（jn-bridge.jar）。
# 需要 JAVA_HOME 指向 JDK 8+ 安装目录，或 javac 在 PATH 中。
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="$SCRIPT_DIR/src"
BUILD_DIR="$SCRIPT_DIR/build"
CLASSES_DIR="$BUILD_DIR/classes"
OUTPUT_DIR="${1:-$SCRIPT_DIR/dist}"

# 查找 javac
JAVAC=""
if [[ -n "${JAVA_HOME:-}" ]] && [[ -x "$JAVA_HOME/bin/javac" ]]; then
    JAVAC="$JAVA_HOME/bin/javac"
elif command -v javac >/dev/null 2>&1; then
    JAVAC="$(command -v javac)"
else
    echo "错误：未找到 javac。请设置 JAVA_HOME 或将 javac 加入 PATH。" >&2
    exit 1
fi

echo "使用 javac: $JAVAC"

# 清理并创建目录
rm -rf "$BUILD_DIR"
mkdir -p "$CLASSES_DIR" "$OUTPUT_DIR"

# 编译
echo "编译 Java 源码..."
# shellcheck disable=SC2046
"$JAVAC" -source 8 -target 8 -d "$CLASSES_DIR" $(find "$SRC_DIR" -name "*.java")

# 打包
JAR_NAME="jn-bridge.jar"
JAR_PATH="$OUTPUT_DIR/$JAR_NAME"
echo "打包为 $JAR_PATH"

JAR=""
if [[ -n "${JAVA_HOME:-}" ]] && [[ -x "$JAVA_HOME/bin/jar" ]]; then
    JAR="$JAVA_HOME/bin/jar"
elif command -v jar >/dev/null 2>&1; then
    JAR="$(command -v jar)"
else
    echo "错误：未找到 jar 工具" >&2
    exit 1
fi

MANIFEST_DIR="$BUILD_DIR/META-INF"
mkdir -p "$MANIFEST_DIR"
cat > "$MANIFEST_DIR/MANIFEST.MF" <<'EOF'
Manifest-Version: 1.0
Built-By: Jvm.NET build script
Implementation-Title: Jvm.NET Java Bridge
Implementation-Version: 2.0.0
EOF

"$JAR" cfm "$JAR_PATH" "$MANIFEST_DIR/MANIFEST.MF" -C "$CLASSES_DIR" .

echo "构建完成: $JAR_PATH"
