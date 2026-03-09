using System.CommandLine;
using Spectre.Console;
using Tripletex.EmployeeCli.Configuration;

namespace Tripletex.EmployeeCli.Commands;

public static class ConfigCommand
{
    public static Command Create()
    {
        var cmd = new Command("config", "View configuration");
        cmd.AddCommand(CreateShowCommand());
        cmd.AddCommand(CreateClearDefaultsCommand());
        return cmd;
    }

    private static Command CreateShowCommand()
    {
        var cmd = new Command("show", "Show current configuration");

        cmd.SetHandler(() =>
        {
            var config = ConfigStore.Load();
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Setting");
            table.AddColumn("Value");

            table.AddRow("Email", config.Email ?? "[dim]not set[/]");
            table.AddRow("Employee Name", config.EmployeeName ?? "[dim]not set[/]");
            table.AddRow("Employee ID", config.EmployeeId?.ToString() ?? "[dim]not set[/]");
            table.AddRow("Consumer Token", Mask(config.ConsumerToken));
            table.AddRow("Employee Token", Mask(config.EmployeeToken));
            table.AddRow("Environment", config.Environment ?? "production");
            table.AddRow("Default Project", config.DefaultProjectId.HasValue
                ? $"{config.DefaultProjectName} (ID: {config.DefaultProjectId})"
                : "[dim]not set[/]");
            table.AddRow("Default Activity", config.DefaultActivityId.HasValue
                ? $"{config.DefaultActivityName} (ID: {config.DefaultActivityId})"
                : "[dim]not set[/]");

            AnsiConsole.Write(table);
        });

        return cmd;
    }

    private static Command CreateClearDefaultsCommand()
    {
        var cmd = new Command("clear-defaults", "Clear default project and activity");

        cmd.SetHandler(() =>
        {
            var config = ConfigStore.Load();
            config.DefaultProjectId = null;
            config.DefaultProjectName = null;
            config.DefaultActivityId = null;
            config.DefaultActivityName = null;
            ConfigStore.Save(config);

            AnsiConsole.MarkupLine("[green]Defaults cleared.[/]");
        });

        return cmd;
    }

    private static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "[dim]not set[/]";
        return value.Length > 8
            ? value[..4] + new string('*', value.Length - 8) + value[^4..]
            : new string('*', value.Length);
    }
}
