namespace QualifyAI.ServiceDefaults.Consul;
public sealed class ConsulRegistrationOptions
{
    public const string SectionName = "Consul";
    public string Address { get; set; } = "http://consul:8500";
    public string ServiceName { get; set; } = "";
    public string ServiceAddress { get; set; } = "";
    public int ServicePort { get; set; } = 8080;
    public string HealthPath { get; set; } = "/health";
    public string[] Tags { get; set; } = [];
}
