using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using GitBinder.Application.Security;

namespace GitBinder.Platform.Windows;

/// <summary>
/// 基于 DPAPI (CurrentUser) 的机密存储。
/// 密文写入磁盘，明文永不落盘。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDpapiSecretStore : ISecretStore
{
    private readonly string _storageDirectory;

    public WindowsDpapiSecretStore(string storageDirectory)
    {
        _storageDirectory = storageDirectory;
    }

    public Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_storageDirectory);
        var path = GetPath(key);

        var plainBytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);

        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var path = GetPath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<string?>(null);
        }

        var protectedBytes = File.ReadAllBytes(path);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return Task.FromResult<string?>(Encoding.UTF8.GetString(plainBytes));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = GetPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string key)
    {
        // key 形如 gitbinder/account/<id>/https，作为子路径。
        var safe = key.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_storageDirectory, safe + ".bin");
    }
}
