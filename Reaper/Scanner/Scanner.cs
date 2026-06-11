namespace Reaper.Scanning;

public static class Scanner
{
    private static readonly HashSet<string> RootExcluded = new(StringComparer.OrdinalIgnoreCase)
    {
        ".reaper.db", ".reaper.toml", "desktop.ini"
    };

    public static IReadOnlyList<FsEntry> Scan(string root)
    {
        var results = new List<FsEntry>();
        Walk(new DirectoryInfo(root), root, results, isRoot: true);
        return results;
    }

    private static void Walk(DirectoryInfo dir, string root, List<FsEntry> results, bool isRoot)
    {
        foreach (var entry in dir.EnumerateFileSystemInfos())
        {
            if (isRoot && RootExcluded.Contains(entry.Name))
                continue;

            if (entry is DirectoryInfo subDir)
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    // Symlink or junction: track as an opaque entry, never traverse
                    var reparsePath = Path.GetRelativePath(root, entry.FullName).Replace('\\', '/');
                    results.Add(new FsEntry(reparsePath, MaxTimestamp(entry)));
                }
                else
                {
                    Walk(subDir, root, results, isRoot: false);
                }
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
