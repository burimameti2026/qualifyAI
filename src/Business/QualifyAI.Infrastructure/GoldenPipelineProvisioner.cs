using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure;

public interface IGoldenPipelineProvisioner
{
    Task<Pipeline> EnsureProvisionedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class GoldenPipelineProvisioner(AppDbContext db) : IGoldenPipelineProvisioner
{
    public async Task<Pipeline> EnsureProvisionedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existing = await db.Pipelines
            .Where(x => x.TenantId == tenantId && x.Name == GoldenPipeline.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            var existingStages = await db.PipelineStages.CountAsync(x => x.TenantId == tenantId && x.PipelineId == existing.Id, cancellationToken);
            if (existingStages == GoldenPipeline.DefaultStages.Length) return existing;
        }

        if (existing is null)
        {
            existing = new Pipeline
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = GoldenPipeline.Name,
                IsDefault = true
            };
            db.Pipelines.Add(existing);
        }

        var stageNames = await db.PipelineStages
            .Where(x => x.TenantId == tenantId && x.PipelineId == existing.Id)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        foreach (var definition in GoldenPipeline.DefaultStages.Where(x => !stageNames.Contains(x.Name, StringComparer.OrdinalIgnoreCase)))
        {
            db.PipelineStages.Add(new PipelineStage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PipelineId = existing.Id,
                Name = definition.Name,
                SortOrder = definition.SortOrder,
                Probability = definition.Probability
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }
}
