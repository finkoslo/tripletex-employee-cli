using System.CommandLine;
using System.Globalization;
using Spectre.Console;
using Tripletex.Api;
using Tripletex.Api.Models;
using Tripletex.Api.Operations;
using Tripletex.EmployeeCli.Configuration;

namespace Tripletex.EmployeeCli.Commands;

public static class TimesheetCommand
{
    public static Command Create(Option<bool> jsonOption)
    {
        var cmd = new Command("timesheet", "Manage your timesheet entries");
        cmd.AddCommand(CreateLogCommand(jsonOption));
        cmd.AddCommand(CreateLogWeekCommand(jsonOption));
        cmd.AddCommand(CreateListCommand(jsonOption));
        cmd.AddCommand(CreateRecentCommand(jsonOption));
        cmd.AddCommand(CreateWeekCommand(jsonOption));
        return cmd;
    }

    public static IEnumerable<Command> CreateShortcuts(Option<bool> jsonOption)
    {
        var l = CreateLogCommand(jsonOption);
        l.Name = "l";
        l.Description = "Log hours (shortcut for 'timesheet log')";
        yield return l;

        var lw = CreateLogWeekCommand(jsonOption);
        lw.Name = "lw";
        lw.Description = "Log a full week (shortcut for 'timesheet log-week')";
        yield return lw;

        var r = CreateRecentCommand(jsonOption);
        r.Name = "r";
        r.Description = "Show recent entries (shortcut for 'timesheet recent')";
        yield return r;

        var w = CreateWeekCommand(jsonOption);
        w.Name = "w";
        w.Description = "Show weekly hours (shortcut for 'timesheet week')";
        yield return w;

        var ls = CreateListCommand(jsonOption);
        ls.Name = "ls";
        ls.Description = "List entries (shortcut for 'timesheet list')";
        yield return ls;
    }

    private enum LogStep { Project, Activity, Hours, Date, Comment, Confirm }

    private const string BackSentinel = "\u2190 Back";
    private const string FilterSentinelLabel = "[blue]Filter...[/]";

