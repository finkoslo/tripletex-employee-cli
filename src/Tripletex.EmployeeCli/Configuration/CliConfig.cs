using System.Text.Json.Serialization;

namespace Tripletex.EmployeeCli.Configuration;

public sealed class CliConfig
{
    [JsonPropertyName("consumerToken")]
    public string? ConsumerToken { get; set; }

    [JsonPropertyName("employeeToken")]
    public string? EmployeeToken { get; set; }

    [JsonPropertyName("employeeId")]
    public long? EmployeeId { get; set; }

    [JsonPropertyName("employeeName")]
    public string? EmployeeName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    [JsonPropertyName("defaultProjectId")]
    public long? DefaultProjectId { get; set; }

    [JsonPropertyName("defaultProjectName")]
    public string? DefaultProjectName { get; set; }

    [JsonPropertyName("defaultActivityId")]
    public long? DefaultActivityId { get; set; }

    [JsonPropertyName("defaultActivityName")]
    public string? DefaultActivityName { get; set; }

    [JsonPropertyName("tenant")]
    public string? Tenant { get; set; }
}
