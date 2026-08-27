using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QualifyAI.Identity.Infrastructure.Persistence;

namespace QualifyAI.Identity.Infrastructure.Persistence;

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
        "Server=localhost;Database=QualifyAI_Identity_Design;User Id=sa;Password=DesignOnly123!;TrustServerCertificate=True;Encrypt=False";
}
