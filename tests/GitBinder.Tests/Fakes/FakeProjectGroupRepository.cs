using GitBinder.Application.Projects;
using GitBinder.Domain.Common;
using GitBinder.Domain.Projects;

namespace GitBinder.Tests.Fakes;

public sealed class FakeProjectGroupRepository : IProjectGroupRepository
{
    public Dictionary<Guid, ProjectGroup> Groups { get; } = [];
    public Dictionary<Guid, Guid> Memberships { get; } = [];
    public Task<IReadOnlyList<ProjectGroup>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProjectGroup>>(Groups.Values.ToList());
    public Task<IReadOnlyDictionary<Guid, Guid>> GetMembershipsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(new Dictionary<Guid, Guid>(Memberships));
    public Task<Result> SaveAsync(ProjectGroup group, bool create, CancellationToken ct = default)
    { Groups[group.Id] = group; return Task.FromResult(Result.Success()); }
    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Groups.Remove(id);
        foreach (var project in Memberships.Where(m => m.Value == id).Select(m => m.Key).ToList()) Memberships.Remove(project);
        return Task.FromResult(Result.Success());
    }
    public Task<Result> AssignAsync(Guid projectId, Guid? groupId, CancellationToken ct = default)
    {
        if (groupId.HasValue) Memberships[projectId] = groupId.Value;
        else Memberships.Remove(projectId);
        return Task.FromResult(Result.Success());
    }
}
