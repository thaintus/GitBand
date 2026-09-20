namespace GitBinder.Desktop.ViewModels;

/// <summary>已加载列表的本地包含匹配；不区分大小写，不解释正则或通配符。</summary>
internal static class ListSearch
{
    public static bool Matches(string? searchText, params string?[] fields)
    {
        var query = searchText?.Trim();
        return string.IsNullOrEmpty(query)
            || fields.Any(field => field?.Contains(query, StringComparison.OrdinalIgnoreCase) is true);
    }
}
