namespace GitBinder.Application.Security;

/// <summary>
/// SSH 服务定位器。
/// </summary>
public interface ISshLocator
{
    Task<string?> LocateAsync(CancellationToken ct = default);
}

/// <summary>
/// 机密存储抽象。各平台差异实现位于 Platform 层。
/// </summary>
public interface ISecretStore
{
    Task SetAsync(string key, string value, CancellationToken ct = default);

    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}