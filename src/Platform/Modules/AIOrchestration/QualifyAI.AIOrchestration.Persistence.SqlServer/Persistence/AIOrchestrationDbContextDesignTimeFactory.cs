using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QualifyAI.AIOrchestration.Persistence.SqlServer;

public sealed class AIOrchestrationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AIOrchestrationDbContext>
{
    public AIOrchestrationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AIOrchestrationDbContext>()
            .UseSqlServer(DesignConnectionString)
            .Options;

        return new AIOrchestrationDbContext(options);
    }

    private const string DesignConnectionString =
        "Server=localhost;Database=QualifyAI_AIOrchestration_Design;User Id=sa;Password=DesignOnly123!;TrustServerCertificate=True;Encrypt=False";
}
