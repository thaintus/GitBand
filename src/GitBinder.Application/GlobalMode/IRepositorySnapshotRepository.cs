using GitBinder.Domain.GlobalMode;

namespace GitBinder.Application.GlobalMode;

/// <summary>
/// 仓库快照仓储接口。
/// </summary>
public interface IRepositorySnapshotRepository
{
    Task<RepositorySnapshot?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    Task SaveAsync(RepositorySnapshot snapshot, CancellationToken ct = default);

    Task DeleteByProjectIdAsync(Guid projectId, CancellationToken ct = default);
}