    private static Command CreateLogCommand(Option<bool> jsonOption)
    {
        var hours = new Argument<decimal?>("hours") { Arity = ArgumentArity.ZeroOrOne, Description = "Number of hours to log" };
        var date = new Option<string?>("--date", "Date (yyyy-MM-dd), defaults to today");
        var comment = new Option<string?>("--comment", "Comment for the entry");
        var projectId = new Option<int?>("--project-id", "Project ID (overrides default)");
        var activityId = new Option<int?>("--activity-id", "Activity ID (overrides default)");

        var cmd = new Command("log", "Log hours (interactive if no arguments given)") { hours, date, comment, projectId, activityId };

        cmd.SetHandler(async (ctx) =>
        {
            var h = ctx.ParseResult.GetValueForArgument(hours);
            var d = ctx.ParseResult.GetValueForOption(date);
            var c = ctx.ParseResult.GetValueForOption(comment);
            var pid = ctx.ParseResult.GetValueForOption(projectId);
            var aid = ctx.ParseResult.GetValueForOption(activityId);
            var json = ctx.ParseResult.GetValueForOption(jsonOption);

            var config = ConfigStore.Load();
            var employeeId = ConfigStore.GetEmployeeId(config);
            using var client = ClientFactory.Create(config);

            int? resolvedProject = pid ?? config.DefaultProjectId;
            int? resolvedActivity = aid ?? config.DefaultActivityId;
            decimal? resolvedHours = h;
            DateOnly? resolvedDate = d is not null ? DateOnly.Parse(d) : null;
            string? resolvedComment = c;

            string? projectName = config.DefaultProjectName;
            string? activityName = config.DefaultActivityName;

            var step = resolvedProject is null ? LogStep.Project
                     : resolvedActivity is null ? LogStep.Activity
                     : resolvedHours is null ? LogStep.Hours
                     : resolvedDate is null ? LogStep.Date
                     : resolvedComment is null ? LogStep.Comment
                     : LogStep.Confirm;

            var firstStep = step;

            while (true)
            {
                switch (step)
                {
                    case LogStep.Project:
                    {
                        var result = await PromptProjectAsync(client, config, canGoBack: false);
                        if (result is null) return;
                        resolvedProject = result.Value.id;
                        projectName = result.Value.name;
                        resolvedActivity = null;
                        activityName = null;
                        step = LogStep.Activity;
                        break;
                    }
                    case LogStep.Activity:
                    {
                        var result = await PromptActivityAsync(client, config, resolvedProject!.Value, canGoBack: true);
                        if (result is { isBack: true }) { step = LogStep.Project; break; }
                        if (result is null) return;
                        resolvedActivity = result.Value.id;
                        activityName = result.Value.name;
                        step = LogStep.Hours;
                        break;
                    }
                    case LogStep.Hours:
                    {
                        resolvedHours ??= AnsiConsole.Prompt(
                            new TextPrompt<decimal>("Hours:")
                                .Validate(v => v > 0 ? ValidationResult.Success() : ValidationResult.Error("Must be > 0")));
                        step = LogStep.Date;
                        break;
                    }
                    case LogStep.Date:
                    {
                        resolvedDate ??= AnsiConsole.Prompt(
                            new TextPrompt<DateOnly>("Date:")
                                .DefaultValue(DateOnly.FromDateTime(DateTime.Today)));
                        step = LogStep.Comment;
                        break;
                    }
                    case LogStep.Comment:
                    {
                        resolvedComment ??= AnsiConsole.Prompt(
                            new TextPrompt<string>("Comment:")
                                .AllowEmpty());
                        if (string.IsNullOrWhiteSpace(resolvedComment)) resolvedComment = null;
                        step = LogStep.Confirm;
                        break;
                    }
                    case LogStep.Confirm:
                    {
                        AnsiConsole.MarkupLine($"[bold]Summary:[/]");
                        if (config.EmployeeName is not null)
                            AnsiConsole.MarkupLine($"  Employee: [cyan]{Markup.Escape(config.EmployeeName)}[/]");
                        AnsiConsole.MarkupLine($"  Project:  [cyan]{Markup.Escape(projectName ?? resolvedProject.ToString()!)}[/]");
                        AnsiConsole.MarkupLine($"  Activity: [cyan]{Markup.Escape(activityName ?? resolvedActivity.ToString()!)}[/]");
                        AnsiConsole.MarkupLine($"  Hours:    [cyan]{resolvedHours}[/]");
                        AnsiConsole.MarkupLine($"  Date:     [cyan]{resolvedDate:yyyy-MM-dd}[/]");
                        if (resolvedComment is not null)
                            AnsiConsole.MarkupLine($"  Comment:  [cyan]{Markup.Escape(resolvedComment)}[/]");

                        if (!AnsiConsole.Confirm("Submit?", defaultValue: true))
                        {
                            step = firstStep;
                            resolvedHours = h;
                            resolvedDate = d is not null ? DateOnly.Parse(d) : null;
                            resolvedComment = c;
                            break;
                        }

                        TimesheetEntry entry;
                        try
                        {
                            entry = await client.Timesheet.LogHoursAsync(
                                resolvedActivity!.Value, resolvedProject!.Value, resolvedDate!.Value,
                                resolvedHours!.Value, resolvedComment, employeeId);
                        }
                        catch (TripletexApiException ex) when (ex.StatusCode == 409)
                        {
                            var existing = await client.Timesheet.SearchAsync(new TimesheetSearchOptions
                            {
                                EmployeeId = employeeId,
                                ProjectId = resolvedProject,
                                ActivityId = resolvedActivity,
                                DateFrom = resolvedDate,
                                DateTo = resolvedDate!.Value.AddDays(1),
                            });

                            var match = existing.Values?.FirstOrDefault();
                            if (match is null)
                            {
                                AnsiConsole.MarkupLine("[red]Conflict: hours already registered but could not find existing entry.[/]");
                                return;
                            }

                            AnsiConsole.MarkupLine($"[yellow]Already registered on {resolvedDate:yyyy-MM-dd}:[/]");
                            AnsiConsole.MarkupLine($"  Hours:   [cyan]{match.Hours}[/]");
                            if (!string.IsNullOrWhiteSpace(match.Comment))
                                AnsiConsole.MarkupLine($"  Comment: [cyan]{Markup.Escape(match.Comment)}[/]");

                            if (!AnsiConsole.Confirm($"Overwrite with [cyan]{resolvedHours}h[/]?", defaultValue: false))
                                return;

                            entry = await client.Timesheet.UpdateAsync(match.Id, new TimesheetEntryUpdate
                            {
                                Id = match.Id,
                                Version = match.Version,
                                Activity = new IdRef { Id = resolvedActivity!.Value },
                                Project = new IdRef { Id = resolvedProject!.Value },
                                Date = resolvedDate!.Value.ToString("yyyy-MM-dd"),
                                Hours = resolvedHours!.Value,
                                Comment = resolvedComment ?? "",
                                Employee = new IdRef { Id = employeeId },
                            });
                        }

                        if (json)
                        {
                            OutputFormatter.Print(entry, true);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine(
                                $"[green]Logged {resolvedHours}h on {resolvedDate:yyyy-MM-dd} \u2014 {Markup.Escape(projectName ?? resolvedProject.ToString()!)} / {Markup.Escape(activityName ?? resolvedActivity.ToString()!)}[/]");
                            await DisplayWeekAsync(client, employeeId, resolvedDate!.Value, "table");
                        }
                        return;
                    }
                }
            }
        });

        return cmd;
    }

