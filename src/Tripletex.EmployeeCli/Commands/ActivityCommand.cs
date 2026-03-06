using System.CommandLine;
using Spectre.Console;
using Tripletex.Api.Operations;
using Tripletex.EmployeeCli.Configuration;

namespace Tripletex.EmployeeCli.Commands;

public static class ActivityCommand
{
    public static Command Create()
    {
        var cmd = new Command("activity", "Manage default activity");
        cmd.AddCommand(CreateSelectCommand());
        return cmd;
    }

    private static Command CreateSelectCommand()
    {
        var projectId = new Option<int?>("--project-id", "Filter activities by project ID");
        var cmd = new Command("select", "Interactively select a default activity") { projectId };

        cmd.SetHandler(async (pid) =>
        {
            var config = ConfigStore.Load();
            ConfigStore.GetEmployeeId(config); // validate logged in
            var resolvedProjectId = pid ?? config.DefaultProjectId;

            using var client = ClientFactory.Create(config);

            if (resolvedProjectId is null)
            {
                AnsiConsole.MarkupLine("[yellow]No project specified. Use --project-id or set a default project first.[/]");
                return;
            }

            var projectLabel = config.DefaultProjectName ?? resolvedProjectId.ToString()!;
            AnsiConsole.MarkupLine($"[dim]Fetching activities for project {Markup.Escape(projectLabel)}...[/]");

            var project = await client.Project.GetAsync(resolvedProjectId.Value, fields: "projectActivities(activity(*))");
            var activities = (project.ProjectActivities ?? [])
                .Where(pa => !pa.IsClosed)
                .Select(pa => new Activity
                {
                    Id = pa.Activity?.Id ?? pa.Id,
                    Name = pa.Activity?.Name,
                    DisplayName = pa.Activity?.DisplayName,
                })
                .OrderBy(a => a.DisplayName ?? a.Name ?? "")
                .ToList();

            if (activities.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No activities found for this project.[/]");
                return;
            }

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<Activity>()
                    .Title("Select default activity:")
                    .PageSize(15)
                    .UseConverter(a => $"{a.DisplayName ?? a.Name ?? "Unnamed"} [dim]ID: {a.Id}[/]")
                    .AddChoices(activities));

            var activityName = selected.DisplayName ?? selected.Name ?? $"Activity {selected.Id}";
            config.DefaultActivityId = selected.Id;
            config.DefaultActivityName = activityName;
            ConfigStore.Save(config);

            AnsiConsole.MarkupLine($"[green]Saved default activity: {Markup.Escape(activityName)} (ID: {selected.Id})[/]");
        }, projectId);

        return cmd;
    }
}
