using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application.Queries;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DatabaseSync")]
public sealed class GetSyncOperationsHistoryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly GetSyncOperationsHistoryHandler _handler;

    public GetSyncOperationsHistoryHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetSyncOperationsHistoryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        var act = () => new GetSyncOperationsHistoryHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("dbContext");
    }

    [Fact]
    public async Task Handle_WithNoHistory_ReturnsEmptyList()
    {
        var result = await _handler.Handle(new GetSyncOperationsHistoryQuery(10), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithDatabaseSyncAuditLogs_ReturnsSyncEntries()
    {
        // Arrange — seed audit logs with DatabaseSync actions
        _dbContext.AuditLogs.AddRange(
            new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                Action = "DatabaseSync.SyncTable",
                Resource = "games",
                Result = "Success",
                CreatedAt = DateTime.UtcNow.AddMinutes(-2),
                UserId = Guid.NewGuid()
            },
            new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                Action = "DatabaseSync.ApplyMigrations",
                Resource = "schema",
                Result = "Success",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                UserId = Guid.NewGuid()
            },
            new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                Action = "Authentication.Login",
                Resource = "user",
                Result = "Success",
                CreatedAt = DateTime.UtcNow,
                UserId = Guid.NewGuid()
            } // NOT a DB sync entry — should be filtered out
        );
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetSyncOperationsHistoryQuery(50), CancellationToken.None);

        // Assert — only DatabaseSync.* entries returned
        result.Should().HaveCount(2);
        result.All(e => e.OperationType.StartsWith("DatabaseSync.", StringComparison.Ordinal)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_RespectsLimitParameter()
    {
        // Arrange — seed 5 entries
        for (int i = 0; i < 5; i++)
        {
            _dbContext.AuditLogs.Add(new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                Action = $"DatabaseSync.Op{i}",
                Resource = "table",
                Result = "Success",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                UserId = Guid.NewGuid()
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act — request only 3
        var result = await _handler.Handle(new GetSyncOperationsHistoryQuery(3), CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithFailedEntry_ReturnsFalseResult()
    {
        // Arrange
        _dbContext.AuditLogs.Add(new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            Action = "DatabaseSync.ApplyMigrations",
            Resource = "schema",
            Result = "Error",
            Details = "Connection timeout",
            CreatedAt = DateTime.UtcNow,
            UserId = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetSyncOperationsHistoryQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Result!.Success.Should().BeFalse();
        result[0].Result!.ErrorMessage.Should().Be("Connection timeout");
    }

    [Fact]
    public async Task Handle_ReturnsEntriesOrderedByDescendingCreatedAt()
    {
        // Arrange
        var old = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            Action = "DatabaseSync.Op1",
            Resource = "t",
            Result = "Success",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        var recent = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            Action = "DatabaseSync.Op2",
            Resource = "t",
            Result = "Success",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        _dbContext.AuditLogs.AddRange(old, recent);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetSyncOperationsHistoryQuery(10), CancellationToken.None);

        // Assert — most recent first
        result.Should().HaveCount(2);
        result[0].StartedAt.Should().BeAfter(result[1].StartedAt);
    }
}
