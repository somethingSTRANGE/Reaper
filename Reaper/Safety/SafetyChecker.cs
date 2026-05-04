namespace Reaper.Safety;

public static class SafetyChecker
{
    // Block the path itself, its ancestors, and all descendants.
    // Nothing under these directories should ever be a reaping target.
    private static readonly string[] StrictProtectedEnvVars =
    [
        "WINDIR", "SystemRoot",
        "ProgramFiles", "ProgramFiles(x86)", "ProgramData",
        "APPDATA", "LOCALAPPDATA"
    ];

    // Block the path itself and its ancestors, but NOT descendants.
    // Subdirectories like %USERPROFILE%\Temp and %USERPROFILE%\Downloads
    // are the primary intended use case.
    private static readonly string[] ProfileProtectedEnvVars =
    [
        "USERPROFILE"
    ];

    public static bool IsProtected(string absolutePath)
    {
        var fullPath = Path.GetFullPath(absolutePath);
        var target   = Normalize(fullPath);

        if (IsDriveRoot(fullPath, target))
            return true;

        foreach (var envVar in StrictProtectedEnvVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(value)) continue;

            var norm = Normalize(Path.GetFullPath(value));

            if (target.Equals(norm, StringComparison.OrdinalIgnoreCase))
                return true;

            // target is an ancestor of this protected path
            if (norm.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;

            // target is a descendant of this protected path
            if (target.StartsWith(norm + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var envVar in ProfileProtectedEnvVars)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (string.IsNullOrEmpty(value)) continue;

            var norm = Normalize(Path.GetFullPath(value));

            if (target.Equals(norm, StringComparison.OrdinalIgnoreCase))
                return true;

            // target is an ancestor of this protected path
            if (norm.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
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
