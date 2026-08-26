namespace QualifyAI.Automation.Domain.Definitions;
public sealed record AutomationStep(Guid Id, string Type, string ConfigurationJson, int Order);
