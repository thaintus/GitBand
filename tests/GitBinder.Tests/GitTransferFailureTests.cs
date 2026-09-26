using GitBinder.Application.Common;
using GitBinder.Infrastructure.Git;

namespace GitBinder.Tests;

public sealed class GitTransferFailureTests
{
    [Theory]
    [InlineData("Can't open user config file NUL: No such file or directory", "TRANSFER_SSH_CONFIG_FAILED")]
    [InlineData("Host key verification failed.", "TRANSFER_HOST_KEY_FAILED")]
    [InlineData("Permission denied (publickey).", "TRANSFER_AUTH_FAILED")]
    [InlineData("Could not resolve hostname host", "TRANSFER_DNS_FAILED")]
    [InlineData("Connection refused", "TRANSFER_CONNECT_FAILED")]
    [InlineData("Command timed out.", "TRANSFER_TIMEOUT")]
    [InlineData("SSL certificate problem: certificate expired", "TRANSFER_CERTIFICATE_FAILED")]
    [InlineData("detected dubious ownership", "TRANSFER_OWNERSHIP_FAILED")]
    [InlineData("Not possible to fast-forward, aborting.", "TRANSFER_DIVERGED")]
    [InlineData("error: Your local changes to the following files would be overwritten by merge:\n\ttracked.txt\nPlease commit your changes or stash them before you merge.\nAborting", "PULL_DIRTY")]
    [InlineData("error: The following untracked working tree files would be overwritten by merge:\n\tlocal.txt\nPlease move or remove them before you merge.\nAborting", "PULL_DIRTY")]
    [InlineData("cannot lock ref", "TRANSFER_LOCK_FAILED")]
    public void CommandFailure_ReportsSpecificReasonAndStage(string error, string code)
    {
        var result = GitTransferFailure.FromCommand("git pull --ff-only", new CommandResult { ExitCode = 128, StdErr = error });
        Assert.Equal(code, result.Error!.Code);
        Assert.Equal(new[] { "git pull --ff-only", "128" }, result.Error.Arguments);
    }

    [Fact]
    public void UnknownFailure_DoesNotExposeRawCredentialsOrServerOutput()
    {
        var result = GitTransferFailure.FromCommand("git clone", new CommandResult
        {
            ExitCode = 128,
            StdErr = "unknown https://user:dummy-password@example.org/repo?token=dummy-token Authorization: Bearer dummy-token",
        });
        Assert.Equal("TRANSFER_COMMAND_FAILED", result.Error!.Code);
        Assert.Null(result.Error.TechnicalDetails);
        Assert.Equal(new[] { "git clone", "128" }, result.Error.Arguments);
    }

    [Fact]
    public void Cancellation_IsNotReportedAsAuthenticationFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = GitTransferFailure.FromCommand("git pull", new CommandResult
            { ExitCode = -1, StdErr = "Permission denied (publickey)." }, cancellation.Token);
        Assert.Equal("TRANSFER_CANCELLED", result.Error!.Code);
    }
}
