namespace GitBinder.Application.Settings;

/// <summary>
/// 仓储接口，用于 settings 键值存储。
/// </summary>
public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}