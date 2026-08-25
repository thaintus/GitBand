using GitBinder.Domain.Accounts;

namespace GitBinder.Application.Accounts;

/// <summary>
/// 账号仓储接口。
/// </summary>
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Account?> GetDefaultAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(Account account, CancellationToken ct = default);

    Task UpdateAsync(Account account, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}