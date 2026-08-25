using System.ComponentModel;
using System.Globalization;
using GitBinder.Application.Common;

namespace GitBinder.Desktop.Localization;

/// <summary>
/// 本地化服务实现。支持 zh-CN 与 en-US 动态切换。
/// 通过实现 INotifyPropertyChanged 的索引器，使 XAML 绑定能在语言切换后自动刷新。
/// </summary>
public sealed class LocalizationService : ILocalizationService, INotifyPropertyChanged
{
    public static LocalizationService Instance { get; private set; } = null!;

    private static readonly CultureInfo ZhCn = new("zh-CN");
    private static readonly CultureInfo EnUs = new("en-US");

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _resources;
    private readonly Dictionary<string, string> _current;

    public CultureInfo CurrentCulture { get; private set; } = ZhCn;

    public IReadOnlyList<CultureInfo> SupportedCultures { get; } = [ZhCn, EnUs];

    public event EventHandler? CultureChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService()
    {
        _resources = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["zh-CN"] = StringsZhCn.Resources,
            ["en-US"] = StringsEnUs.Resources,
        };
        _current = new Dictionary<string, string>(StringsZhCn.Resources);
        Instance = this;
    }

    /// <summary>索引器，供 XAML 绑定 {Binding [Key]} 使用。</summary>
    public string this[string key] => GetString(key);

    public string GetString(string key)
    {
        if (key is null)
        {
            return string.Empty;
        }

        if (_current.TryGetValue(key, out var value))
        {
            return value;
        }

        // 回退到 zh-CN，再回退到 key。
        if (StringsZhCn.Resources.TryGetValue(key, out var zh))
        {
            return zh;
        }

        return key;
    }

    public string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        return args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
    }

    public Task SetCultureAsync(CultureInfo culture, CancellationToken ct = default)
    {
        var name = culture.Name;
        if (_resources.TryGetValue(name, out var resources))
        {
            CurrentCulture = culture;
            _current.Clear();
            foreach (var kv in resources)
            {
                _current[kv.Key] = kv.Value;
            }
        }
        else if (_resources.TryGetValue("zh-CN", out var zh))
        {
            // 不支持的语言回退 zh-CN。
            CurrentCulture = ZhCn;
            _current.Clear();
            foreach (var kv in zh)
            {
                _current[kv.Key] = kv.Value;
            }
        }

        CultureChanged?.Invoke(this, EventArgs.Empty);
        // 通知所有索引器绑定刷新。
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        return Task.CompletedTask;
    }
}