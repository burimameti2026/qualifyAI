using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Persistence.Configurations;

internal static class ConversationSupportModelConfiguration
{
    internal static void ConfigureConversationSupportModel(this ModelBuilder builder)
    {
        builder.Entity<Message>()
            .HasIndex(x => new { x.TenantId, x.ConversationId, x.CreatedAtUtc });
    }
}
