using MongoDB.Driver;
namespace QualifyAI.Knowledge.Infrastructure.Mongo;

public sealed class MongoKnowledgeChunkStore(IMongoCollection<KnowledgeChunkDocument> collection)
    : IKnowledgeChunkStore
{
    public Task UpsertAsync(KnowledgeChunkDocument document, CancellationToken ct = default)
    {
        var filter = Builders<KnowledgeChunkDocument>.Filter.And(
            Builders<KnowledgeChunkDocument>.Filter.Eq(x => x.TenantId, document.TenantId),
            Builders<KnowledgeChunkDocument>.Filter.Eq(x => x.SourceId, document.SourceId),
            Builders<KnowledgeChunkDocument>.Filter.Eq(x => x.ChunkIndex, document.ChunkIndex));

        return collection.ReplaceOneAsync(
            filter, document,
            new ReplaceOptions { IsUpsert = true },
            ct);
    }

    public async Task<IReadOnlyList<KnowledgeChunkDocument>> GetBySourceAsync(
        Guid tenantId, Guid sourceId, CancellationToken ct = default)
        => await collection.Find(x => x.TenantId == tenantId && x.SourceId == sourceId)
            .SortBy(x => x.ChunkIndex)
            .ToListAsync(ct);

    public Task DeleteSourceAsync(Guid tenantId, Guid sourceId, CancellationToken ct = default)
        => collection.DeleteManyAsync(x => x.TenantId == tenantId && x.SourceId == sourceId, ct);
}
