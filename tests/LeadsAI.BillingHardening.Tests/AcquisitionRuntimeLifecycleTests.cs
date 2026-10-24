using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Persistence.SqlServer;
using Xunit;

namespace LeadsAI.BillingHardening.Tests;

public sealed class AcquisitionRuntimeLifecycleTests
{
    private static (AppDbContext Db, CampaignContainerRuntime Runtime) CreateRuntime()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        return (db, new CampaignContainerRuntime(db));
    }

    private static CampaignContainer CreateContainer(CampaignContainerStatus status = CampaignContainerStatus.Pending) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CampaignId = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            Name = "runtime-test-container",
            PackageCode = "test",
            PackageVersion = "1.0",
            Status = status
        };

    [Fact]
    public void Queue_does_not_reset_a_running_container()
    {
        var (db, runtime) = CreateRuntime();
        using (db)
        {
            var container = CreateContainer(CampaignContainerStatus.Running);
            runtime.Queue(container);
            Assert.Equal(CampaignContainerStatus.Running, container.Status);
        }
    }

    [Fact]
    public void Start_and_run_move_container_through_starting_to_running()
    {
        var (db, runtime) = CreateRuntime();
        using (db)
        {
            var container = CreateContainer();
            var now = DateTime.UtcNow;
            runtime.Start(container, now);
            Assert.Equal(CampaignContainerStatus.Starting, container.Status);
            Assert.Equal(now, container.LastStartedAtUtc);

            runtime.Run(container, now.AddSeconds(1));
            Assert.Equal(CampaignContainerStatus.Running, container.Status);
            Assert.Equal(now, container.LastStartedAtUtc);
        }
    }

    [Fact]
    public void Waiting_is_not_marked_as_completed()
    {
        var (db, runtime) = CreateRuntime();
        using (db)
        {
            var container = CreateContainer(CampaignContainerStatus.Running);
            runtime.Wait(container, DateTime.UtcNow);
            Assert.Equal(CampaignContainerStatus.Waiting, container.Status);
            Assert.Null(container.CompletedAtUtc);
        }
    }

    [Theory]
    [InlineData(CampaignContainerStatus.Stopped)]
    [InlineData(CampaignContainerStatus.Completed)]
    public void Lease_recovery_preserves_terminal_container_states(CampaignContainerStatus status)
    {
        var (db, runtime) = CreateRuntime();
        using (db)
        {
            var container = CreateContainer(status);
            runtime.RequeueAfterLeaseExpiry(container, DateTime.UtcNow);
            Assert.Equal(status, container.Status);
        }
    }

    [Fact]
    public void Failed_container_can_be_requeued_for_retry()
    {
        var (db, runtime) = CreateRuntime();
        using (db)
        {
            var container = CreateContainer(CampaignContainerStatus.Failed);
            runtime.RequeueAfterLeaseExpiry(container, DateTime.UtcNow);
            Assert.Equal(CampaignContainerStatus.Queued, container.Status);
            Assert.Null(container.CompletedAtUtc);
        }
    }
}
