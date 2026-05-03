namespace Reaper.Pruning;

public static class Pruner
{
    public static IReadOnlySet<string> FlagForRemoval(
        IEnumerable<Entry> entries,
        long retentionSeconds,
        long nowSeconds)
    {
        var all = entries.ToList();

        var flagged = all
            .Where(e => nowSeconds - e.FirstSeen > retentionSeconds)
            .Select(e => e.Path)
            .ToHashSet();

        if (flagged.Count == 0)
            return flagged;

        // Build the set of directories that contain at least one retained entry
        var protectedDirs = new HashSet<string>();
        foreach (var entry in all.Where(e => !flagged.Contains(e.Path)))
        {
            foreach (var ancestor in Ancestors(entry.Path))
                protectedDirs.Add(ancestor);
        }

        if (protectedDirs.Count == 0)
            return flagged;

        // Unflag anything that is a protected directory or lives under one
        flagged.RemoveWhere(path =>
            protectedDirs.Contains(path) ||
            Ancestors(path).Any(protectedDirs.Contains));

        return flagged;
    }

    private static IEnumerable<string> Ancestors(string path)
    {
        var parts = path.Split('/');
        for (var i = 1; i < parts.Length; i++)
            yield return string.Join('/', parts[..i]);
    }
}