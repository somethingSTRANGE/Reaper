using System.Diagnostics;

namespace Reaper.Scheduling;

public sealed record ScheduledTaskInfo(
    string  Name,
    string? NextRunTime,
    string? LastRunTime,
    string? LastResult,
    string? ScheduleType,
    string? StartTime,
    string? Status);

public static class ScheduledTaskLocator
{
    /// <summary>
    /// Queries Windows Task Scheduler for any task whose command line targets
    /// <paramref name="absoluteRoot"/>, by shelling out to schtasks.exe — avoids
    /// re-implementing trigger/recurrence math the OS already knows.
    /// </summary>
    public static IReadOnlyList<ScheduledTaskInfo> FindTasksTargeting(string absoluteRoot)
    {
        var needle = absoluteRoot.TrimEnd('\\').ToLowerInvariant();

        string output;
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", "/Query /FO LIST /V")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow          = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return [];
            output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return [];
        }

        var results = new List<ScheduledTaskInfo>();

        foreach (var block in output.Replace("\r\n", "\n").Split("\n\n"))
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in block.Split('\n'))
            {
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                fields[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }

            if (!fields.TryGetValue("Task To Run", out var taskToRun)) continue;
            if (!taskToRun.ToLowerInvariant().Contains(needle)) continue;

            results.Add(new ScheduledTaskInfo(
                Name:         fields.GetValueOrDefault("TaskName", "?"),
                NextRunTime:  fields.GetValueOrDefault("Next Run Time"),
                LastRunTime:  fields.GetValueOrDefault("Last Run Time"),
                LastResult:   fields.GetValueOrDefault("Last Result"),
                ScheduleType: fields.GetValueOrDefault("Schedule Type"),
                StartTime:    fields.GetValueOrDefault("Start Time"),
                Status:       fields.GetValueOrDefault("Status")));
        }

        return results;
    }
}
