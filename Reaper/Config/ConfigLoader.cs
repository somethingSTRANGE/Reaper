using Tomlyn;

namespace Reaper.Config;

public static class ConfigLoader
{
    private const int  DefaultRetentionDays    = 7;
    private const bool DefaultDeleteEmptyDirs  = true;
    private const int  DefaultMaxDeletesPerRun = 0;

    public static ReaperConfig Load(string tomlPath, CliOverrides? overrides = null)
    {
        TomlModel? toml = null;
        if (File.Exists(tomlPath))
            toml = TomlSerializer.Deserialize<TomlModel>(File.ReadAllText(tomlPath));

        return new ReaperConfig(
            RetentionDays:    overrides?.RetentionDays ?? toml?.retention_days    ?? DefaultRetentionDays,
            DeleteEmptyDirs:  toml?.delete_empty_dirs  ?? DefaultDeleteEmptyDirs,
            MaxDeletesPerRun: toml?.max_deletes_per_run ?? DefaultMaxDeletesPerRun
        );
    }

    private class TomlModel
    {
        public int?  retention_days    { get; set; }
        public bool? delete_empty_dirs { get; set; }
        public int?  max_deletes_per_run { get; set; }
    }
}
