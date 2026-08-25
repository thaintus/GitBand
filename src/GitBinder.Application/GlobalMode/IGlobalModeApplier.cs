using GitBinder.Domain.Accounts;
using GitBinder.Domain.Common;

namespace GitBinder.Application.GlobalMode;

/// <summary>
/// 将全局账号实际应用到已绑定仓库，或在关闭全局模式时恢复各自的绑定账号。
/// </summary>
public interface IGlobalModeApplier
{
    Task<Result> ApplyGlobalAccountAsync(Account account, CancellationToken ct = default);

    Task<Result> RestoreBoundAccountsAsync(CancellationToken ct = default);
}
