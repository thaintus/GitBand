namespace GitBinder.Infrastructure.Git;

/// <summary>真实的空配置文件，兼容 Windows OpenSSH 与 Git 自带 OpenSSH，不依赖 NUL 或 /dev/null。</summary>
internal sealed class EmptySshConfig : IDisposable
{
    private readonly FileStream _file;
    public string Path { get; }

    public EmptySshConfig()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"gitbinder-ssh-{Guid.NewGuid():N}.conf");
        // 先创建再只读持有：Windows OpenSSH 的读取模式会拒绝已有写句柄。
        // 不使用 DeleteOnClose，避免要求 SSH 以 FILE_SHARE_DELETE 打开文件。
        using (new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        _file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public void Dispose()
    {
        _file.Dispose();
        // 仅清理本实例创建的唯一空文件；清理失败不覆盖真实 Git 操作结果。
        try { File.Delete(Path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
