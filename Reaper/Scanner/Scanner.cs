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
                    results.Add(new FsEntry(reparsePath, MaxTimestamp(entry), Size: 0));
                }
                else
                {
                    Walk(subDir, root, results, isRoot: false);
                }
                continue;
            }

            var relativePath = Path.GetRelativePath(root, entry.FullName).Replace('\\', '/');
            var isFileReparsePoint = entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
            var size = entry is FileInfo fileInfo && !isFileReparsePoint ? fileInfo.Length : 0;
            results.Add(new FsEntry(relativePath, MaxTimestamp(entry), size));
        }
    }

    private static long MaxTimestamp(FileSystemInfo info)
    {
        // LastAccessTimeUtc is deliberately excluded: it's meant to be reads-don't-touch-it
        // per NTFS's DisableLastAccess setting, but that's not reliably true in practice (AV
        // scans, indexing, and even a plain directory read have been observed to bump it here).
        // Since it can advance without the file's content or identity actually changing, treating
        // it as an external-touch signal would perpetually reset first_seen on every scan.
        var max = new[] { info.CreationTimeUtc, info.LastWriteTimeUtc }.Max();
        return new DateTimeOffset(max).ToUnixTimeSeconds();
    }
}
