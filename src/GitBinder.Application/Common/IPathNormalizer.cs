namespace GitBinder.Application.Common;

/// <summary>
/// 路径规范化服务，统一处理跨平台路径比较。
/// </summary>
public interface IPathNormalizer
{
    /// <summary>规范化路径（分隔符、尾部分隔符）。</summary>
    string Normalize(string path);

    /// <summary>解析为绝对、规范路径。</summary>
    string Canonicalize(string path);

    /// <summary>路径比较（遵循平台大小写规则）。</summary>
    bool Equals(string left, string right);

    /// <summary>规范化后的 Hash，用于匹配。</summary>
    string ToKey(string path);
}