namespace Reaper.Config;

public record ReaperConfig(
    int RetentionDays,
    bool DeleteEmptyDirs,
    int MaxDeletesPerRun
);