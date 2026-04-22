using System.Globalization;
using Spectre.Console;

namespace Tripletex.EmployeeCli;

public static class NumberPrompt
{
    public static decimal? AskHours(string label)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException($"{label} prompt requires an interactive terminal.");

        var escapedLabel = Markup.Escape(label);
        var buffer = "";
        string? error = null;

        Render(escapedLabel, buffer, error);
        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    RenderCancelled(escapedLabel);
                    return null;
                case ConsoleKey.Backspace:
                    if (buffer.Length > 0) buffer = buffer[..^1];
                    error = null;
                    break;
                case ConsoleKey.Enter:
                    if (decimal.TryParse(buffer, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0)
                    {
                        RenderCommitted(escapedLabel, buffer);
                        return value;
                    }
                    error = buffer.Length == 0 ? "required" : "must be > 0";
                    break;
                default:
                    if (IsNumberChar(key.KeyChar, buffer))
                        buffer += key.KeyChar;
                    break;
            }
            Render(escapedLabel, buffer, error);
        }
    }

    private static bool IsNumberChar(char c, string buffer) =>
        (c >= '0' && c <= '9')
        || ((c == '.' || c == ',') && !buffer.Contains('.') && !buffer.Contains(','));

    private static void Render(string label, string buffer, string? error)
    {
        Console.Write('\r');
        AnsiConsole.Markup($"{label} {Markup.Escape(buffer)}");
        if (error is not null) AnsiConsole.Markup($"  [red]{Markup.Escape(error)}[/]");
        AnsiConsole.Markup("[grey]  Esc back · Enter confirms[/]");
        Console.Write("\x1b[K");
    }

    private static void RenderCommitted(string label, string buffer)
    {
        Console.Write('\r');
        AnsiConsole.Markup($"{label} [cyan]{Markup.Escape(buffer)}[/]");
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

public static class TextLinePrompt
{
    public readonly record struct Result(bool Cancelled, string Value);

    public static Result Ask(string label, bool allowEmpty = true)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException($"{label} prompt requires an interactive terminal.");

        var escapedLabel = Markup.Escape(label);
        var buffer = "";

        Render(escapedLabel, buffer);
        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    RenderCancelled(escapedLabel);
                    return new Result(true, "");
                case ConsoleKey.Backspace:
                    if (buffer.Length > 0) buffer = buffer[..^1];
                    break;
                case ConsoleKey.Enter:
                    if (buffer.Length == 0 && !allowEmpty) break;
                    RenderCommitted(escapedLabel, buffer);
                    return new Result(false, buffer);
                default:
                    if (!char.IsControl(key.KeyChar))
                        buffer += key.KeyChar;
                    break;
            }
            Render(escapedLabel, buffer);
        }
    }

    private static void Render(string label, string buffer)
    {
        Console.Write('\r');
        AnsiConsole.Markup($"{label} {Markup.Escape(buffer)}");
        AnsiConsole.Markup("[grey]  Esc back · Enter confirms[/]");
        Console.Write("\x1b[K");
    }

    private static void RenderCommitted(string label, string buffer)
    {
        Console.Write('\r');
        if (buffer.Length == 0)
            AnsiConsole.Markup($"{label} [dim](empty)[/]");
        else
            AnsiConsole.Markup($"{label} [cyan]{Markup.Escape(buffer)}[/]");
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

public static class ConfirmPrompt
{
    public static bool? Ask(string label, bool defaultValue = true)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException($"{label} prompt requires an interactive terminal.");

        var escapedLabel = Markup.Escape(label);

        Render(escapedLabel, defaultValue);
        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    RenderCancelled(escapedLabel);
                    return null;
                case ConsoleKey.Enter:
                    RenderCommitted(escapedLabel, defaultValue);
                    return defaultValue;
                case ConsoleKey.Y:
                    RenderCommitted(escapedLabel, true);
                    return true;
                case ConsoleKey.N:
                    RenderCommitted(escapedLabel, false);
                    return false;
            }
        }
    }

    private static void Render(string label, bool defaultValue)
    {
        Console.Write('\r');
        var hint = defaultValue ? "[Y/n]" : "[y/N]";
        AnsiConsole.Markup($"{label} [grey]{hint}[/]  [grey]Esc back[/]");
        Console.Write("\x1b[K");
    }

    private static void RenderCommitted(string label, bool value)
    {
        Console.Write('\r');
        AnsiConsole.Markup($"{label} [cyan]{(value ? "yes" : "no")}[/]");
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
