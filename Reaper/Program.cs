using Reaper.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("reap");
    config.AddCommand<VersionCommand>("version")
          .WithDescription("Print version and build info");
    config.AddCommand<InitCommand>("init")
          .WithDescription("Initialize a folder for tracking");
    config.AddCommand<StatusCommand>("status")
          .WithDescription("Show tracked-folder stats");
    config.AddCommand<PreviewCommand>("preview")
          .WithDescription("List what would be deleted (read-only)");
    config.AddCommand<ExecuteCommand>("execute")
          .WithDescription("Prune expired entries");
    config.AddCommand<TouchCommand>("touch")
          .WithDescription("Reset first_seen to NOW for a path or directory");
});

return app.Run(args);