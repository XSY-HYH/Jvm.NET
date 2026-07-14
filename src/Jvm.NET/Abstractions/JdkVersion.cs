namespace Jvm.NET.Abstractions;

/// <summary>
/// Supported JDK versions. Each version maps to a dedicated abstraction layer
/// under <c>Abstractions/jdkXX</c> that handles version-specific differences.
/// </summary>
public enum JdkVersion
{
    /// <summary>
    /// OpenJDK 21 (LTS). The first version supported by Jvm.NET.
    /// </summary>
    Jdk21,
}
