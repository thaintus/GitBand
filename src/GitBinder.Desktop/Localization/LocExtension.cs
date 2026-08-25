using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace GitBinder.Desktop.Localization;

/// <summary>
/// 本地化标记扩展，XAML 中使用 {loc:Loc Key=xxx} 绑定动态文案。
/// 语言切换后通过 LocalizationService 的索引器通知自动刷新。
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public LocExtension() { }

    public LocExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // 绑定到 LocalizationService 实例的索引器，实现 OneWay 动态刷新。
        return new Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
        };
    }
}