    private static Command CreateLogWeekCommand(Option<bool> jsonOption)
    {
        var weekStart = new Argument<string>("week-start", "Monday of the week (yyyy-MM-dd)");
        var mon = new Argument<decimal>("mon", "Hours for Monday");
        var tue = new Argument<decimal>("tue", "Hours for Tuesday");
        var wed = new Argument<decimal>("wed", "Hours for Wednesday");
        var thu = new Argument<decimal>("thu", "Hours for Thursday");
        var fri = new Argument<decimal>("fri", "Hours for Friday");
        var sat = new Option<decimal>("--sat", () => 0, "Hours for Saturday");
        var sun = new Option<decimal>("--sun", () => 0, "Hours for Sunday");
        var comment = new Option<string?>("--comment", "Comment for all entries");
        var projectId = new Option<int?>("--project-id", "Project ID (overrides default)");
        var activityId = new Option<int?>("--activity-id", "Activity ID (overrides default)");

        var cmd = new Command("log-week", "Log hours for a full week")
        {
            weekStart, mon, tue, wed, thu, fri, sat, sun, comment, projectId, activityId
        };

        cmd.SetHandler(async (ctx) =>
        {
            var ws = DateOnly.Parse(ctx.ParseResult.GetValueForArgument(weekStart));
            var hoursPerDay = new[]
            {
                ctx.ParseResult.GetValueForArgument(mon),
                ctx.ParseResult.GetValueForArgument(tue),
                ctx.ParseResult.GetValueForArgument(wed),
                ctx.ParseResult.GetValueForArgument(thu),
                ctx.ParseResult.GetValueForArgument(fri),
                ctx.ParseResult.GetValueForOption(sat),
                ctx.ParseResult.GetValueForOption(sun),
            };
            var c = ctx.ParseResult.GetValueForOption(comment);
            var pid = ctx.ParseResult.GetValueForOption(projectId);
            var aid = ctx.ParseResult.GetValueForOption(activityId);
            var json = ctx.ParseResult.GetValueForOption(jsonOption);

            var config = ConfigStore.Load();
            ConfigStore.GetEmployeeId(config); // validate logged in
            var resolvedProject = pid ?? config.DefaultProjectId
                ?? throw new InvalidOperationException("No project specified. Use --project-id or 'project select'.");
            var resolvedActivity = aid ?? config.DefaultActivityId
                ?? throw new InvalidOperationException("No activity specified. Use --activity-id or 'activity select'.");

            using var client = ClientFactory.Create(config);
            var result = await client.Timesheet.LogWeekAsync(
                resolvedActivity, resolvedProject, ws, hoursPerDay, c);

            var entries = result.Values ?? [];

            if (json)
            {
                OutputFormatter.PrintList<TimesheetEntry>(entries, true);
            }
            else
            {
                var total = hoursPerDay.Sum();
                AnsiConsole.MarkupLine(
                    $"[green]Logged {entries.Count} entries ({total}h total) for week starting {ws:yyyy-MM-dd}[/]");
                var employeeId = ConfigStore.GetEmployeeId(config);
                await DisplayWeekAsync(client, employeeId, ws, "table");
            }
        });

        return cmd;
    }

