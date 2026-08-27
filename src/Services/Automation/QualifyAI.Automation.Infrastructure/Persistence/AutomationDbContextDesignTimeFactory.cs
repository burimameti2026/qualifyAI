using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QualifyAI.Automation.Infrastructure.Persistence;

namespace QualifyAI.Automation.Infrastructure.Persistence;

public sealed class AutomationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AutomationDbContext>
{
    public AutomationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AutomationDbContext>()
            .UseSqlServer(DesignConnectionString)
            .Options;

        return new AutomationDbContext(options);
    }

    private const string DesignConnectionString =
        "Server=localhost;Database=QualifyAI_Automation_Design;User Id=sa;Password=DesignOnly123!;TrustServerCertificate=True;Encrypt=False";
}
