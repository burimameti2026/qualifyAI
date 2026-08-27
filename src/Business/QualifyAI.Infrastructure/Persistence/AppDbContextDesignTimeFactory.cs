using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QualifyAI.Infrastructure;

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
        "Server=localhost;Database=QualifyAI_Business_Design;User Id=sa;Password=DesignOnly123!;TrustServerCertificate=True;Encrypt=False";
}
