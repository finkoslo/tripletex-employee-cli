using System.CommandLine;
using Tripletex.Api.Operations;
using Tripletex.EmployeeCli.Configuration;

namespace Tripletex.EmployeeCli.Commands;

public static class ProjectCommand
{
    public static Command Create(Option<bool> jsonOption)
    {
        var cmd = new Command("project", "Browse projects");
        cmd.AddCommand(CreateListCommand(jsonOption));
        cmd.AddCommand(CreateSearchCommand(jsonOption));
        return cmd;
    }

    private static Command CreateListCommand(Option<bool> jsonOption)
    {
        var cmd = new Command("list", "List all projects");

        cmd.SetHandler(async (json) =>
        {
            var config = ConfigStore.Load();
            ConfigStore.GetEmployeeId(config); // validate logged in
            using var client = ClientFactory.Create(config);
            var result = await client.Project.SearchAsync();
            OutputFormatter.PrintList<Project>(result.Values ?? [], json);
        }, jsonOption);

        return cmd;
    }

    private static Command CreateSearchCommand(Option<bool> jsonOption)
    {
        var name = new Option<string?>("--name", "Search by project name");
        var cmd = new Command("search", "Search projects") { name };

        cmd.SetHandler(async (n, json) =>
        {
            var config = ConfigStore.Load();
            ConfigStore.GetEmployeeId(config); // validate logged in
            using var client = ClientFactory.Create(config);
            var result = await client.Project.SearchAsync(name: n);
            OutputFormatter.PrintList<Project>(result.Values ?? [], json);
        }, name, jsonOption);

        return cmd;
    }
}
