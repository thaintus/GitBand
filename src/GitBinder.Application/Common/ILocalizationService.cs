using System.Globalization;

namespace GitBinder.Application.Common;

/// <summary>
/// 本地化服务，所有用户可见文本统一经过此服务。
/// </summary>
public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }

    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    string GetString(string key);

    string GetString(string key, params object[] args);

    event EventHandler? CultureChanged;

    Task SetCultureAsync(CultureInfo culture, CancellationToken ct = default);

    /// <summary>为本地化目的格式化资源键（等价于 GetString(key, args)）。</summary>
    string Format(string key, params object[] args) => GetString(key, args);
}