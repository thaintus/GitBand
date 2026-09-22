using GitBinder.Domain.Common;
using GitBinder.Domain.Projects;

namespace GitBinder.Application.Projects;

public interface IProjectGroupRepository
{
    Task<IReadOnlyList<ProjectGroup>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, Guid>> GetMembershipsAsync(CancellationToken ct = default);
    Task<Result> SaveAsync(ProjectGroup group, bool create, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result> AssignAsync(Guid projectId, Guid? groupId, CancellationToken ct = default);
}
