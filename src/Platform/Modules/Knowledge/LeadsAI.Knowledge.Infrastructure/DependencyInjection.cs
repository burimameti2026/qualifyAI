using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using LeadsAI.Knowledge.Application.Abstractions.Persistence;
using LeadsAI.Knowledge.Domain.KnowledgeBases;
using LeadsAI.Knowledge.Infrastructure.Mongo;
using LeadsAI.Knowledge.Persistence.SqlServer;
using LeadsAI.Knowledge.Persistence.SqlServer.Repositories;

namespace LeadsAI.Knowledge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddKnowledgeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<KnowledgeDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("KnowledgeDb"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();
        services.AddScoped<IKnowledgeUnitOfWork, KnowledgeUnitOfWork>();

        var mongoConnection = configuration["Mongo:ConnectionString"] ?? "mongodb://mongodb:27017";
        var mongoDatabase = configuration["Mongo:Database"] ?? "LeadsAI_Knowledge";

        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnection));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabase));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>()
            .GetCollection<KnowledgeChunkDocument>("knowledge_chunks"));
        services.AddScoped<IKnowledgeChunkStore, MongoKnowledgeChunkStore>();

        return services;
    }
}
