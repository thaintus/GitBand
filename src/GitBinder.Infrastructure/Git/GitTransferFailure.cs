using System.Globalization;
using GitBinder.Application.Common;
using GitBinder.Domain.Common;

namespace GitBinder.Infrastructure.Git;

/// <summary>把 Git 诊断归类为固定文案，不回显可能带凭据的 URL、认证头或任意服务端原文。</summary>
public static class GitTransferFailure
{
    public static Result FromCommand(string stage, CommandResult result, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return Result.Failure(new DomainError("TRANSFER_CANCELLED"));
        var error = result.StdErr;
        bool Has(string value) => error.Contains(value, StringComparison.OrdinalIgnoreCase);
        var code = Has("Can't open user config file") ? "TRANSFER_SSH_CONFIG_FAILED"
            : Has("REMOTE HOST IDENTIFICATION HAS CHANGED") || Has("Host key verification failed") ? "TRANSFER_HOST_KEY_FAILED"
            : Has("Permission denied (publickey") || Has("Authentication failed") || Has("could not read Username")
                || Has("HTTP Basic: Access denied") ? "TRANSFER_AUTH_FAILED"
            : Has("Could not resolve host") || Has("Could not resolve hostname") ? "TRANSFER_DNS_FAILED"
            : Has("Connection refused") || Has("Failed to connect") || Has("Network is unreachable") ? "TRANSFER_CONNECT_FAILED"
            : Has("timed out") || Has("timeout") ? "TRANSFER_TIMEOUT"
            : Has("SSL certificate problem") || Has("certificate verify failed") || Has("schannel:") ? "TRANSFER_CERTIFICATE_FAILED"
            : Has("detected dubious ownership") ? "TRANSFER_OWNERSHIP_FAILED"
            : Has("Not possible to fast-forward") || Has("Diverging branches") ? "TRANSFER_DIVERGED"
            : Has("would be overwritten") || Has("Please commit your changes or stash") ? "PULL_DIRTY"
            : Has("cannot lock ref") || Has("index.lock") ? "TRANSFER_LOCK_FAILED"
            : Has("not found") && (Has("ssh") || Has("gitbinder-credential")) ? "TRANSFER_PROCESS_MISSING"
            : Has("repository not found") || Has("does not appear to be a git repository") ? "TRANSFER_REMOTE_MISSING"
            : Has("Permission denied") || Has("Access is denied") ? "TRANSFER_ACCESS_FAILED"
            : "TRANSFER_COMMAND_FAILED";
        return Result.Failure(new DomainError(code, new[] { stage, result.ExitCode.ToString(CultureInfo.InvariantCulture) }));
    }

    public static Result FromException(string stage, Exception error, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return Result.Failure(new DomainError("TRANSFER_CANCELLED"));
        var code = error is UnauthorizedAccessException ? "TRANSFER_ACCESS_FAILED"
            : error is IOException ? "TRANSFER_LOCAL_IO_FAILED" : "TRANSFER_COMMAND_FAILED";
        return Result.Failure(new DomainError(code, new[] { stage, "-1" }));
    }
}
