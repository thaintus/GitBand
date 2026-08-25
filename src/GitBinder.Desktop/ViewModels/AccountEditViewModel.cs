using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitBinder.Application.Accounts;
using GitBinder.Application.Common;
using GitBinder.Domain.Accounts;

namespace GitBinder.Desktop.ViewModels;

/// <summary>
/// 账号编辑窗口 ViewModel（三块设计）：
/// 1. Git 信息：提交姓名、提交邮箱、别名（默认提交姓名）
/// 2. HTTPS 账密：账号、密码（可选，留空不启用）
/// 3. SSH 私钥：上传私钥
/// 认证方式由填写内容自动推断。
/// </summary>
public partial class AccountEditViewModel : ViewModelBase
{
    private readonly AccountService _accountService;
    private readonly PlatformService _platformService;
    private readonly ILocalizationService _localization;
    private readonly FilePickerDelegate _filePicker;

    private Guid? _editingId;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    // ===== 块 1：Git 信息 =====
    [ObservableProperty]
    private string _gitName = string.Empty;

    [ObservableProperty]
    private string _gitEmail = string.Empty;

    [ObservableProperty]
    private string _alias = string.Empty;

    [ObservableProperty]
    private PlatformOption? _selectedPlatform;

    // ===== 块 2：HTTPS 账密 =====
    [ObservableProperty]
    private string _httpsUsername = string.Empty;

    [ObservableProperty]
    private string _httpsCredential = string.Empty;

