using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Persistence.Configurations;

internal static class BusinessEntityModelConfiguration
{
    internal static void ConfigureBusinessEntityKeys(this ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
                builder.Entity(entityType.ClrType).HasKey(nameof(Entity.Id));
        }
    }
}
