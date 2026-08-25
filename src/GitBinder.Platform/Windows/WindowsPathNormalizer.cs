using GitBinder.Application.Common;

namespace GitBinder.Platform.Windows;

/// <summary>
/// Windows 路径规范化实现。Windows 文件系统默认大小写不敏感。
/// </summary>
public sealed class WindowsPathNormalizer : IPathNormalizer
{
    public string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('/', Path.DirectorySeparatorChar);
        normalized = normalized.TrimEnd(Path.DirectorySeparatorChar);
        // 保留盘符根路径自带的反斜杠。
        if (normalized.Length == 2 && normalized[1] == ':')
        {
            normalized += Path.DirectorySeparatorChar;
        }

        return normalized;
    }

    public string Canonicalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var full = Path.GetFullPath(path);
        return Normalize(full);
    }

    public bool Equals(string left, string right)
        => string.Equals(ToKey(left), ToKey(right), StringComparison.OrdinalIgnoreCase);

    public string ToKey(string path)
    {
        var canonical = Canonicalize(path);
        return canonical.ToUpperInvariant();
    }
}