    private static Command CreateListCommand(Option<bool> jsonOption)
    {
        var fromDate = new Option<string?>("--from-date", "Start date (yyyy-MM-dd)");
        var toDate = new Option<string?>("--to-date", "End date (yyyy-MM-dd)");
        var projectId = new Option<int?>("--project-id", "Filter by project ID");

        var cmd = new Command("list", "List your timesheet entries") { fromDate, toDate, projectId };

        cmd.SetHandler(async (fd, td, pid, json) =>
        {
            var config = ConfigStore.Load();
            var employeeId = ConfigStore.GetEmployeeId(config);
            using var client = ClientFactory.Create(config);

            var options = new TimesheetSearchOptions
            {
                DateFrom = fd is not null ? DateOnly.Parse(fd) : null,
                DateTo = td is not null ? DateOnly.Parse(td) : null,
                EmployeeId = employeeId,
                ProjectId = pid
            };

            var result = await client.Timesheet.SearchAsync(options);
            OutputFormatter.PrintList<TimesheetEntry>(result.Values ?? [], json);
        }, fromDate, toDate, projectId, jsonOption);

        return cmd;
    }

    private static Command CreateRecentCommand(Option<bool> jsonOption)
    {
        var cmd = new Command("recent", "Show your recent timesheet entries");

        cmd.SetHandler(async (json) =>
        {
            var config = ConfigStore.Load();
            var employeeId = ConfigStore.GetEmployeeId(config);
            using var client = ClientFactory.Create(config);
            var result = await client.Timesheet.GetRecentAsync(employeeId);
            OutputFormatter.PrintList<TimesheetEntry>(result.Values ?? [], json);
        }, jsonOption);

        return cmd;
    }

    private static T? FilterableSelect<T>(
        string title,
        IReadOnlyList<T> items,
        Func<T, string> searchText,
        Func<T, string> display,
        T? backSentinel = default,
        T? filterSentinel = default,
        int pageSize = 15) where T : class
    {
        while (true)
        {
            var choices = new List<T>(items);
            if (filterSentinel is not null && items.Count > pageSize)
                choices.Insert(backSentinel is not null ? 1 : 0, filterSentinel);

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<T>()
                    .Title(title)
                    .PageSize(pageSize)
                    .UseConverter(i =>
                        i == backSentinel ? BackSentinel :
                        i == filterSentinel ? FilterSentinelLabel :
                        display(i))
                    .AddChoices(choices));

            if (selected != filterSentinel)
                return selected;

            var filter = AnsiConsole.Prompt(
                new TextPrompt<string>("Filter:")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(filter))
                continue;

            var filtered = items
                .Where(i => i == backSentinel || searchText(i).Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (filtered.Count == 0 || (filtered.Count == 1 && filtered[0] == backSentinel))
            {
                AnsiConsole.MarkupLine($"[yellow]No matches for \"{Markup.Escape(filter)}\". Showing all.[/]");
                continue;
            }

            var result = AnsiConsole.Prompt(
                new SelectionPrompt<T>()
                    .Title($"{title} [dim](filtered: \"{Markup.Escape(filter)}\")[/]")
                    .PageSize(pageSize)
                    .UseConverter(i => i == backSentinel ? BackSentinel : display(i))
                    .AddChoices(filtered));

            return result;
        }
    }

