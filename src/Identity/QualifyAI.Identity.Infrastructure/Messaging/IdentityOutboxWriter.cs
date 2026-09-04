using QualifyAI.BuildingBlocks.Messaging;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Identity.Persistence.SqlServer;

namespace QualifyAI.Identity.Infrastructure.Messaging;

public sealed class IdentityOutboxWriter(IdentityDbContext dbContext) : IOutboxWriter
{
    public void Add<T>(T message) where T : IntegrationEvent
        => dbContext.OutboxMessages.Add(OutboxEnvelopeFactory.Create(message));
}
