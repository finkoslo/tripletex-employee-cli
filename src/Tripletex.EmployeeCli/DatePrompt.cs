using System.Globalization;
using Spectre.Console;

namespace Tripletex.EmployeeCli;

public static class DatePrompt
{
    private const string NavHint = "[grey]  ↑/↓ day · Shift+↑/↓ week · PgUp/PgDn month · type to edit · Esc back · Enter[/]";
    private const string EditHint = "[grey]  type yyyy-MM-dd · Esc cancels · Enter confirms[/]";
    private const string InvalidMarker = "  [red]invalid[/]";

    public static DateOnly? Ask(string label, DateOnly initial)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException($"{label} prompt requires an interactive terminal.");

        var escapedLabel = Markup.Escape(label);
        var date = initial;
        string? buffer = null;
        var showInvalid = false;

        RenderNav(escapedLabel, date);
        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (buffer is null)
            {
                var shift = (key.Modifiers & ConsoleModifiers.Shift) != 0;
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        date = date.AddDays(shift ? 7 : 1);
                        break;
                    case ConsoleKey.DownArrow:
                        date = date.AddDays(shift ? -7 : -1);
                        break;
                    case ConsoleKey.PageUp:
                        date = date.AddMonths(1);
                        break;
                    case ConsoleKey.PageDown:
                        date = date.AddMonths(-1);
                        break;
                    case ConsoleKey.Enter:
                        RenderCommitted(escapedLabel, date);
                        return date;
                    case ConsoleKey.Escape:
                        RenderCancelled(escapedLabel);
                        return null;
                    default:
                        if (IsDateChar(key.KeyChar))
                        {
                            buffer = key.KeyChar.ToString();
                            showInvalid = false;
                        }
                        break;
                }
                if (buffer is null)
                    RenderNav(escapedLabel, date);
                else
                    RenderEdit(escapedLabel, buffer, showInvalid);
                continue;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    buffer = null;
                    showInvalid = false;
                    RenderNav(escapedLabel, date);
                    break;
                case ConsoleKey.Backspace:
                    if (buffer.Length > 0) buffer = buffer[..^1];
                    showInvalid = false;
                    RenderEdit(escapedLabel, buffer, showInvalid);
                    break;
                case ConsoleKey.Enter:
                    if (DateOnly.TryParseExact(buffer, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        date = parsed;
                        RenderCommitted(escapedLabel, date);
                        return date;
                    }
                    showInvalid = true;
                    RenderEdit(escapedLabel, buffer, showInvalid);
                    break;
                default:
                    if (IsDateChar(key.KeyChar) && buffer.Length < 10)
                    {
                        buffer += key.KeyChar;
                        showInvalid = false;
                    }
                    RenderEdit(escapedLabel, buffer, showInvalid);
                    break;
            }
        }
    }

    private static bool IsDateChar(char c) => (c >= '0' && c <= '9') || c == '-';

    private static void RenderNav(string label, DateOnly date)
    {
        Console.Write('\r');
        AnsiConsole.Markup($"{label} [cyan]{date:yyyy-MM-dd}[/]{NavHint}");
        Console.Write("\x1b[K");
    }

    private static void RenderEdit(string label, string buffer, bool invalid)
    {
        Console.Write('\r');
        AnsiConsole.Markup($"{label} {Markup.Escape(buffer)}");
        if (invalid) AnsiConsole.Markup(InvalidMarker);
        AnsiConsole.Markup(EditHint);
        Console.Write("\x1b[K");
    }

    private static void RenderCommitted(string label, DateOnly date)
    {
        Console.Write('\r');
        AnsiConsole.Markup($"{label} [cyan]{date:yyyy-MM-dd}[/]");
        Console.Write("\x1b[K");
        Console.WriteLine();
    }

    private static void RenderCancelled(string label)
    {
        Console.Write('\r');
        AnsiConsole.Markup($"{label} [dim]cancelled[/]");
        Console.Write("\x1b[K");
        Console.WriteLine();
    }
}
