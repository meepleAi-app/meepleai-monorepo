using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.Scheduling;

/// <summary>
/// Tests for AuditLogRetentionJob.
/// Issue #3691: Retention policy - 90 days.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class AuditLogRetentionJobTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly AuditLogRetentionJob _job;

    public AuditLogRetentionJobTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"RetentionTests_{Guid.NewGuid()}")
            .Options;

        _db = new MeepleAiDbContext(options, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
        _job = new AuditLogRetentionJob(_db, NullLogger<AuditLogRetentionJob>.Instance);
    }

    [Fact]
    public async Task Should_Delete_Entries_Older_Than_90_Days()
    {
        // Arrange
        SeedAuditLogs(3, DateTime.UtcNow.AddDays(-100)); // Old - should be deleted
        SeedAuditLogs(2, DateTime.UtcNow.AddDays(-10));  // Recent - should remain

        var context = CreateJobContext();

        // Act
        await _job.Execute(context);

        // Assert
        var remaining = await _db.AuditLogs.CountAsync();
        Assert.Equal(2, remaining);
    }

    [Fact]
    public async Task Should_Keep_Entries_Within_Retention_Period()
    {
        // Arrange
        SeedAuditLogs(5, DateTime.UtcNow.AddDays(-30)); // Within 90 days

        var context = CreateJobContext();

        // Act
        await _job.Execute(context);

        // Assert
        var remaining = await _db.AuditLogs.CountAsync();
        Assert.Equal(5, remaining);
    }

    [Fact]
    public async Task Should_Handle_Empty_Table()
    {
        // Arrange - no data
        var context = CreateJobContext();

        // Act - should not throw
        await _job.Execute(context);

        // Assert
        var count = await _db.AuditLogs.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public void DefaultRetentionDays_Should_Be_90()
    {
        Assert.Equal(90, AuditLogRetentionJob.DefaultRetentionDays);
    }

    private void SeedAuditLogs(int count, DateTime createdAt)
    {
        for (var i = 0; i < count; i++)
        {
            _db.AuditLogs.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                Action = "TestAction",
                Resource = "TestResource",
                Result = "Success",
                CreatedAt = createdAt,
            });
        }
        _db.SaveChanges();
    }

    private static IJobExecutionContext CreateJobContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.Setup(x => x.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        mock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        mock.SetupProperty(x => x.Result);
        return mock.Object;
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
