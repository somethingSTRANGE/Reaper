// -------------------------------------------------------------------------------------
// <copyright file="ScheduledTaskInfo.cs">
//   Copyright (c) 2026 Michael Ryan
//   Licensed under the MIT License. See LICENSE file in the project root.
// </copyright>
// -------------------------------------------------------------------------------------

namespace Reaper.Scheduling;

using System.ComponentModel;
using System.Diagnostics;

public sealed record ScheduledTaskInfo(string Name, string? NextRunTime, string? LastRunTime, string? LastResult);

public static class ScheduledTaskLocator
{
   /// <summary>Queries Windows Task Scheduler for any task whose command line targets
   ///    <paramref name="absoluteRoot"/>, by shelling out to schtasks.exe — avoids
   ///    re-implementing trigger/recurrence math the OS already knows.</summary>
   public static IReadOnlyList<ScheduledTaskInfo> FindTasksTargeting(string absoluteRoot)
   {
      var needle = absoluteRoot.TrimEnd('\\').ToLowerInvariant();
      var output = RunSchTasksQuery();
      if (output is null)
      {
         return [];
      }

      var results = new List<ScheduledTaskInfo>();

      foreach (var block in output.Replace("\r\n", "\n").Split("\n\n"))
      {
         var fields = ParseFields(block);

         if (!fields.TryGetValue("Task To Run", out var taskToRun))
         {
            continue;
         }

         if (!taskToRun.ToLowerInvariant().Contains(needle))
         {
            continue;
         }

         results.Add(
            new ScheduledTaskInfo(
               Name: fields.GetValueOrDefault("TaskName", "?"),
               NextRunTime: fields.GetValueOrDefault("Next Run Time"),
               LastRunTime: fields.GetValueOrDefault("Last Run Time"),
               LastResult: fields.GetValueOrDefault("Last Result")));
      }

      return results;
   }

   private static Dictionary<string, string> ParseFields(string block)
   {
      var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (var line in block.Split('\n'))
      {
         var idx = line.IndexOf(':');
         if (idx <= 0)
         {
            continue;
         }

         fields[line[..idx].Trim()] = line[(idx + 1)..].Trim();
      }

      return fields;
   }

   private static string? RunSchTasksQuery()
   {
      try
      {
         var psi = new ProcessStartInfo("schtasks.exe", "/Query /FO LIST /V")
            {
               RedirectStandardOutput = true,
               RedirectStandardError = true,
               UseShellExecute = false,
               CreateNoWindow = true,
            };
         using var proc = Process.Start(psi);
         if (proc is null)
         {
            return null;
         }

         var output = proc.StandardOutput.ReadToEnd();
         proc.WaitForExit();
         return output;
      }
      catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
      {
         return null;
      }
   }
}
