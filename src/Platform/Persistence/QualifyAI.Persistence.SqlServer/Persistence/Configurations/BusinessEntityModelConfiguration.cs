using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Persistence.SqlServer.Configurations;

internal static class BusinessEntityModelConfiguration
{
    internal static void ConfigureBusinessEntityKeys(this ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
                builder.Entity(entityType.ClrType).HasKey(nameof(Entity.Id));

            foreach (var property in entityType.GetProperties()
                         .Where(x => x.ClrType == typeof(decimal) || x.ClrType == typeof(decimal?)))
            {
                builder.Entity(entityType.ClrType)
                    .Property(property.Name)
                    .HasPrecision(18, 2);
            }
        }
    }
}
