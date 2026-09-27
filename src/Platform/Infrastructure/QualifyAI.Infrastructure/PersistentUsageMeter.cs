using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure;

public sealed class PersistentUsageMeter(AppDbContext db) : IUsageMeter
{
 public long Add(Guid tenantId,string metric,long amount=1){ var row=db.UsageRecords.SingleOrDefault(x=>x.TenantId==tenantId&&x.Metric==metric); if(row is null){row=new UsageRecord{TenantId=tenantId,Metric=metric,Value=amount,RecordedAtUtc=DateTime.UtcNow};db.UsageRecords.Add(row);}else{row.Value+=amount;row.RecordedAtUtc=DateTime.UtcNow;} db.SaveChanges(); return row.Value; }
 public long Get(Guid tenantId,string metric)=>db.UsageRecords.Where(x=>x.TenantId==tenantId&&x.Metric==metric).Select(x=>x.Value).FirstOrDefault();
 public bool IsExceeded(Guid tenantId,string metric,long limit)=>limit>=0&&Get(tenantId,metric)>=limit;
}
