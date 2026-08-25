namespace GitBinder.Domain.Accounts;

/// <summary>
/// 账号工厂，负责创建账号并保证 Alias 默认值规则。
/// </summary>
public static class AccountFactory
{
    public static Account Create(
        string username,
        string alias = "",
        Guid? platformId = null,
        string platformName = "",
        string host = "",
        string gitName = "",
        string gitEmail = "")
    {
        var normalizedAlias = string.IsNullOrWhiteSpace(alias) ? username : alias.Trim();
        return new Account
        {
            Username = username?.Trim() ?? string.Empty,
            Alias = normalizedAlias,
            PlatformId = platformId,
            PlatformName = platformName?.Trim() ?? string.Empty,
            Host = host?.Trim() ?? string.Empty,
            GitName = gitName?.Trim() ?? string.Empty,
            GitEmail = gitEmail?.Trim() ?? string.Empty,
        };
    }
}