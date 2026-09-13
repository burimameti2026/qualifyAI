using LeadsAI.BuildingBlocks.Messaging;
using LeadsAI.BuildingBlocks.Messaging.Outbox;
using LeadsAI.Identity.Persistence.SqlServer;

namespace LeadsAI.Identity.Infrastructure.Messaging;

public sealed class IdentityOutboxWriter(IdentityDbContext dbContext) : IOutboxWriter
{
    public void Add<T>(T message) where T : IntegrationEvent
        => dbContext.OutboxMessages.Add(OutboxEnvelopeFactory.Create(message));
}
