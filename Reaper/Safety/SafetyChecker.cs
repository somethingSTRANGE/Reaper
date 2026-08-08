// -------------------------------------------------------------------------------------
// <copyright file="SafetyChecker.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Safety;

public static class SafetyChecker
{
   // Block the path itself and its ancestors, but NOT descendants.
   // Subdirectories like %USERPROFILE%\Temp and %USERPROFILE%\Downloads
   // are the intended use case.
   private static readonly string[] profileProtectedEnvVars =
      [
         "USERPROFILE",
      ];

   // Block the path itself, its ancestors, and all descendants.
   // Nothing under these directories should ever be a reaping target.
   private static readonly string[] strictProtectedEnvVars =
      [
         "WINDIR", "SystemRoot",
         "ProgramFiles", "ProgramFiles(x86)", "ProgramData",
         "APPDATA", "LOCALAPPDATA",
      ];

   public static bool IsProtected(string absolutePath)
   {
      var fullPath = Path.GetFullPath(absolutePath);
      var target = Normalize(fullPath);

      return IsDriveRoot(fullPath, target) || MatchesAny(strictProtectedEnvVars, target, blockDescendants: true)
                                           || MatchesAny(profileProtectedEnvVars, target, blockDescendants: false);
   }

   private static bool IsDriveRoot(string fullPath, string normalized)
   {
      var root = Path.GetPathRoot(fullPath);
      return !string.IsNullOrEmpty(root) && normalized.Equals(Normalize(root), StringComparison.OrdinalIgnoreCase);
   }

   private static bool IsSameAncestorOrDescendant(string target, string protectedPath, bool blockDescendants)
   {
      if (target.Equals(protectedPath, StringComparison.OrdinalIgnoreCase))
      {
         return true;
      }

      // target is an ancestor of this protected path
      if (protectedPath.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
      {
         return true;
      }

      // target is a descendant of this protected path
      return blockDescendants && target.StartsWith(
         protectedPath + Path.DirectorySeparatorChar,
         StringComparison.OrdinalIgnoreCase);
   }

   // True if `target` equals, is an ancestor of, or (when blockDescendants) is a descendant of
   // the path named by any of the given environment variables. Unset/empty variables are skipped.
   private static bool MatchesAny(IEnumerable<string> envVars, string target, bool blockDescendants)
   {
      foreach (var envVar in envVars)
      {
         var value = Environment.GetEnvironmentVariable(envVar);
         if (string.IsNullOrEmpty(value))
         {
            continue;
         }

         var protectedPath = Normalize(Path.GetFullPath(value));

         if (IsSameAncestorOrDescendant(target, protectedPath, blockDescendants))
         {
            return true;
         }
      }

      return false;
   }

   private static string Normalize(string path)
   {
      return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
   }
}
