using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QualifyAI.Knowledge.Infrastructure.Persistence;

public sealed class KnowledgeDbContextDesignTimeFactory : IDesignTimeDbContextFactory<KnowledgeDbContext>
{
    public KnowledgeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseSqlServer(DesignConnectionString)
            .Options;

        return new KnowledgeDbContext(options);
    }

    private const string DesignConnectionString =
        "Server=localhost;Database=QualifyAI_Knowledge_Design;User Id=sa;Password=DesignOnly123!;TrustServerCertificate=True;Encrypt=False";
}
