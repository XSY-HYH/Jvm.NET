<#
.SYNOPSIS
    构建 Jvm.NET Java 桥接 jar（jn-bridge.jar）。

.DESCRIPTION
    编译 src/com/xsy/jn/*.java 并打包为 jn-bridge.jar。
    需要 JAVA_HOME 指向 JDK 8+ 安装目录，或 javac 在 PATH 中。

.PARAMETER OutputDir
    输出目录。默认为当前脚本目录下的 dist。

.EXAMPLE
    .\build.ps1
    .\build.ps1 -OutputDir D:\artifacts
#>
[CmdletBinding()]
param(
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $scriptDir "src"
$buildDir = Join-Path $scriptDir "build"
$classesDir = Join-Path $buildDir "classes"

if (-not $OutputDir) {
    $OutputDir = Join-Path $scriptDir "dist"
}

# 查找 javac
$javac = $null
if ($env:JAVA_HOME) {
    $javacPath = Join-Path $env:JAVA_HOME "bin\javac.exe"
    if (Test-Path $javacPath) {
        $javac = $javacPath
    }
}
if (-not $javac) {
    $javac = Get-Command javac -ErrorAction SilentlyContinue
    if ($javac) {
        $javac = $javac.Source
    }
}
if (-not $javac) {
    Write-Error "未找到 javac。请设置 JAVA_HOME 或将 javac 加入 PATH。"
    exit 1
}

Write-Host "使用 javac: $javac"

# 清理并创建目录
if (Test-Path $buildDir) { Remove-Item $buildDir -Recurse -Force }
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
New-Item -ItemType Directory -Path $classesDir -Force | Out-Null

# 编译
Write-Host "编译 Java 源码..."
$sourceFiles = Get-ChildItem -Path $srcDir -Recurse -Filter "*.java" | ForEach-Object { $_.FullName }
& $javac -source 8 -target 8 -d $classesDir $sourceFiles
if ($LASTEXITCODE -ne 0) {
    Write-Error "javac 编译失败"
    exit 1
}

# 打包
$jarName = "jn-bridge.jar"
$jarPath = Join-Path $OutputDir $jarName
Write-Host "打包为 $jarPath"

$jar = $null
if ($env:JAVA_HOME) {
    $jarPath2 = Join-Path $env:JAVA_HOME "bin\jar.exe"
    if (Test-Path $jarPath2) {
        $jar = $jarPath2
    }
}
if (-not $jar) {
    $jar = (Get-Command jar -ErrorAction SilentlyContinue).Source
}
if (-not $jar) {
    Write-Error "未找到 jar 工具"
    exit 1
}

# 创建 MANIFEST.MF
$manifestDir = Join-Path $buildDir "META-INF"
New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null
$manifestPath = Join-Path $manifestDir "MANIFEST.MF"
@"
Manifest-Version: 1.0
Built-By: Jvm.NET build script
Implementation-Title: Jvm.NET Java Bridge
Implementation-Version: 2.0.0
"@ | Set-Content -Path $manifestPath -Encoding ASCII

& $jar cfm $jarPath $manifestPath -C $classesDir "."
if ($LASTEXITCODE -ne 0) {
    Write-Error "jar 打包失败"
    exit 1
}

Write-Host "构建完成: $jarPath"
