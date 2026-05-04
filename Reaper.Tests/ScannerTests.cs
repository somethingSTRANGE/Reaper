using System.Diagnostics;
using Reaper.Scanning;

namespace Reaper.Tests;

[TestFixture]
public class ScannerTests
{
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    // Creates a file and returns its relative path
    private string Touch(string relativePath)
    {
        var full = Full(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, string.Empty);
        return relativePath;
    }

    private void Mkdir(string relativePath) =>
        Directory.CreateDirectory(Full(relativePath));

    private string Full(string relativePath) =>
        Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    // Creates an NTFS junction — does not require elevated privileges
    private static bool TryCreateJunction(string linkPath, string targetPath)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{linkPath}\" \"{targetPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(psi)!;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    // Creates a directory symlink — requires Developer Mode or elevation
    private static bool TryCreateDirectorySymlink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    // Creates a file symlink — requires Developer Mode or elevation
    private static bool TryCreateFileSymlink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Basic scanning
    // -------------------------------------------------------------------------

    [Test]
    public void Empty_root_returns_empty()
    {
        Assert.That(Scanner.Scan(_root), Is.Empty);
    }

    [Test]
    public void Files_at_root_level_are_included()
    {
        Touch("a.txt");
        Touch("b.txt");
        var paths = Scanner.Scan(_root).Select(e => e.Path);
        Assert.That(paths, Is.EquivalentTo(new[] { "a.txt", "b.txt" }));
    }

    [Test]
    public void Files_in_subdirectory_are_included()
    {
        Touch("Foo/bar.txt");
        Touch("Foo/baz.txt");
        var paths = Scanner.Scan(_root).Select(e => e.Path);
        Assert.That(paths, Is.EquivalentTo(new[] { "Foo/bar.txt", "Foo/baz.txt" }));
    }

    [Test]
    public void Empty_subdirectory_is_not_tracked()
    {
        Mkdir("EmptyDir");
        var paths = Scanner.Scan(_root).Select(e => e.Path);
        Assert.That(paths, Is.Empty);
    }

    [Test]
    public void Reaper_db_is_excluded()
    {
        Touch("a.txt");
        Touch(".reaper.db");
        var paths = Scanner.Scan(_root).Select(e => e.Path);
        Assert.That(paths, Does.Not.Contain(".reaper.db"));
        Assert.That(paths, Contains.Item("a.txt"));
    }

    [Test]
    public void Reaper_toml_is_excluded()
    {
        Touch("a.txt");
        Touch(".reaper.toml");
        var paths = Scanner.Scan(_root).Select(e => e.Path);
        Assert.That(paths, Does.Not.Contain(".reaper.toml"));
    }

    [Test]
    public void Paths_use_forward_slashes()
    {
        Touch("Foo/Bar/baz.txt");
        var paths = Scanner.Scan(_root).Select(e => e.Path);
        Assert.That(paths, Has.All.Not.Contains('\\'));
    }

    [Test]
    public void Paths_have_no_leading_slash()
    {
        Touch("Foo/bar.txt");
        var paths = Scanner.Scan(_root).Select(e => e.Path);
        Assert.That(paths, Has.All.Not.StartsWith("/"));
    }

    [Test]
    public void Max_timestamp_is_positive()
    {
        Touch("a.txt");
        var entry = Scanner.Scan(_root).Single(e => e.Path == "a.txt");
        Assert.That(entry.MaxTimestamp, Is.GreaterThan(0));
    }

    // -------------------------------------------------------------------------
    // Symlinks and junctions
    //
    // Junction test:        always runs — junctions require no special privileges.
    // Directory symlink:    requires Developer Mode or elevation; skipped otherwise.
    // File symlink:         requires Developer Mode or elevation; skipped otherwise.
    //
    // To enable symlink tests: Settings > System > For Developers > Developer Mode.
    // -------------------------------------------------------------------------

    [Test]
    public void Junction_appears_as_entry_but_contents_are_not_traversed()
    {
        var bait = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(bait);
        File.WriteAllText(Path.Combine(bait, "secret.txt"), string.Empty);
        var linkPath = Full("linked-dir");

        try
        {
            Assume.That(TryCreateJunction(linkPath, bait), Is.True, "Junction creation failed");

            var paths = Scanner.Scan(_root).Select(e => e.Path).ToList();

            Assert.That(paths, Contains.Item("linked-dir"));
            Assert.That(paths, Does.Not.Contain("linked-dir/secret.txt"));
        }
        finally
        {
            // Delete the junction non-recursively — recursive delete fails on junctions
            if (Directory.Exists(linkPath))
                Directory.Delete(linkPath);
            Directory.Delete(bait, recursive: true);
        }
    }

    [Test]
    public void Directory_symlink_appears_as_entry_but_contents_are_not_traversed()
    {
        var bait = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(bait);
        File.WriteAllText(Path.Combine(bait, "secret.txt"), string.Empty);
        var linkPath = Full("linked-dir");

        try
        {
            Assume.That(TryCreateDirectorySymlink(linkPath, bait), Is.True,
                "Directory symlink creation requires Developer Mode or elevation; skipping");

            var paths = Scanner.Scan(_root).Select(e => e.Path).ToList();

            Assert.That(paths, Contains.Item("linked-dir"));
            Assert.That(paths, Does.Not.Contain("linked-dir/secret.txt"));
        }
        finally
        {
            if (Directory.Exists(linkPath))
                Directory.Delete(linkPath);
            Directory.Delete(bait, recursive: true);
        }
    }

    [Test]
    public void File_symlink_appears_as_entry()
    {
        var baitFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
        File.WriteAllText(baitFile, string.Empty);
        var linkPath = Full("linked-file.txt");

        try
        {
            Assume.That(TryCreateFileSymlink(linkPath, baitFile), Is.True,
                "File symlink creation requires Developer Mode or elevation; skipping");

            var paths = Scanner.Scan(_root).Select(e => e.Path).ToList();
            Assert.That(paths, Contains.Item("linked-file.txt"));
        }
        finally
        {
            File.Delete(baitFile);
        }
    }
}