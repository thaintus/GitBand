using Microsoft.Data.Sqlite;

namespace GitBinder.Infrastructure.Persistence;

/// <summary>
/// SQLite 数据库上下文，负责打开连接、初始化 Schema 与迁移。
/// </summary>
public sealed class DatabaseContext
{
    private readonly string _databasePath;

    public DatabaseContext(string databasePath)
    {
        _databasePath = databasePath;
    }

    public string DatabasePath => _databasePath;

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        return connection;
    }

    /// <summary>初始化数据库结构（幂等）。</summary>
    public void EnsureCreated()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = Schema.Sql;
        command.ExecuteNonQuery();
    }
}