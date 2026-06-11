using System.Diagnostics;

var reapExe = Path.Combine(AppContext.BaseDirectory, "reap.exe");
if (!File.Exists(reapExe))
    return 1;

var psi = new ProcessStartInfo(reapExe)
{
    CreateNoWindow = true,
    UseShellExecute = false,
};
foreach (var arg in args)
    psi.ArgumentList.Add(arg);

using var process = Process.Start(psi)!;
process.WaitForExit();
return process.ExitCode;
