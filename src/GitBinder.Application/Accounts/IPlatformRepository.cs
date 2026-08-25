using GitBinder.Domain.Accounts;

namespace GitBinder.Application.Accounts;

/// <summary>
/// 平台目录仓储接口。
/// </summary>
public interface IPlatformRepository
{
    Task<GitPlatform?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<GitPlatform?> GetByNameAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<GitPlatform>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(GitPlatform platform, CancellationToken ct = default);

    Task UpdateAsync(GitPlatform platform, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}