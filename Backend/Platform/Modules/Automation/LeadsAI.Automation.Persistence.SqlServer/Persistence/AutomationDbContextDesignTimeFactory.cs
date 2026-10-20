using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using LeadsAI.Automation.Persistence.SqlServer;

namespace LeadsAI.Automation.Persistence.SqlServer;

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
        "Server=localhost;Database=LeadsAI_Automation_Design;User Id=t24test;Password=DesignOnly123!;TrustServerCertificate=True;Encrypt=False";
}
