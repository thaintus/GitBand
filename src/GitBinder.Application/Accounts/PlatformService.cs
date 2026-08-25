using GitBinder.Application.Settings;
using GitBinder.Domain.Accounts;
using GitBinder.Domain.Common;

namespace GitBinder.Application.Accounts;

/// <summary>
/// 平台目录应用服务：维护平台名称与 Host，供账号页平台下拉使用。
/// 内置平台与自建平台地位完全相同，可自由编辑、删除，无任何保护。
/// </summary>
public sealed class PlatformService
{
    private const string SeededKey = "platforms.seeded";

    private readonly IPlatformRepository _repository;
    private readonly ISettingsRepository _settings;

    public PlatformService(IPlatformRepository repository, ISettingsRepository settings)
    {
        _repository = repository;
        _settings = settings;
    }

    public Task<IReadOnlyList<GitPlatform>> GetAllAsync(CancellationToken ct = default)
        => _repository.GetAllAsync(ct);

    /// <summary>创建平台，名称唯一。</summary>
    public async Task<Result<GitPlatform>> CreateAsync(string name, string host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<GitPlatform>.Failure(new DomainError("PLATFORM_NAME_REQUIRED"));
        }

        var dup = await _repository.GetByNameAsync(name.Trim(), ct);
        if (dup is not null)
        {
            return Result<GitPlatform>.Failure(new DomainError("PLATFORM_NAME_EXISTS", name.Trim()));
        }

        var platform = new GitPlatform
        {
            Name = name.Trim(),
            Host = host?.Trim() ?? string.Empty,
        };

        await _repository.AddAsync(platform, ct);
        return Result<GitPlatform>.Success(platform);
    }

    /// <summary>更新平台。</summary>
    public async Task<Result<GitPlatform>> UpdateAsync(Guid id, string name, string host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<GitPlatform>.Failure(new DomainError("PLATFORM_NAME_REQUIRED"));
        }

        var platform = await _repository.GetByIdAsync(id, ct);
        if (platform is null)
        {
            return Result<GitPlatform>.Failure(new DomainError("PLATFORM_NOT_FOUND", id.ToString()));
        }

        var dup = await _repository.GetByNameAsync(name.Trim(), ct);
        if (dup is not null && dup.Id != id)
        {
            return Result<GitPlatform>.Failure(new DomainError("PLATFORM_NAME_EXISTS", name.Trim()));
        }

        platform.Name = name.Trim();
        platform.Host = host?.Trim() ?? string.Empty;
        platform.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(platform, ct);
        return Result<GitPlatform>.Success(platform);
    }

    /// <summary>删除平台（内置平台同样可删；账号引用将保留名称快照）。</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        return Result.Success();
    }

    /// <summary>启用/禁用平台。禁用后新建账号时不展示。</summary>
    public async Task<Result> SetEnabledAsync(Guid id, bool enabled, CancellationToken ct = default)
    {
        var platform = await _repository.GetByIdAsync(id, ct);
        if (platform is null)
        {
            return Result.Failure(new DomainError("PLATFORM_NOT_FOUND", id.ToString()));
        }

        platform.Enabled = enabled;
        platform.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.UpdateAsync(platform, ct);
        return Result.Success();
    }

    /// <summary>首次运行时灌入内置默认平台（仅一次，之后不再自动恢复）。</summary>
    public async Task SeedDefaultsAsync(CancellationToken ct = default)
    {
        var seeded = await _settings.GetAsync(SeededKey, ct);
        if (seeded is not null)
        {
            return; // 已初始化过，尊重用户的增删改，不再自动灌入。
        }

        await AddDefaultsAsync(ct);
        await _settings.SetAsync(SeededKey, "true", ct);
    }

    /// <summary>显式恢复内置默认平台（跳过已存在的名称）。</summary>
    public async Task RestoreDefaultsAsync(CancellationToken ct = default)
    {
        await AddDefaultsAsync(ct);
    }

    private async Task AddDefaultsAsync(CancellationToken ct)
    {
        var defaults = new (string Name, string Host)[]
        {
            ("GitHub", "github.com"),
            ("Gitee", "gitee.com"),
            ("GitLab", ""),
            ("阿里云云效 Codeup", "codeup.aliyun.com"),
            ("腾讯云代码托管", "git.cloud.tencent.com"),
        };

        var existing = await _repository.GetAllAsync(ct);
        var maxOrder = existing.Count > 0 ? existing.Max(p => p.SortOrder) + 1 : 0;

        foreach (var (name, host) in defaults)
        {
            if (existing.Any(p => p.Name == name))
            {
                continue; // 已存在则不重复添加。
            }

            await _repository.AddAsync(new GitPlatform { Name = name, Host = host, SortOrder = maxOrder++ }, ct);
        }
    }
}