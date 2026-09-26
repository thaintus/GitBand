using GitBinder.Domain.Projects;

namespace GitBinder.Tests;

public class GitRemoteUrlConverterTests
{
    [Theory]
    [InlineData("https://github.com/org/repository.git", "git@github.com:org/repository.git")]
    [InlineData("http://codeup.aliyun.com/team/repository.git", "git@codeup.aliyun.com:team/repository.git")]
    public void TrySwitchProtocol_HttpToSsh_ConvertsStandardRemote(string originUrl, string expected)
    {
        var converted = GitRemoteUrlConverter.TrySwitchProtocol(originUrl, out var actual, out var protocol);

        Assert.True(converted);
        Assert.Equal(expected, actual);
        Assert.Equal(RemoteProtocol.Ssh, protocol);
    }

    [Theory]
    [InlineData("git@github.com:org/repository.git", "https://github.com/org/repository.git")]
    [InlineData("ssh://git@gitlab.example.com/group/repository.git", "https://gitlab.example.com/group/repository.git")]
    public void TrySwitchProtocol_SshToHttps_ConvertsStandardRemote(string originUrl, string expected)
    {
        var converted = GitRemoteUrlConverter.TrySwitchProtocol(originUrl, out var actual, out var protocol);

        Assert.True(converted);
        Assert.Equal(expected, actual);
        Assert.Equal(RemoteProtocol.Https, protocol);
    }

    [Theory]
    [InlineData("https://user:token@github.com/org/repository.git")]
    [InlineData("https://git.example.com:8443/org/repository.git")]
    [InlineData("ssh://git@git.example.com:2222/org/repository.git")]
    public void TrySwitchProtocol_NonStandardRemote_IsRejected(string originUrl)
    {
        var converted = GitRemoteUrlConverter.TrySwitchProtocol(originUrl, out var actual, out var protocol);

        Assert.False(converted);
        Assert.Equal(string.Empty, actual);
        Assert.Equal(RemoteProtocol.Unknown, protocol);
    }
}
