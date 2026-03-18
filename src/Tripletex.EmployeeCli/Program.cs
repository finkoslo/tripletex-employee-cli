using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Reflection;
using Spectre.Console;
using Tripletex.Api.Models;
using Tripletex.EmployeeCli;
using Tripletex.EmployeeCli.Commands;

var updateCheck = args.Any(a => a.Equals("update", StringComparison.OrdinalIgnoreCase))
    ? null
    : UpdateChecker.CheckForUpdateAsync();

var jsonOption = new Option<bool>("--json", "Output results as JSON");
var yesOption = new Option<bool>(["--yes", "-y"], "Skip confirmations (auto-confirm)");

var rootCommand = new RootCommand("Finkletex — manage your Tripletex timesheets")
{
    jsonOption,
    yesOption
};

rootCommand.AddCommand(LoginCommand.Create());
rootCommand.AddCommand(ConfigCommand.Create());
rootCommand.AddCommand(TimesheetCommand.Create(jsonOption, yesOption));
rootCommand.AddCommand(ProjectCommand.Create(jsonOption));
rootCommand.AddCommand(ActivityCommand.Create(jsonOption));
rootCommand.AddCommand(UpdateCommand.Create());

foreach (var shortcut in TimesheetCommand.CreateShortcuts(jsonOption, yesOption))
    rootCommand.AddCommand(shortcut);

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseExceptionHandler((ex, ctx) =>
    {
        var inner = ex is TargetInvocationException { InnerException: { } innerEx } ? innerEx : ex;

        switch (inner)
        {
            case TripletexApiException apiEx:
                AnsiConsole.MarkupLine($"[red]API Error ({apiEx.StatusCode}): {Markup.Escape(apiEx.Message)}[/]");
                if (apiEx.DeveloperMessage is not null)
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(apiEx.DeveloperMessage)}[/]");
                foreach (var v in apiEx.ValidationMessages)
                    AnsiConsole.MarkupLine($"[yellow]  {Markup.Escape(v.Field ?? "")}: {Markup.Escape(v.Message ?? "")}[/]");
                break;

            case HttpRequestException httpEx:
                AnsiConsole.MarkupLine($"[red]Network error: {Markup.Escape(httpEx.Message)}[/]");
                break;

            case InvalidOperationException opEx when IsAuthError(opEx):
                AnsiConsole.MarkupLine("[red]Not logged in or session expired.[/]");
                AnsiConsole.MarkupLine("[dim]Run: finkletex login[/]");
                break;

            case InvalidOperationException opEx:
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(opEx.Message)}[/]");
                break;

            case FormatException fmtEx:
                AnsiConsole.MarkupLine($"[red]Invalid format: {Markup.Escape(fmtEx.Message)}[/]");
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(inner.Message ?? "Unknown error")}[/]");
                break;
        }

        ctx.ExitCode = 1;
    })
    .Build();

var exitCode = await parser.InvokeAsync(args);

if (updateCheck is not null)
{
    var latestVersion = await updateCheck;
    if (latestVersion is not null)
        AnsiConsole.MarkupLine($"[yellow]Update available: v{latestVersion}. Run [bold]finkletex update[/] to upgrade.[/]");
}

return exitCode;

static bool IsAuthError(Exception ex) =>
    ex.Message.Contains("Not logged in", StringComparison.OrdinalIgnoreCase)
    || ex.Message.Contains("Session token", StringComparison.OrdinalIgnoreCase)
    || ex.Message.Contains("encryptedId", StringComparison.OrdinalIgnoreCase)
    || ex.StackTrace?.Contains("SessionTokenProvider", StringComparison.Ordinal) == true;
