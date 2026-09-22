namespace GitBinder.Domain.Projects;

/// <summary>传输入口只接受明确的 HTTPS/SSH 地址，拒绝嵌入密码及任意协议命令。</summary>
public static class GitTransferRemote
{
    public static bool TryParse(string value, out RemoteProtocol protocol)
    {
        protocol = RemoteProtocol.Unknown;
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl)) return false;
        value = value.Trim();
        if (value.StartsWith("git@", StringComparison.Ordinal))
        {
            var colon = value.IndexOf(':');
            if (colon <= 4 || colon == value.Length - 1 || value.Any(char.IsWhiteSpace)
                || value.Contains('\\')) return false;
            var host = value[4..colon];
            if (Uri.CheckHostName(host) == UriHostNameType.Unknown) return false;
            protocol = RemoteProtocol.Ssh;
            return true;
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)
            || uri.AbsolutePath is "" or "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        if (uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo))
            protocol = RemoteProtocol.Https;
        else if (uri.Scheme == "ssh" && !uri.UserInfo.Contains(':') && !Uri.UnescapeDataString(uri.UserInfo).Contains(':'))
            protocol = RemoteProtocol.Ssh;
        return protocol is not RemoteProtocol.Unknown;
    }
}
