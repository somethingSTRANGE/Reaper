using Microsoft.Data.Sqlite;

namespace Reaper.Db;

public sealed class ReaperDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public ReaperDb(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        EnsureSchema();
    }

    public IReadOnlyList<Entry> GetAll()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT path, first_seen, updated_at FROM entries";
        using var reader = cmd.ExecuteReader();
        var entries = new List<Entry>();
        while (reader.Read())
            entries.Add(new Entry(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2)));
        return entries;
    }

    public void Upsert(IEnumerable<Entry> entries)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO entries (path, first_seen, updated_at)
            VALUES ($path, $first_seen, $updated_at)
            ON CONFLICT(path) DO UPDATE SET
                first_seen = excluded.first_seen,
                updated_at = excluded.updated_at
            """;
        var pPath      = cmd.Parameters.Add("$path",       SqliteType.Text);
        var pFirstSeen = cmd.Parameters.Add("$first_seen", SqliteType.Integer);
        var pUpdatedAt = cmd.Parameters.Add("$updated_at", SqliteType.Integer);

        foreach (var e in entries)
        {
            pPath.Value      = e.Path;
            pFirstSeen.Value = e.FirstSeen;
            pUpdatedAt.Value = e.UpdatedAt;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void Delete(IEnumerable<string> paths)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM entries WHERE path = $path";
        var pPath = cmd.Parameters.Add("$path", SqliteType.Text);

        foreach (var path in paths)
        {
            pPath.Value = path;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public int Touch(string relativePath, long nowSeconds)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE entries
            SET first_seen = $now, updated_at = $now
            WHERE path = $path OR path GLOB $glob
            """;
        cmd.Parameters.AddWithValue("$now",  nowSeconds);
        cmd.Parameters.AddWithValue("$path", relativePath);
        cmd.Parameters.AddWithValue("$glob", relativePath + "/*");
        var rows = cmd.ExecuteNonQuery();
        tx.Commit();
        return rows;
    }

    public void Dispose() => _connection.Dispose();

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS entries (
                path       TEXT    PRIMARY KEY,
                first_seen INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }
}