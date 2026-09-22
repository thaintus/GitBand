using GitBinder.Domain.Accounts;
using GitBinder.Domain.Common;

namespace GitBinder.Application.Git;

/// <summary>支持只读连接测试、克隆和快进拉取，不处理提交、推送、冲突或自动变基。</summary>
public interface IGitTransfer
{
    /// <summary>读取实际 origin，按当前账号执行只读、非交互的连接测试，不修改仓库配置。</summary>
    Task<Result> TestAsync(string repositoryPath, Account account, CancellationToken ct = default);
    Task<Result> CloneAsync(string remoteUrl, string destination, Account account, CancellationToken ct = default);
    Task<Result> PullAsync(string repositoryPath, Account account, CancellationToken ct = default);
}
