using Microsoft.EntityFrameworkCore;
using QualifyAI.Infrastructure;
using QualifyAI.Persistence.SqlServer;
using Xunit;

namespace QualifyAI.BillingHardening.Tests;
public sealed class BillingLifecycleTests
{
 [Fact] public void Failed_payment_enters_grace_and_schedules_retry(){var engine=new BillingLifecycleEngine(new BillingLifecyclePolicy(GraceDays:7,MaxRetryAttempts:4));var now=DateTime.UtcNow;var result=engine.Transition(new(Guid.NewGuid(),EnterpriseBillingState.Active,null,null,0,null),"failed",now);Assert.Equal(EnterpriseBillingState.GracePeriod,result.State);Assert.Equal(1,result.RetryAttempt);Assert.NotNull(result.GraceEndsAtUtc);Assert.NotNull(result.NextRetryAtUtc);}
 [Fact] public void Successful_payment_restores_active(){var engine=new BillingLifecycleEngine(new BillingLifecyclePolicy());var result=engine.Transition(new(Guid.NewGuid(),EnterpriseBillingState.GracePeriod,null,DateTime.UtcNow.AddDays(2),2,DateTime.UtcNow.AddHours(4)),"paid",DateTime.UtcNow);Assert.Equal(EnterpriseBillingState.Active,result.State);Assert.Equal(0,result.RetryAttempt);Assert.Null(result.NextRetryAtUtc);}
 [Fact] public void Quota_blocks_after_limit(){var options=new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;using var db=new AppDbContext(options);var meter=new PersistentUsageMeter(db);var quota=new BillingQuotaEnforcer(meter);var tenant=Guid.NewGuid();Assert.True(quota.Consume(tenant,"api_requests",2).Allowed);Assert.True(quota.Consume(tenant,"api_requests",2).Allowed);Assert.False(quota.Consume(tenant,"api_requests",2).Allowed);}
}
