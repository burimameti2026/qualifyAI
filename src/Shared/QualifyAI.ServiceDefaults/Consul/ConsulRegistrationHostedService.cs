using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace QualifyAI.ServiceDefaults.Consul;

public sealed class ConsulRegistrationHostedService(
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<ConsulRegistrationHostedService> logger) : IHostedService
{
    private ConsulClient? _client;
    private string? _registrationId;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var section = configuration.GetSection(ConsulRegistrationOptions.SectionName);
        var options = section.Get<ConsulRegistrationOptions>() ?? new();

        options.ServiceName = Environment.GetEnvironmentVariable("SERVICE_NAME")
            ?? options.ServiceName
            ?? "";

        if (string.IsNullOrWhiteSpace(options.ServiceName))
        {
            logger.LogWarning("Consul registration skipped because ServiceName is empty.");
            return;
        }

        options.ServiceAddress = Environment.GetEnvironmentVariable("SERVICE_ADDRESS")
            ?? options.ServiceAddress;

        if (string.IsNullOrWhiteSpace(options.ServiceAddress))
            options.ServiceAddress = options.ServiceName;

        if (int.TryParse(Environment.GetEnvironmentVariable("SERVICE_PORT"), out var port))
            options.ServicePort = port;

        var consulAddress = Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR")
            ?? options.Address;

        _client = new ConsulClient(c => c.Address = new Uri(consulAddress));
        _registrationId = $"{options.ServiceName}-{Guid.NewGuid():N}";

        var registration = new AgentServiceRegistration
        {
            ID = _registrationId,
            Name = options.ServiceName,
            Address = options.ServiceAddress,
            Port = options.ServicePort,
            Tags = options.Tags,
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{options.ServiceAddress}:{options.ServicePort}{options.HealthPath}",
                Interval = TimeSpan.FromSeconds(10),
                Timeout = TimeSpan.FromSeconds(5),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
            }
        };

        await _client.Agent.ServiceRegister(registration, cancellationToken);
        logger.LogInformation(
            "Registered {ServiceName} in Consul as {RegistrationId} at {Address}:{Port}",
            options.ServiceName, _registrationId, options.ServiceAddress, options.ServicePort);

        lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                if (_client is not null && _registrationId is not null)
                    _client.Agent.ServiceDeregister(_registrationId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deregister {RegistrationId} from Consul", _registrationId);
            }
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client is not null && _registrationId is not null)
            await _client.Agent.ServiceDeregister(_registrationId, cancellationToken);
        _client?.Dispose();
    }
}
