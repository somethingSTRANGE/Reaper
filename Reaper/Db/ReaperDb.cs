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
        cmd.CommandText = "SELECT path, first_seen, refreshed_at, size FROM entries";
        using var reader = cmd.ExecuteReader();
        var entries = new List<Entry>();
        while (reader.Read())
            entries.Add(new Entry(reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3)));
        return entries;
    }

    // first_seen is deliberately absent from the ON CONFLICT SET clause — it is immutable
    // after insert, enforced here rather than trusting every call site to preserve it.
    public void Upsert(IEnumerable<Entry> entries)
    {
        using var tx = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO entries (path, first_seen, refreshed_at, size)
            VALUES ($path, $first_seen, $refreshed_at, $size)
            ON CONFLICT(path) DO UPDATE SET
                refreshed_at = excluded.refreshed_at,
                size         = excluded.size
            """;
        var pPath        = cmd.Parameters.Add("$path",         SqliteType.Text);
        var pFirstSeen   = cmd.Parameters.Add("$first_seen",   SqliteType.Integer);
        var pRefreshedAt = cmd.Parameters.Add("$refreshed_at", SqliteType.Integer);
        var pSize        = cmd.Parameters.Add("$size",         SqliteType.Integer);

        foreach (var e in entries)
        {
            pPath.Value        = e.Path;
            pFirstSeen.Value   = e.FirstSeen;
            pRefreshedAt.Value = e.RefreshedAt;
            pSize.Value        = e.Size;
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
            SET refreshed_at = $now
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
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS entries (
                    path         TEXT    PRIMARY KEY,
                    first_seen   INTEGER NOT NULL,
                    refreshed_at INTEGER NOT NULL,
                    size         INTEGER NOT NULL DEFAULT 0
                )
                """;
            cmd.ExecuteNonQuery();
        }

        MigrateLegacySchema();
    }

    // Older databases used `updated_at` and had no `size` column. Migrate in place rather
    // than requiring users to re-init and lose their first_seen history.
    private void MigrateLegacySchema()
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(entries)";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                columns.Add(reader.GetString(1));
        }

        if (columns.Contains("updated_at") && !columns.Contains("refreshed_at"))
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE entries RENAME COLUMN updated_at TO refreshed_at";
            cmd.ExecuteNonQuery();
        }

        if (!columns.Contains("size"))
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE entries ADD COLUMN size INTEGER NOT NULL DEFAULT 0";
            cmd.ExecuteNonQuery();
        }
    }
}