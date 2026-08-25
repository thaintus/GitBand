using GitBinder.Application.Security;

namespace GitBinder.Application.Security;

/// <summary>
/// Secret 应用服务：为账号 HTTPS Credential / SSH Passphrase 生成 Secret Id 并读写机密。
/// </summary>
public sealed class SecretService
{
    private readonly ISecretStore _secretStore;

    public SecretService(ISecretStore secretStore)
    {
        _secretStore = secretStore;
    }

    /// <summary>生成标准 Secret Id。</summary>
    public static string BuildSecretId(Guid accountId, string kind)
        => $"gitbinder/account/{accountId}/{kind}";

    public Task SetAsync(string secretId, string value, CancellationToken ct = default)
        => _secretStore.SetAsync(secretId, value, ct);

    public Task<string?> GetAsync(string secretId, CancellationToken ct = default)
        => _secretStore.GetAsync(secretId, ct);

    public Task DeleteAsync(string secretId, CancellationToken ct = default)
        => _secretStore.DeleteAsync(secretId, ct);
}