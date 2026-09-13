using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using LeadsAI.Identity.Persistence.SqlServer;

namespace LeadsAI.Identity.Persistence.SqlServer;

public sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseSqlServer(DesignConnectionString);
        optionsBuilder.UseOpenIddict();
        return new IdentityDbContext(optionsBuilder.Options);
    }

    private const string DesignConnectionString =
        "Server=localhost;Database=LeadsAI_Identity_Design;User Id=t24test;Password=DesignOnly123!;TrustServerCertificate=True;Encrypt=False";
}
