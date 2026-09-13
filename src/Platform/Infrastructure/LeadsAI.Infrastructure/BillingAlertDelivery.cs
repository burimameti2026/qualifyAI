using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure;

public sealed class PersistentBillingAlertSink(AppDbContext db) : IBillingAlertSink
{
 public async Task PublishAsync(BillingAlert alert,CancellationToken ct=default){db.Notifications.Add(new Notification{TenantId=alert.TenantId,Type=$"billing.{alert.Type}",Title=alert.Severity,Message=alert.Message,CreatedAtUtc=alert.OccurredAtUtc});await db.SaveChangesAsync(ct);}
}
