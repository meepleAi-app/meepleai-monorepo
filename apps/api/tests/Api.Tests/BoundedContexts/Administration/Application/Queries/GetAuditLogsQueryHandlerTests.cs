using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Tests for GetAuditLogsQueryHandler.
/// Issue #3691: Audit Log System.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetAuditLogsQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly GetAuditLogsQueryHandler _handler;

    public GetAuditLogsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuditLogTests_{Guid.NewGuid()}")
            .Options;

        _db = new MeepleAiDbContext(options, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
        _handler = new GetAuditLogsQueryHandler(_db);
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_Entries()
    {
        // Act
        var result = await _handler.Handle(new GetAuditLogsQuery(), CancellationToken.None);

        // Assert
        Assert.Empty(result.Entries);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Should_Return_Paginated_Results()
    {
        // Arrange
        SeedAuditLogs(10);

        // Act
        var result = await _handler.Handle(
            new GetAuditLogsQuery(Limit: 3, Offset: 0),
            CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(3, result.Limit);
        Assert.Equal(0, result.Offset);
    }

    [Fact]
    public async Task Should_Filter_By_Action()
    {
        // Arrange
        SeedAuditLogs(5, action: "UserImpersonate");
        SeedAuditLogs(3, action: "TierChange");

        // Act
        var result = await _handler.Handle(
            new GetAuditLogsQuery(Action: "UserImpersonate"),
            CancellationToken.None);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.All(result.Entries, e => Assert.Equal("UserImpersonate", e.Action));
    }

    [Fact]
    public async Task Should_Filter_By_Result()
    {
        // Arrange
        SeedAuditLogs(4, result: "Success");
        SeedAuditLogs(2, result: "Error");

        // Act
        var result = await _handler.Handle(
            new GetAuditLogsQuery(Result: "Error"),
            CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Entries, e => Assert.Equal("Error", e.Result));
    }

    [Fact]
    public async Task Should_Filter_By_AdminUserId()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SeedAuditLogs(3, userId: adminId);
        SeedAuditLogs(2, userId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(
            new GetAuditLogsQuery(AdminUserId: adminId),
            CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Entries, e => Assert.Equal(adminId, e.AdminUserId));
    }

    [Fact]
    public async Task Should_Filter_By_DateRange()
    {
        // Arrange
        var now = DateTime.UtcNow;
        SeedAuditLogs(2, createdAt: now.AddDays(-10));
        SeedAuditLogs(3, createdAt: now.AddDays(-1));

        // Act
        var result = await _handler.Handle(
            new GetAuditLogsQuery(StartDate: now.AddDays(-5)),
            CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Should_Order_By_CreatedAt_Descending()
    {
        // Arrange
        var now = DateTime.UtcNow;
        SeedAuditLogs(1, createdAt: now.AddHours(-2));
        SeedAuditLogs(1, createdAt: now);
        SeedAuditLogs(1, createdAt: now.AddHours(-1));

        // Act
        var result = await _handler.Handle(new GetAuditLogsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.True(result.Entries[0].CreatedAt >= result.Entries[1].CreatedAt);
        Assert.True(result.Entries[1].CreatedAt >= result.Entries[2].CreatedAt);
    }

    [Fact]
    public async Task Should_Apply_Multiple_Filters()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SeedAuditLogs(2, userId: adminId, action: "UserImpersonate", result: "Success");
        SeedAuditLogs(1, userId: adminId, action: "UserImpersonate", result: "Error");
        SeedAuditLogs(3, userId: Guid.NewGuid(), action: "TierChange", result: "Success");

        // Act
        var result = await _handler.Handle(
            new GetAuditLogsQuery(AdminUserId: adminId, Action: "UserImpersonate", Result: "Success"),
            CancellationToken.None);

        // Assert
        Assert.Equal(2, result.TotalCount);
    }

    private void SeedAuditLogs(
        int count,
        string action = "TestAction",
        string result = "Success",
        Guid? userId = null,
        DateTime? createdAt = null)
    {
        for (var i = 0; i < count; i++)
        {
            _db.AuditLogs.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId ?? Guid.NewGuid(),
                Action = action,
                Resource = "TestResource",
                Result = result,
                CreatedAt = createdAt ?? DateTime.UtcNow,
            });
        }
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
