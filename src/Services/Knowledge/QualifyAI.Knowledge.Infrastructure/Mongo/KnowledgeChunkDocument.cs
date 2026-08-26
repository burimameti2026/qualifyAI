using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace QualifyAI.Knowledge.Infrastructure.Mongo;

public sealed class KnowledgeChunkDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid KnowledgeBaseId { get; set; }
    public Guid SourceId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = "";
    public float[] Embedding { get; set; } = [];
    public Dictionary<string,string> Metadata { get; set; } = new();
    public DateTime IndexedAtUtc { get; set; } = DateTime.UtcNow;
}
