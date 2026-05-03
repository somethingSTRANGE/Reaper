namespace Reaper.Scanning;

public static class Scanner
{
    private static readonly HashSet<string> Excluded = [".reaper.db", ".reaper.toml"];

    public static IReadOnlyList<FsEntry> Scan(string root)
    {
        var results = new List<FsEntry>();
        Walk(new DirectoryInfo(root), root, results);
        return results;
    }

    private static void Walk(DirectoryInfo dir, string root, List<FsEntry> results)
    {
        foreach (var entry in dir.EnumerateFileSystemInfos())
        {
            if (Excluded.Contains(entry.Name))
                continue;

            if (entry is DirectoryInfo subDir)
            {
                if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    Walk(subDir, root, results);
                continue;
            }

            var relativePath = Path.GetRelativePath(root, entry.FullName).Replace('\\', '/');
            results.Add(new FsEntry(relativePath, MaxTimestamp(entry)));
        }
    }

    private static long MaxTimestamp(FileSystemInfo info)
    {
        var max = new[] { info.CreationTimeUtc, info.LastWriteTimeUtc, info.LastAccessTimeUtc }.Max();
        return new DateTimeOffset(max).ToUnixTimeSeconds();
    }
}
