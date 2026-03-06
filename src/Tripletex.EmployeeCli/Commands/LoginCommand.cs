using System.CommandLine;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Tripletex.EmployeeCli.Configuration;

namespace Tripletex.EmployeeCli.Commands;

public static class LoginCommand
{
    private const int TimeoutSeconds = 120;

    public static Command Create()
    {
        var cmd = new Command("login", "Authenticate via Google OAuth");

        cmd.SetHandler(async () =>
        {
            var authUrl = System.Environment.GetEnvironmentVariable("FINKLETEX_AUTH_URL")
                ?? "https://authfinkletexspkdnilo-finkletext-oauth.functions.fnc.nl-ams.scw.cloud";
            var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var port = GetAvailablePort();
            var prefix = $"http://localhost:{port}/";

            using var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            var loginUrl = $"{authUrl}/auth?port={port}&state={Uri.EscapeDataString(state)}";
            AnsiConsole.MarkupLine("[dim]Opening browser for authentication...[/]");
            OpenBrowser(loginUrl);
            AnsiConsole.MarkupLine($"[dim]If browser didn't open, visit:[/]");
            AnsiConsole.MarkupLine($"[link]{Markup.Escape(loginUrl)}[/]");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[red]Login timed out. Please try again.[/]");
                return;
            }

            var query = context.Request.QueryString;
            var payloadParam = query["payload"];
            var returnedState = query["state"];

            var responseHtml = "<html><body style='font-family:sans-serif;padding:2em'>";

            if (returnedState != state)
            {
                responseHtml += "<h2>Login Failed</h2><p>State mismatch. Please try again.</p>";
                SendResponse(context, responseHtml + "</body></html>", 400);
                AnsiConsole.MarkupLine("[red]Login failed: state mismatch.[/]");
                return;
            }

            if (string.IsNullOrEmpty(payloadParam))
            {
                var error = query["error"] ?? "Unknown error";
                responseHtml += $"<h2>Login Failed</h2><p>{error}</p>";
                SendResponse(context, responseHtml + "</body></html>", 400);
                AnsiConsole.MarkupLine($"[red]Login failed: {Markup.Escape(error)}[/]");
                return;
            }

            var parts = payloadParam.Split('.');
            if (parts.Length != 2)
            {
                responseHtml += "<h2>Login Failed</h2><p>Invalid payload format.</p>";
                SendResponse(context, responseHtml + "</body></html>", 400);
                AnsiConsole.MarkupLine("[red]Login failed: invalid payload format.[/]");
                return;
            }

            var dataBytes = Convert.FromBase64String(PadBase64Url(parts[0]));
            var dataJson = Encoding.UTF8.GetString(dataBytes);
            var payload = JsonSerializer.Deserialize<LoginPayload>(dataJson);

            if (payload is null)
            {
                responseHtml += "<h2>Login Failed</h2><p>Could not parse payload.</p>";
                SendResponse(context, responseHtml + "</body></html>", 400);
                AnsiConsole.MarkupLine("[red]Login failed: could not parse payload.[/]");
                return;
            }

            if (payload.Exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                responseHtml += "<h2>Login Failed</h2><p>Token expired. Please try again.</p>";
                SendResponse(context, responseHtml + "</body></html>", 400);
                AnsiConsole.MarkupLine("[red]Login failed: token expired.[/]");
                return;
            }

            var config = new CliConfig
            {
                ConsumerToken = payload.ConsumerToken,
                EmployeeToken = payload.EmployeeToken,
                EmployeeId = payload.EmployeeId,
                EmployeeName = payload.EmployeeName,
                Email = payload.Email,
                Environment = "production"
            };

            var existing = ConfigStore.Load();
            config.DefaultProjectId = existing.DefaultProjectId;
            config.DefaultProjectName = existing.DefaultProjectName;
            config.DefaultActivityId = existing.DefaultActivityId;
            config.DefaultActivityName = existing.DefaultActivityName;

            ConfigStore.Save(config);

            responseHtml += $"<h2>Login Successful!</h2><p>Logged in as <b>{payload.Email}</b>.</p><p>You can close this window.</p>";
            SendResponse(context, responseHtml + "</body></html>", 200);

            AnsiConsole.MarkupLine($"[green]Logged in as {Markup.Escape(payload.Email ?? "")} (Employee ID: {payload.EmployeeId})[/]");
        });

        return cmd;
    }

    private static int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void SendResponse(HttpListenerContext context, string html, int statusCode)
    {
        var buffer = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer);
        context.Response.OutputStream.Close();
    }

    private static string PadBase64Url(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        return s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    }

    private sealed class LoginPayload
    {
        [JsonPropertyName("consumerToken")]
        public string? ConsumerToken { get; set; }
        [JsonPropertyName("employeeToken")]
        public string? EmployeeToken { get; set; }
        [JsonPropertyName("employeeId")]
        public int EmployeeId { get; set; }
        [JsonPropertyName("employeeName")]
        public string? EmployeeName { get; set; }
        [JsonPropertyName("email")]
        public string? Email { get; set; }
        [JsonPropertyName("exp")]
        public long Exp { get; set; }
    }
}
