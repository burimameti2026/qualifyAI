using MongoDB.Driver;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Knowledge.Application.Abstractions.Persistence;
using QualifyAI.Knowledge.Domain.KnowledgeBases;
using QualifyAI.Knowledge.Infrastructure.Mongo;
using QualifyAI.Knowledge.Infrastructure.Persistence;
using QualifyAI.Knowledge.Infrastructure.Persistence.Repositories;

namespace QualifyAI.Knowledge.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddKnowledgeInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<KnowledgeDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("KnowledgeDb")));

        services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();
        services.AddScoped<IKnowledgeUnitOfWork, KnowledgeUnitOfWork>();

        var mongoConnection = configuration["Mongo:ConnectionString"] ?? "mongodb://mongodb:27017";
        var mongoDatabase = configuration["Mongo:Database"] ?? "QualifyAI_Knowledge";

        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnection));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabase));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoDatabase>()
            .GetCollection<KnowledgeChunkDocument>("knowledge_chunks"));
        services.AddScoped<IKnowledgeChunkStore, MongoKnowledgeChunkStore>();

        return services;
    }
}
