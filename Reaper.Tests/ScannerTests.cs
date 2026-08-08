// -------------------------------------------------------------------------------------
// <copyright file="ScannerTests.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Tests;

using System.Diagnostics;

using Reaper.Scanning;

[TestFixture]
public class ScannerTests
{
   [SetUp]
   public void Setup()
   {
      this.root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
      Directory.CreateDirectory(this.root);
   }

   [TearDown]
   public void TearDown()
   {
      if (Directory.Exists(this.root))
      {
         Directory.Delete(this.root, recursive: true);
      }
   }

   private string root = null!;

   private void Touch(string relativePath)
   {
      var full = this.Full(relativePath);
      Directory.CreateDirectory(Path.GetDirectoryName(full)!);
      File.WriteAllText(full, string.Empty);
   }

   private void Mkdir(string relativePath)
   {
      Directory.CreateDirectory(this.Full(relativePath));
   }

   private string Full(string relativePath)
   {
      return Path.Combine(this.root, relativePath.Replace('/', Path.DirectorySeparatorChar));
   }

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

   [Test]
   public void Desktop_ini_at_root_is_excluded()
   {
      this.Touch("a.txt");
      this.Touch("desktop.ini");
      var paths = Scanner.Scan(this.root).Select(e => e.Path).ToList();
      Assert.That(paths, Does.Not.Contain("desktop.ini"));
      Assert.That(paths, Contains.Item("a.txt"));
   }

   [Test]
   public void Desktop_ini_in_subdirectory_is_included()
   {
      this.Touch("Sub/desktop.ini");
      this.Touch("Sub/file.txt");
      var paths = Scanner.Scan(this.root).Select(e => e.Path);
      Assert.That(paths, Contains.Item("Sub/desktop.ini"));
   }

   [Test]
   public void Directory_symlink_appears_as_entry_but_contents_are_not_traversed()
   {
      var bait = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
      Directory.CreateDirectory(bait);
      File.WriteAllText(Path.Combine(bait, "secret.txt"), string.Empty);
      var linkPath = this.Full("linked-dir");

      try
      {
         Assume.That(
            TryCreateDirectorySymlink(linkPath, bait),
            Is.True,
            "Directory symlink creation requires Developer Mode or elevation; skipping");

         var paths = Scanner.Scan(this.root).Select(e => e.Path).ToList();

         Assert.That(paths, Contains.Item("linked-dir"));
         Assert.That(paths, Does.Not.Contain("linked-dir/secret.txt"));
      }
      finally
      {
         if (Directory.Exists(linkPath))
         {
            Directory.Delete(linkPath);
         }

         Directory.Delete(bait, recursive: true);
      }
   }

   // -------------------------------------------------------------------------
   // Basic scanning
   // -------------------------------------------------------------------------

   [Test]
   public void Empty_root_returns_empty()
   {
      Assert.That(Scanner.Scan(this.root), Is.Empty);
   }

   [Test]
   public void Empty_subdirectory_is_not_tracked()
   {
      this.Mkdir("EmptyDir");
      var paths = Scanner.Scan(this.root).Select(e => e.Path);
      Assert.That(paths, Is.Empty);
   }

   [Test]
   public void File_symlink_appears_as_entry()
   {
      var baitFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
      File.WriteAllText(baitFile, string.Empty);
      var linkPath = this.Full("linked-file.txt");

      try
      {
         Assume.That(
            TryCreateFileSymlink(linkPath, baitFile),
            Is.True,
            "File symlink creation requires Developer Mode or elevation; skipping");

         var paths = Scanner.Scan(this.root).Select(e => e.Path).ToList();
         Assert.That(paths, Contains.Item("linked-file.txt"));
      }
      finally
      {
         File.Delete(baitFile);
      }
   }

   [Test]
   public void Files_at_root_level_are_included()
   {
      this.Touch("a.txt");
      this.Touch("b.txt");
      var paths = Scanner.Scan(this.root).Select(e => e.Path);
      Assert.That(paths, Is.EquivalentTo(["a.txt", "b.txt"]));
   }

   [Test]
   public void Files_in_subdirectory_are_included()
   {
      this.Touch("Foo/bar.txt");
      this.Touch("Foo/baz.txt");
      var paths = Scanner.Scan(this.root).Select(e => e.Path);
      Assert.That(paths, Is.EquivalentTo(["Foo/bar.txt", "Foo/baz.txt"]));
   }

   // -------------------------------------------------------------------------
   // Symlinks and junctions
   //
   // Junction test: always runs — junctions require no special privileges.
   // Directory symlink: requires Developer Mode or elevation; skipped otherwise.
   // File symlink: requires Developer Mode or elevation; skipped otherwise.
   //
   // To enable symlink tests: Settings > System > For Developers > Developer Mode.
   // -------------------------------------------------------------------------

   [Test]
   public void Junction_appears_as_entry_but_contents_are_not_traversed()
   {
      var bait = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
      Directory.CreateDirectory(bait);
      File.WriteAllText(Path.Combine(bait, "secret.txt"), string.Empty);
      var linkPath = this.Full("linked-dir");

      try
      {
         Assume.That(TryCreateJunction(linkPath, bait), Is.True, "Junction creation failed");

         var paths = Scanner.Scan(this.root).Select(e => e.Path).ToList();

         Assert.That(paths, Contains.Item("linked-dir"));
         Assert.That(paths, Does.Not.Contain("linked-dir/secret.txt"));
      }
      finally
      {
         // Delete the junction non-recursively — recursive delete fails on junctions
         if (Directory.Exists(linkPath))
         {
            Directory.Delete(linkPath);
         }

         Directory.Delete(bait, recursive: true);
      }
   }

   [Test]
   public void Max_timestamp_is_positive()
   {
      this.Touch("a.txt");
      var entry = Scanner.Scan(this.root).Single(e => e.Path == "a.txt");
      Assert.That(entry.MaxTimestamp, Is.GreaterThan(0));
   }

   [Test]
   public void Paths_have_no_leading_slash()
   {
      this.Touch("Foo/bar.txt");
      var paths = Scanner.Scan(this.root).Select(e => e.Path);
      Assert.That(paths, Has.All.Not.StartsWith("/"));
   }

   [Test]
   public void Paths_use_forward_slashes()
   {
      this.Touch("Foo/Bar/baz.txt");
      var paths = Scanner.Scan(this.root).Select(e => e.Path);
      Assert.That(paths, Has.All.Not.Contains('\\'));
   }

   [Test]
   public void Reaper_db_at_root_is_excluded()
   {
      this.Touch("a.txt");
      this.Touch(".reaper.db");
      var paths = Scanner.Scan(this.root).Select(e => e.Path).ToList();
      Assert.That(paths, Does.Not.Contain(".reaper.db"));
      Assert.That(paths, Contains.Item("a.txt"));
   }

   [Test]
   public void Reaper_db_in_subdirectory_is_included()
   {
      this.Touch("Inner/.reaper.db");
      this.Touch("Inner/.reaper.toml");
      this.Touch("Inner/file.txt");
      var paths = Scanner.Scan(this.root).Select(e => e.Path).ToList();
      Assert.That(paths, Contains.Item("Inner/.reaper.db"));
      Assert.That(paths, Contains.Item("Inner/.reaper.toml"));
   }

   [Test]
   public void Reaper_toml_at_root_is_excluded()
   {
      this.Touch("a.txt");
      this.Touch(".reaper.toml");
      var paths = Scanner.Scan(this.root).Select(e => e.Path);
      Assert.That(paths, Does.Not.Contain(".reaper.toml"));
   }
}
