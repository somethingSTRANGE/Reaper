// -------------------------------------------------------------------------------------
// <copyright file="SafetyCheckerTests.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Tests;

using Reaper.Safety;

[TestFixture]
public class SafetyCheckerTests
{
   [Test]
   public void Ancestor_of_USERPROFILE_is_blocked()
   {
      var userProfile = Environment.GetEnvironmentVariable("USERPROFILE")!;
      var parent = Path.GetDirectoryName(userProfile);
      Assume.That(parent, Is.Not.Null, "USERPROFILE has no parent");
      var root = Path.GetPathRoot(userProfile)!.TrimEnd('\\', '/');
      Assume.That(
         parent,
         Is.Not.EqualTo(root).IgnoreCase,
         "USERPROFILE parent is drive root — ancestor check not meaningful");
      Assert.That(SafetyChecker.IsProtected(parent!), Is.True);
   }

   [Test]
   public void APPDATA_is_blocked()
   {
      Assert.That(SafetyChecker.IsProtected(Environment.GetEnvironmentVariable("APPDATA")!), Is.True);
   }

   [Test]
   public void Descendant_of_APPDATA_is_blocked()
   {
      var descendant = Path.Combine(Environment.GetEnvironmentVariable("APPDATA")!, "SomeApp");
      Assert.That(SafetyChecker.IsProtected(descendant), Is.True);
   }

   [Test]
   public void Descendant_of_LOCALAPPDATA_is_blocked()
   {
      var descendant = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "SomeApp");
      Assert.That(SafetyChecker.IsProtected(descendant), Is.True);
   }

   [Test]
   public void Descendant_of_USERPROFILE_is_allowed()
   {
      var descendant = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE")!, "Temp");
      Assert.That(SafetyChecker.IsProtected(descendant), Is.False);
   }

   [Test]
   public void Descendant_of_WINDIR_is_blocked()
   {
      var descendant = Path.Combine(Environment.GetEnvironmentVariable("WINDIR")!, "Temp");
      Assert.That(SafetyChecker.IsProtected(descendant), Is.True);
   }

   [Test]
   public void Drive_root_is_blocked()
   {
      var root = Path.GetPathRoot(Environment.GetEnvironmentVariable("USERPROFILE")!)!;
      Assert.That(SafetyChecker.IsProtected(root), Is.True);
   }

   [Test]
   public void ProgramFiles_is_blocked()
   {
      Assert.That(SafetyChecker.IsProtected(Environment.GetEnvironmentVariable("ProgramFiles")!), Is.True);
   }

   [Test]
   public void USERPROFILE_is_blocked()
   {
      Assert.That(SafetyChecker.IsProtected(Environment.GetEnvironmentVariable("USERPROFILE")!), Is.True);
   }

   [Test]
   public void WINDIR_is_blocked()
   {
      Assert.That(SafetyChecker.IsProtected(Environment.GetEnvironmentVariable("WINDIR")!), Is.True);
   }
}
