using GitBinder.Domain.Common;
using GitBinder.Domain.Projects;

namespace GitBinder.Application.Projects;

public sealed class ProjectGroupService(IProjectGroupRepository repository)
{
    public Task<IReadOnlyList<ProjectGroup>> GetAllAsync() => repository.GetAllAsync();
    public Task<IReadOnlyDictionary<Guid, Guid>> GetMembershipsAsync() => repository.GetMembershipsAsync();

    public Task<Result> SaveAsync(Guid? id, string name) => GuardAsync(async () =>
    {
        name = name.Trim();
        if (name.Length is 0 or > 60) return Result.Failure(new DomainError("GROUP_NAME_INVALID"));
        if ((await repository.GetAllAsync()).Any(g => g.Id != id && string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Result.Failure(new DomainError("GROUP_NAME_EXISTS"));
        return await repository.SaveAsync(new ProjectGroup(id ?? Guid.NewGuid(), name), id is null);
    });

    public Task<Result> DeleteAsync(Guid id) => GuardAsync(() => repository.DeleteAsync(id));
    public Task<Result> AssignAsync(Guid projectId, Guid? groupId) => GuardAsync(() => repository.AssignAsync(projectId, groupId));

    private static async Task<Result> GuardAsync(Func<Task<Result>> action)
    {
        try { return await action(); }
        catch (Exception) { return Result.Failure(new DomainError("GROUP_SAVE_FAILED")); }
    }
}
