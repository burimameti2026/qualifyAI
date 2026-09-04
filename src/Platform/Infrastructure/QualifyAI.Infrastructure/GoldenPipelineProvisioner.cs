using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure;

public interface IGoldenPipelineProvisioner
{
    Task<Pipeline> EnsureProvisionedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class GoldenPipelineProvisioner(AppDbContext db) : IGoldenPipelineProvisioner
{
    public async Task<Pipeline> EnsureProvisionedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var pipeline = await db.Pipelines.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == GoldenPipeline.Name, cancellationToken);
        if (pipeline is null)
        {
            pipeline = new Pipeline { Id = Guid.NewGuid(), TenantId = tenantId, Name = GoldenPipeline.Name, IsDefault = true };
            db.Pipelines.Add(pipeline);
        }

        var existingNames = await db.PipelineStages
            .Where(x => x.TenantId == tenantId && x.PipelineId == pipeline.Id)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        foreach (var stage in GoldenPipeline.DefaultStages.Where(x => !existingNames.Contains(x.Name, StringComparer.OrdinalIgnoreCase)))
        {
            db.PipelineStages.Add(new PipelineStage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PipelineId = pipeline.Id,
                Name = stage.Name,
                SortOrder = stage.SortOrder,
                Probability = stage.Probability
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return pipeline;
    }
}
