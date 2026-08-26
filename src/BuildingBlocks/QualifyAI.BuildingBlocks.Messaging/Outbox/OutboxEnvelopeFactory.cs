using System.Text.Json;
namespace QualifyAI.BuildingBlocks.Messaging.Outbox;
public static class OutboxEnvelopeFactory
{
    public static OutboxMessage Create<T>(T message) where T : IntegrationEvent =>
        new()
        {
            Id = message.EventId,
            OccurredAtUtc = message.OccurredAtUtc,
            Type = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name,
            Payload = JsonSerializer.Serialize(message, typeof(T))
        };
}
