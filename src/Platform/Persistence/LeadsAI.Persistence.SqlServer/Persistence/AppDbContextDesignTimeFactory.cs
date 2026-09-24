using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LeadsAI.Persistence.SqlServer;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(DesignConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    private const string DesignConnectionString =
        "Server=localhost;Database=LeadsAI_Business;User Id=t24test;Password=t24test;TrustServerCertificate=True;Encrypt=False";
}
