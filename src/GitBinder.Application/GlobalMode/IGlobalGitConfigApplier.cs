using GitBinder.Domain.Accounts;
using GitBinder.Domain.Common;
using GitBinder.Domain.GlobalMode;

namespace GitBinder.Application.GlobalMode;

/// <summary>Git 全局配置的可恢复快照。</summary>
public sealed record GlobalGitConfigSnapshot(
    string? UserName,
    string? UserEmail,
    string? SshCommand,
    string[]? CredentialHelpers = null,
    GitEmailConfigSnapshot? EmailConfig = null);

/// <summary>读写 Git 用户级配置（git config --global）。</summary>
public interface IGlobalGitConfigApplier
{
    Task<GlobalGitConfigSnapshot> CaptureAsync(CancellationToken ct = default);

    Task<Result> ApplyAsync(Account account, CancellationToken ct = default);

    Task<Result> RestoreAsync(GlobalGitConfigSnapshot snapshot, CancellationToken ct = default);
}
