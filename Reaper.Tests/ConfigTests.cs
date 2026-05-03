using Reaper.Config;

namespace Reaper.Tests;

[TestFixture]
public class ConfigTests
{
    private static string WriteToml(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    [Test]
    public void Defaults_when_no_config_file()
    {
        var config = ConfigLoader.Load("nonexistent.toml");
        Assert.That(config.RetentionDays,    Is.EqualTo(7));
        Assert.That(config.DeleteEmptyDirs,  Is.True);
        Assert.That(config.MaxDeletesPerRun, Is.EqualTo(0));
    }

    [Test]
    public void Config_file_values_override_defaults()
    {
        var path = WriteToml("""
            retention_days = 14
            delete_empty_dirs = false
            max_deletes_per_run = 100
            """);
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.That(config.RetentionDays,    Is.EqualTo(14));
            Assert.That(config.DeleteEmptyDirs,  Is.False);
            Assert.That(config.MaxDeletesPerRun, Is.EqualTo(100));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void Partial_config_uses_defaults_for_missing_keys()
    {
        var path = WriteToml("retention_days = 14");
        try
        {
            var config = ConfigLoader.Load(path);
            Assert.That(config.RetentionDays,    Is.EqualTo(14));
            Assert.That(config.DeleteEmptyDirs,  Is.True);
            Assert.That(config.MaxDeletesPerRun, Is.EqualTo(0));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void Cli_override_wins_over_config_file()
    {
        var path = WriteToml("retention_days = 14");
        try
        {
            var config = ConfigLoader.Load(path, new CliOverrides(RetentionDays: 30));
            Assert.That(config.RetentionDays, Is.EqualTo(30));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void Cli_override_wins_over_defaults()
    {
        var config = ConfigLoader.Load("nonexistent.toml", new CliOverrides(RetentionDays: 30));
        Assert.That(config.RetentionDays, Is.EqualTo(30));
    }
}