using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QualifyAI.Integrations.Persistence.SqlServer;

public sealed class IntegrationsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IntegrationsDbContext>
{
    public IntegrationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IntegrationsDbContext>()
            .UseSqlServer(DesignConnectionString)
            .Options;

        return new IntegrationsDbContext(options);
    }

    private const string DesignConnectionString =
        "Server=localhost;Database=QualifyAI_Integrations_Design;User Id=sa;Password=DesignOnly123!;TrustServerCertificate=True;Encrypt=False";
}
