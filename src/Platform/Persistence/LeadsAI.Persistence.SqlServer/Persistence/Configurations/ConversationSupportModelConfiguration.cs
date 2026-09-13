using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;

namespace LeadsAI.Persistence.SqlServer.Configurations;

internal static class ConversationSupportModelConfiguration
{
    internal static void ConfigureConversationSupportModel(this ModelBuilder builder)
    {
        builder.Entity<Message>()
            .HasIndex(x => new { x.TenantId, x.ConversationId, x.CreatedAtUtc });
    }
}
