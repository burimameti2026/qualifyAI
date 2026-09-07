using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure;

public sealed class PersistentUsageMeter(AppDbContext db) : IUsageMeter
{
    public long Add(Guid tenantId, string metric, long amount = 1)
    {
        for(var attempt = 0; attempt<3; attempt++)
        {
            var row = db.UsageRecords
                .FirstOrDefault(x => x.TenantId==tenantId&&x.Metric==metric);

            if(row is null)
            {
                row=new UsageRecord
                {
                    TenantId=tenantId,
                    Metric=metric,
                    Value=amount,
                    RecordedAtUtc=DateTime.UtcNow
                };
                db.UsageRecords.Add(row);

                try
                {
                    db.SaveChanges();
                    return row.Value;
                }
                catch(DbUpdateException) when(attempt<2)
                {
                    // Another request inserted the same (TenantId, Metric) first.
                    db.Entry(row).State=EntityState.Detached;
                    continue; // retry: it'll be found by FirstOrDefault next loop
                }
            }

            row.Value+=amount;
            row.RecordedAtUtc=DateTime.UtcNow;

            try
            {
                db.SaveChanges();
                return row.Value;
            }
            catch(DbUpdateConcurrencyException) when(attempt<2)
            {
                db.Entry(row).State=EntityState.Detached;
                continue; // retry: someone else updated it concurrently
            }
        }

        throw new InvalidOperationException(
            $"Failed to update usage for tenant {tenantId}, metric {metric} after retries.");
    }

    public long Get(Guid tenantId, string metric) =>
        db.UsageRecords
            .Where(x => x.TenantId==tenantId&&x.Metric==metric)
            .Select(x => x.Value)
            .FirstOrDefault();

    public bool IsExceeded(Guid tenantId, string metric, long limit) =>
        limit>=0&&Get(tenantId, metric)>=limit;
}
