namespace QualifyAI.BuildingBlocks.Messaging;
public abstract record IntegrationEvent(Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId);
