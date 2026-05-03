namespace Reaper.Safety;

public static class SafetyChecker
{
    private static readonly string[] ProtectedEnvVars =
    [
        "WINDIR", "SystemRoot", "USERPROFILE", "APPDATA", "LOCALAPPDATA",
        "ProgramFiles", "ProgramFiles(x86)", "ProgramData"
    ];

    public static bool IsProtected(string absolutePath)
    {
        var fullPath = Path.GetFullPath(absolutePath);
        var target   = Normalize(fullPath);

        if (IsDriveRoot(fullPath, target))
            return true;

        foreach (var envVar in ProtectedEnvVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(value)) continue;

            var protectedNorm = Normalize(Path.GetFullPath(value));

            if (target.Equals(protectedNorm, StringComparison.OrdinalIgnoreCase))
                return true;

            // target is an ancestor of a protected path
            if (protectedNorm.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsDriveRoot(string fullPath, string normalized)
    {
        var root = Path.GetPathRoot(fullPath);
        return !string.IsNullOrEmpty(root) &&
               normalized.Equals(Normalize(root), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}