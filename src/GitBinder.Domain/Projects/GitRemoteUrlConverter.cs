namespace GitBinder.Domain.Projects;

/// <summary>标准 Git SSH/HTTPS Remote 地址之间的纯转换规则。</summary>
public static class GitRemoteUrlConverter
{
    /// <summary>
    /// 在标准 HTTPS 与 SSH Remote 地址之间切换协议。
    /// 不处理带用户信息、查询参数、片段或自定义端口的地址，避免猜测目标服务端口。
    /// </summary>
    public static bool TrySwitchProtocol(
        string originUrl,
        out string convertedUrl,
        out RemoteProtocol convertedProtocol)
    {
        convertedUrl = string.Empty;
        convertedProtocol = RemoteProtocol.Unknown;
        if (string.IsNullOrWhiteSpace(originUrl))
        {
            return false;
        }

        var value = originUrl.Trim();
        if (TryConvertHttpToSsh(value, out convertedUrl))
        {
            convertedProtocol = RemoteProtocol.Ssh;
            return true;
        }

        if (TryConvertSshToHttps(value, out convertedUrl))
        {
            convertedProtocol = RemoteProtocol.Https;
            return true;
        }

        return false;
    }

    private static bool TryConvertHttpToSsh(string value, out string convertedUrl)
    {
        convertedUrl = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            || !uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || uri.Host.Contains(':'))
        {
            return false;
        }

        var repositoryPath = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped).Trim('/');
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return false;
        }

        convertedUrl = $"git@{uri.Host}:{repositoryPath}";
        return true;
    }

    private static bool TryConvertSshToHttps(string value, out string convertedUrl)
    {
        convertedUrl = string.Empty;
        if (value.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || !uri.Scheme.Equals("ssh", StringComparison.OrdinalIgnoreCase)
                || !uri.IsDefaultPort
                || uri.UserInfo.Contains(':')
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment)
                || uri.Host.Contains(':'))
            {
                return false;
            }

            var sshRepositoryPath = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped).Trim('/');
            if (string.IsNullOrWhiteSpace(sshRepositoryPath))
            {
                return false;
            }

            convertedUrl = $"https://{uri.Host}/{sshRepositoryPath}";
            return true;
        }

        if (value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = value.IndexOf('@');
        var colon = value.IndexOf(':', at + 1);
        if (at <= 0 || colon <= at + 1 || colon >= value.Length - 1)
        {
            return false;
        }

        var host = value[(at + 1)..colon];
        var repositoryPath = value[(colon + 1)..].TrimStart('/');
        if (string.IsNullOrWhiteSpace(host)
            || host.Contains('/')
            || host.Contains(':')
            || string.IsNullOrWhiteSpace(repositoryPath)
            || repositoryPath.Contains('?')
            || repositoryPath.Contains('#'))
        {
            return false;
        }

        convertedUrl = $"https://{host}/{repositoryPath}";
        return true;
    }
}
