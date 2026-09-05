using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure;

public sealed class PersistentBillingAlertSink(AppDbContext db) : IBillingAlertSink
{
 public async Task PublishAsync(BillingAlert alert,CancellationToken ct=default){db.Notifications.Add(new Notification{TenantId=alert.TenantId,Type=$"billing.{alert.Type}",Title=alert.Severity,Message=alert.Message,CreatedAtUtc=alert.OccurredAtUtc});await db.SaveChangesAsync(ct);}
}
