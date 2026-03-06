using Tripletex.Api;
using Tripletex.EmployeeCli.Configuration;

namespace Tripletex.EmployeeCli;

public static class ClientFactory
{
    public static TripletexClient Create(CliConfig config)
    {
        var env = config.Environment?.ToLowerInvariant() switch
        {
            "test" => TripletexEnvironment.Test,
            _ => TripletexEnvironment.Production
        };

        var options = new TripletexOptions
        {
            ConsumerToken = ConfigStore.GetConsumerToken(config),
            EmployeeToken = ConfigStore.GetEmployeeToken(config),
            Environment = env
        };

        return new TripletexClient(options);
    }
}
