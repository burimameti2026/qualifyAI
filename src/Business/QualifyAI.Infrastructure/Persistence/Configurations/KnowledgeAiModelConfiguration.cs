using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Persistence.Configurations;

internal static class KnowledgeAiModelConfiguration
{
    internal static void ConfigureKnowledgeAiModel(this ModelBuilder builder)
    {
        builder.Entity<KnowledgeChunk>()
            .HasIndex(x => new { x.TenantId, x.DocumentId, x.ChunkIndex });
    }
}
