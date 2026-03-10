namespace Tripletex.EmployeeCli;

public static class PipeMode
{
    public static bool IsInputRedirected => Console.IsInputRedirected;
    public static bool IsOutputRedirected => Console.IsOutputRedirected;

    public static bool ResolveJson(bool jsonFlag) =>
        jsonFlag || IsOutputRedirected;

    public static string? ReadStdin()
    {
        if (!IsInputRedirected) return null;
        return Console.In.ReadToEnd();
    }

    public static void RequireInteractive(string feature)
    {
        if (IsInputRedirected)
            throw new InvalidOperationException(
                $"{feature} requires an interactive terminal. Provide all arguments via flags or pipe JSON to stdin.");
    }
}