    // ===== 块 3：SSH 私钥 =====
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKey))]
    private string _sshPrivateKeyPath = string.Empty;

    [ObservableProperty]
    private string _sshKeyFileName = string.Empty;

    /// <summary>是否已上传私钥（控制"移除"按钮显示）。</summary>
    public bool HasKey => !string.IsNullOrWhiteSpace(SshPrivateKeyPath);

    [ObservableProperty]
    private string _error = string.Empty;

    public ObservableCollection<PlatformOption> Platforms { get; } = [];

    /// <summary>保存成功后返回 true。</summary>
    public bool Saved { get; private set; }

    /// <summary>请求关闭窗口（保存成功时触发）。</summary>
    public event Action? CloseRequested;

    public AccountEditViewModel(
        AccountService accountService,
        PlatformService platformService,
        ILocalizationService localization,
        FilePickerDelegate filePicker)
    {
        _accountService = accountService;
        _platformService = platformService;
        _localization = localization;
        _filePicker = filePicker;
    }

    /// <summary>加载平台目录（每次打开窗口时调用，仅启用的平台）。</summary>
    public async Task LoadPlatformsAsync()
    {
        var all = await _platformService.GetAllAsync();
        Platforms.Clear();
        foreach (var p in all.Where(p => p.Enabled))
        {
            Platforms.Add(new PlatformOption(p.Id, p.Name, p.Host));
        }
    }

    /// <summary>初始化新建。</summary>
    public void InitializeForCreate()
    {
        _editingId = null;
        WindowTitle = _localization.GetString("Accounts.Edit.New");
        GitName = string.Empty;
        GitEmail = string.Empty;
        Alias = string.Empty;
        HttpsUsername = string.Empty;
        HttpsCredential = string.Empty;
        SshPrivateKeyPath = string.Empty;
        SshKeyFileName = string.Empty;
        SelectedPlatform = Platforms.Count > 0 ? Platforms[0] : null;
        Error = string.Empty;
        Saved = false;
    }

    /// <summary>初始化编辑。</summary>
    public void InitializeForEdit(Account account)
    {
        _editingId = account.Id;
        WindowTitle = _localization.GetString("Accounts.Edit.Title");
        GitName = account.GitName;
        GitEmail = account.GitEmail;
        Alias = account.Alias;
        HttpsUsername = account.HttpsUsername;
        HttpsCredential = string.Empty;
        SshPrivateKeyPath = account.SshPrivateKeyPath;
        SshKeyFileName = string.IsNullOrEmpty(account.SshPrivateKeyPath)
            ? string.Empty
            : Path.GetFileName(account.SshPrivateKeyPath);
        SelectedPlatform = FindPlatform(account.PlatformId, account.PlatformName);
        Error = string.Empty;
        Saved = false;
    }

    /// <summary>上传 SSH 私钥：选择文件并导入到应用 keys 目录。</summary>
    [RelayCommand]
    private async Task UploadKeyAsync()
    {
        var picked = await _filePicker();
        if (string.IsNullOrWhiteSpace(picked))
        {
            return;
        }

        var result = await _accountService.ImportPrivateKeyAsync(picked);
        if (result.IsSuccess)
        {
            SshPrivateKeyPath = result.Value!;
            SshKeyFileName = Path.GetFileName(result.Value!);
            Error = string.Empty;
        }
        else
        {
            Error = _localization.GetString(result.Error!.Code, result.Error.Arguments);
        }
    }

    /// <summary>经确认后移除当前表单中的已上传私钥。</summary>
    [RelayCommand]
    private async Task ClearKeyAsync()
    {
        var confirmed = await DialogHelper.ConfirmAsync(
            _localization.GetString("Common.Confirm"),
            _localization.GetString("Accounts.ClearKey.Confirm"),
            _localization.GetString("Common.Delete"));
        if (!confirmed)
        {
            return;
        }

        SshPrivateKeyPath = string.Empty;
        SshKeyFileName = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Error = string.Empty;

        // 别名默认提交姓名。
        if (string.IsNullOrWhiteSpace(Alias))
        {
            Alias = GitName.Trim();
        }

        // 校验：提交姓名必填。
        if (string.IsNullOrWhiteSpace(GitName))
        {
            Error = _localization.GetString("ACCOUNT_NAME_REQUIRED");
            return;
        }

        // 自动推断认证方式。
        var hasHttps = !string.IsNullOrWhiteSpace(HttpsUsername) && !string.IsNullOrWhiteSpace(HttpsCredential);
        var hasSsh = !string.IsNullOrWhiteSpace(SshPrivateKeyPath);
        var authType = (hasHttps, hasSsh) switch
        {
            (true, true) => AuthenticationType.Both,
            (true, false) => AuthenticationType.Https,
            (false, true) => AuthenticationType.Ssh,
            _ => AuthenticationType.None,
        };

        // 平台 → Host。
        var platformId = SelectedPlatform?.Id;
        var platformName = SelectedPlatform?.Name ?? string.Empty;
        var host = SelectedPlatform?.Host ?? string.Empty;

        // HTTPS 账号作为平台用户名。
        var username = string.IsNullOrWhiteSpace(HttpsUsername) ? GitName.Trim() : HttpsUsername.Trim();

        if (_editingId is Guid id)
        {
            var update = new UpdateAccountInput
            {
                Id = id,
                Username = username,
                Alias = Alias.Trim(),
                PlatformId = platformId,
                PlatformName = platformName,
                Host = host,
                GitName = GitName.Trim(),
                GitEmail = GitEmail.Trim(),
                AuthenticationType = authType,
                SshPrivateKeyPath = SshPrivateKeyPath.Trim(),
                HttpsUsername = HttpsUsername.Trim(),
                HttpsCredential = HttpsCredential,
            };
            var result = await _accountService.UpdateAsync(update);
            if (!result.IsSuccess)
            {
                Error = _localization.GetString(result.Error!.Code, result.Error.Arguments);
                return;
            }
        }
        else
        {
            var create = new CreateAccountInput
            {
                Username = username,
                Alias = Alias.Trim(),
                PlatformId = platformId,
                PlatformName = platformName,
                Host = host,
                GitName = GitName.Trim(),
                GitEmail = GitEmail.Trim(),
                AuthenticationType = authType,
                SshPrivateKeyPath = SshPrivateKeyPath.Trim(),
                HttpsUsername = HttpsUsername.Trim(),
                HttpsCredential = HttpsCredential,
            };
            var result = await _accountService.CreateAsync(create);
            if (!result.IsSuccess)
            {
                Error = _localization.GetString(result.Error!.Code, result.Error.Arguments);
                return;
            }
        }

        Saved = true;
        CloseRequested?.Invoke();
    }

    private PlatformOption? FindPlatform(Guid? platformId, string platformName)
    {
        if (platformId is Guid id)
        {
            var byId = Platforms.FirstOrDefault(p => p.Id == id);
            if (byId is not null)
            {
                return byId;
            }
        }

        return Platforms.FirstOrDefault(p => p.Name == platformName);
    }
}

/// <summary>平台选项（来自平台目录）。</summary>
public sealed class PlatformOption
{
    public Guid Id { get; }

    public string Name { get; }

    public string Host { get; }

    public PlatformOption(Guid id, string name, string host)
    {
        Id = id;
        Name = name;
        Host = host;
    }

    public override string ToString() => Name;
}