    private static async Task<(int id, string? name, bool isBack)?> PromptProjectAsync(
        TripletexClient client, CliConfig config, bool canGoBack)
    {
        AnsiConsole.MarkupLine("[dim]Fetching projects...[/]");
        var result = await client.Project.SearchAsync();
        var projects = result.Values ?? [];

        if (projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No projects found.[/]");
            return null;
        }

        var sorted = projects.OrderBy(p => p.Name).ToList();
        var backSentinel = canGoBack ? new Project { Id = -1, Name = BackSentinel } : null;
        if (backSentinel is not null) sorted.Insert(0, backSentinel);

        var selected = FilterableSelect(
            "Select project",
            sorted,
            p => $"{p.Name} {p.Number}",
            p => $"{p.Name} ({p.Number ?? "no number"}) [dim]ID: {p.Id}[/]",
            backSentinel,
            filterSentinel: new Project { Id = -2 });

        if (selected is null || selected == backSentinel)
            return (0, null, true);

        if (AnsiConsole.Confirm("Save as default project?", defaultValue: false))
        {
            config.DefaultProjectId = selected.Id;
            config.DefaultProjectName = selected.Name;
            ConfigStore.Save(config);
            AnsiConsole.MarkupLine($"[green]Saved default project: {Markup.Escape(selected.Name ?? "")} (ID: {selected.Id})[/]");
        }

        return (selected.Id, selected.Name, false);
    }

    private static async Task<(int id, string? name, bool isBack)?> PromptActivityAsync(
        TripletexClient client, CliConfig config, int projectId, bool canGoBack)
    {
        AnsiConsole.MarkupLine("[dim]Fetching activities for project...[/]");
        var project = await client.Project.GetAsync(projectId, fields: "projectActivities(activity(*))");
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
            return null;
        }

        var backSentinel = canGoBack ? new Activity { Id = -1, Name = BackSentinel } : null;
        if (backSentinel is not null) activities.Insert(0, backSentinel);

        var selected = FilterableSelect(
            "Select activity",
            activities,
            a => a.DisplayName ?? a.Name ?? "",
            a => $"{a.DisplayName ?? a.Name ?? "Unnamed"} [dim]ID: {a.Id}[/]",
            backSentinel,
            filterSentinel: new Activity { Id = -2 });

        if (selected is null || selected == backSentinel)
            return (0, null, true);

        var activityName = selected.DisplayName ?? selected.Name ?? $"Activity {selected.Id}";

        if (AnsiConsole.Confirm("Save as default activity?", defaultValue: false))
        {
            config.DefaultActivityId = selected.Id;
            config.DefaultActivityName = activityName;
            ConfigStore.Save(config);
            AnsiConsole.MarkupLine($"[green]Saved default activity: {Markup.Escape(activityName)} (ID: {selected.Id})[/]");
        }

