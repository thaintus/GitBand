using GitBinder.Domain.Bindings;

namespace GitBinder.Application.Bindings;

/// <summary>
/// 绑定仓储接口。
/// </summary>
public interface IBindingRepository
{
    Task<Binding?> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    Task<IReadOnlyList<Binding>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Binding>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);

    Task<int> CountByAccountIdAsync(Guid accountId, CancellationToken ct = default);

    Task AddAsync(Binding binding, CancellationToken ct = default);

    Task UpdateAsync(Binding binding, CancellationToken ct = default);

    Task DeleteByProjectIdAsync(Guid projectId, CancellationToken ct = default);
}