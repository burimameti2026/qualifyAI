namespace QualifyAI.BuildingBlocks.Messaging.Outbox;
public interface IOutboxWriter
{
    void Add<T>(T message) where T : IntegrationEvent;
}