        return (selected.Id, activityName, false);
    }

    private static Command CreateWeekCommand(Option<bool> jsonOption)
    {
        var dateOption = new Option<string?>("--date", "Any date in the target week (yyyy-MM-dd), defaults to today");
        var styleOption = new Option<string>("--style", () => "table", "Display style: table, grid, or compact");

        var cmd = new Command("week", "Show weekly hours summary with project breakdown") { dateOption, styleOption };

        cmd.SetHandler(async (d, style, json) =>
        {
            var targetDate = d is not null ? DateOnly.Parse(d) : DateOnly.FromDateTime(DateTime.Today);
            var config = ConfigStore.Load();
            var employeeId = ConfigStore.GetEmployeeId(config);
            using var client = ClientFactory.Create(config);

            await DisplayWeekAsync(client, employeeId, targetDate, style, json);
        }, dateOption, styleOption, jsonOption);

        return cmd;
    }

    internal static async Task DisplayWeekAsync(TripletexClient client, int employeeId, DateOnly dateInWeek, string style = "table", bool json = false)
    {
        var monday = dateInWeek.AddDays(-(int)dateInWeek.DayOfWeek + (int)DayOfWeek.Monday);
        if (dateInWeek.DayOfWeek == DayOfWeek.Sunday)
            monday = monday.AddDays(-7);
        var sunday = monday.AddDays(6);

        var result = await client.Timesheet.SearchAsync(new TimesheetSearchOptions
        {
            EmployeeId = employeeId,
            DateFrom = monday,
            DateTo = sunday.AddDays(1),
            Count = 1000,
            Sorting = "date",
        });

        var entries = result.Values ?? [];

        if (json)
        {
            OutputFormatter.PrintList<TimesheetEntry>(entries, true);
            return;
        }

        var projectIds = entries
            .Where(e => e.Project is not null)
            .Select(e => e.Project!.Id)
            .Distinct()
            .ToList();

        var projectNames = new Dictionary<int, string>();
        foreach (var pid in projectIds)
        {
            try
            {
                var project = await client.Project.GetAsync(pid, fields: "id,name");
                projectNames[pid] = project.Name ?? $"Project {pid}";
            }
            catch
            {
                projectNames[pid] = $"Project {pid}";
            }
        }

        var weekNumber = ISOWeek.GetWeekOfYear(monday.ToDateTime(TimeOnly.MinValue));
        var weekLabel = $"Week {weekNumber} ({monday:MMM d} - {sunday:MMM d})";

        const decimal weeklyTarget = 37.5m;

        switch (style.ToLowerInvariant())
        {
            case "compact":
                DisplayCompact(entries, projectNames, monday, weekLabel, weeklyTarget);
                break;
            case "grid":
                DisplayGrid(entries, monday, weekLabel, weeklyTarget);
                break;
            default:
                DisplayTable(entries, projectNames, monday, weekLabel, weeklyTarget);
                break;
        }
    }

    private static void DisplayTable(IReadOnlyList<TimesheetEntry> entries, Dictionary<int, string> projectNames, DateOnly monday, string weekLabel, decimal weeklyTarget)
    {
        var dayNames = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        var grouped = entries
            .Where(e => e.Project is not null && e.Date is not null)
            .GroupBy(e => e.Project!.Id)
            .ToDictionary(g => g.Key, g => g.ToList());

        var hasSatSun = entries.Any(e =>
        {
            if (e.Date is null) return false;
            var d = DateOnly.Parse(e.Date);
            return d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        });

        var dayCount = hasSatSun ? 7 : 5;

        AnsiConsole.MarkupLine($"\n[bold]{Markup.Escape(weekLabel)}[/]");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("Project").NoWrap());

        for (var i = 0; i < dayCount; i++)
            table.AddColumn(new TableColumn(dayNames[i]).Centered());
        table.AddColumn(new TableColumn("[bold]Total[/]").Centered());

        foreach (var (projectId, projectEntries) in grouped.OrderBy(g => projectNames.GetValueOrDefault(g.Key, "")))
        {
            var row = new List<string> { $"[cyan]{Markup.Escape(projectNames.GetValueOrDefault(projectId, $"Project {projectId}"))}[/]" };
            decimal projectTotal = 0;

            for (var i = 0; i < dayCount; i++)
            {
                var day = monday.AddDays(i);
                var dayHours = projectEntries
                    .Where(e => e.Date is not null && DateOnly.Parse(e.Date) == day)
                    .Sum(e => e.Hours);
                projectTotal += dayHours;
                row.Add(dayHours > 0 ? $"{dayHours:0.#}" : "[dim]-[/]");
            }
            row.Add($"[bold]{projectTotal:0.#}[/]");
            table.AddRow(row.ToArray());
        }

        table.AddEmptyRow();

        var totalRow = new List<string> { "[bold]Total[/]" };
        decimal grandTotal = 0;
        for (var i = 0; i < dayCount; i++)
        {
            var day = monday.AddDays(i);
            var dayTotal = entries
                .Where(e => e.Date is not null && DateOnly.Parse(e.Date) == day)
                .Sum(e => e.Hours);
            grandTotal += dayTotal;
            totalRow.Add($"[bold]{(dayTotal > 0 ? $"{dayTotal:0.#}" : "-")}[/]");
        }
        totalRow.Add($"[bold]{grandTotal:0.#}[/]");
        table.AddRow(totalRow.ToArray());

        AnsiConsole.Write(table);

        var check = grandTotal >= weeklyTarget ? "[green]\u2713[/]" : $"[yellow]{weeklyTarget - grandTotal:0.#}h remaining[/]";
        AnsiConsole.MarkupLine($"  {grandTotal:0.#} / {weeklyTarget}h {check}");
    }

    private static void DisplayGrid(IReadOnlyList<TimesheetEntry> entries, DateOnly monday, string weekLabel, decimal weeklyTarget)
    {
        AnsiConsole.MarkupLine($"\n[bold]{Markup.Escape(weekLabel)}[/]");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Day")
            .AddColumn(new TableColumn("Hours").Centered())
            .AddColumn("Visual");

        decimal total = 0;
        for (var i = 0; i < 7; i++)
        {
            var day = monday.AddDays(i);
            var dayHours = entries
                .Where(e => e.Date is not null && DateOnly.Parse(e.Date) == day)
                .Sum(e => e.Hours);
            total += dayHours;

            if (i >= 5 && dayHours == 0) continue;

            var barFilled = (int)Math.Round((double)dayHours);
            var barEmpty = Math.Max(0, 8 - barFilled);
            var bar = new string('\u2588', barFilled) + new string('\u2591', barEmpty);
            var color = dayHours >= 7.5m ? "green" : dayHours > 0 ? "yellow" : "dim";

            table.AddRow(
                $"{day:ddd M/d}",
                $"[{color}]{dayHours:0.#}[/]",
                $"[{color}]{bar}[/]");
        }

        AnsiConsole.Write(table);

        var check = total >= weeklyTarget ? "[green]\u2713[/]" : $"[yellow]{weeklyTarget - total:0.#}h remaining[/]";
        AnsiConsole.MarkupLine($"  Total: {total:0.#} / {weeklyTarget}h {check}");
    }

    private static void DisplayCompact(IReadOnlyList<TimesheetEntry> entries, Dictionary<int, string> projectNames, DateOnly monday, string weekLabel, decimal weeklyTarget)
    {
        AnsiConsole.MarkupLine($"\n[bold]{Markup.Escape(weekLabel)}[/]");

        decimal total = 0;
        for (var i = 0; i < 7; i++)
        {
            var day = monday.AddDays(i);
            var dayHours = entries
                .Where(e => e.Date is not null && DateOnly.Parse(e.Date) == day)
                .Sum(e => e.Hours);
            total += dayHours;

            if (i >= 5 && dayHours == 0) continue;

            var barFilled = (int)Math.Round((double)dayHours);
            var bar = new string('\u2588', barFilled);
            var color = dayHours >= 7.5m ? "green" : dayHours > 0 ? "yellow" : "dim";

            AnsiConsole.MarkupLine($"  {day:ddd}  [{color}]{dayHours,4:0.#}h  {bar}[/]");
        }

        AnsiConsole.WriteLine();
        var check = total >= weeklyTarget ? "[green]\u2713[/]" : $"[yellow]{weeklyTarget - total:0.#}h remaining[/]";
        AnsiConsole.MarkupLine($"  Total: {total:0.#} / {weeklyTarget}h {check}");
    }
}
