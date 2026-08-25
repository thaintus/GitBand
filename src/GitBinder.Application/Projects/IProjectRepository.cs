using GitBinder.Domain.Projects;

namespace GitBinder.Application.Projects;

/// <summary>
/// 项目仓储接口。
/// </summary>
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default);

    Task<Project?> FindByPathAsync(string canonicalPath, CancellationToken ct = default);

    Task AddAsync(Project project, CancellationToken ct = default);

    Task UpdateAsync(Project project, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}