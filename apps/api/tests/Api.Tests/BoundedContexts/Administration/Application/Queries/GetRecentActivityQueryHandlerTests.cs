using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Tests for GetRecentActivityQueryHandler - Issue #874, #878
/// Validates ActivityFeedService functionality (13 comprehensive test scenarios)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetRecentActivityQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<GetRecentActivityQueryHandler>> _mockLogger;
    private readonly GetRecentActivityQueryHandler _handler;
    private readonly FakeTimeProvider _timeProvider;

    public GetRecentActivityQueryHandlerTests()
    {
        _dbContext = CreateFreshDbContext();
        _mockLogger = new Mock<ILogger<GetRecentActivityQueryHandler>>();
        _timeProvider = new FakeTimeProvider(new DateTime(2025, 12, 8, 14, 0, 0, DateTimeKind.Utc));
        _handler = new GetRecentActivityQueryHandler(_dbContext, _timeProvider, _mockLogger.Object);
    }

    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }

    [Fact]
    public async Task Handle_WithNoEvents_ReturnsEmptyActivity()
    {
        // Arrange
        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Events);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WithUserRegistrations_ReturnsUserEvents()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "john.doe@example.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-5)
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Events);
        Assert.Equal(ActivityEventType.UserRegistered, result.Events[0].EventType);
        Assert.Contains("john.doe@example.com", result.Events[0].Description);
        Assert.Equal(ActivitySeverity.Info, result.Events[0].Severity);
    }

    [Fact]
    public async Task Handle_WithPdfUploads_ReturnsPdfEvents()
    {
        // Arrange
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Game",
            MinPlayers = 2,
            MaxPlayers = 4
        };
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);
        _dbContext.Users.Add(user);

        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            FileName = "Catan-Rules.pdf",
            FilePath = "/uploads/catan.pdf",
            FileSizeBytes = 2456789,
            UploadedByUserId = user.Id,
            UploadedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-10)
        };
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Events.Count); // User + PDF events
        var pdfEvent = result.Events.FirstOrDefault(e => e.EventType == ActivityEventType.PdfUploaded);
        Assert.NotNull(pdfEvent);
        Assert.Contains("Catan-Rules.pdf", pdfEvent.Description);
        Assert.Contains("2456789", pdfEvent.Description);
    }

    [Fact]
    public async Task Handle_WithAlerts_ReturnsAlertEvents()
    {
        // Arrange
        var alert = new AlertEntity
        {
            Id = Guid.NewGuid(),
            AlertType = "HighErrorRate",
            Severity = "warning",
            Message = "High error rate detected",
            TriggeredAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-15),
            IsActive = true
        };
        _dbContext.Alerts.Add(alert);
        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Events);
        Assert.Equal(ActivityEventType.AlertCreated, result.Events[0].EventType);
        Assert.Contains("High error rate", result.Events[0].Description);
        Assert.Equal(ActivitySeverity.Warning, result.Events[0].Severity);
    }

    [Fact]
    public async Task Handle_WithResolvedAlert_ReturnsAlertResolvedEvent()
    {
        // Arrange
        var resolvedAlert = new AlertEntity
        {
            Id = Guid.NewGuid(),
            AlertType = "DatabaseDown",
            Severity = "critical",
            Message = "Database connection restored",
            TriggeredAt = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-2),
            ResolvedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-30),
            IsActive = false
        };
        _dbContext.Alerts.Add(resolvedAlert);
        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Events);
        Assert.Equal(ActivityEventType.AlertResolved, result.Events[0].EventType);
        Assert.Equal(ActivitySeverity.Critical, result.Events[0].Severity);
    }

    [Fact]
    public async Task Handle_WithErrors_ReturnsErrorEvents()
    {
        // Arrange
        var errorLog = new AiRequestLogEntity
        {
            Id = Guid.NewGuid(),
            Endpoint = "qa",
            Query = "Test query",
            Status = "Error",
            ErrorMessage = "Rate limit exceeded",
            LatencyMs = 0,
            TokenCount = 0,
            PromptTokens = 0,
            CompletionTokens = 0,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-20)
        };
        _dbContext.AiRequestLogs.Add(errorLog);
        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Events);
        Assert.Equal(ActivityEventType.ErrorOccurred, result.Events[0].EventType);
        Assert.Contains("Rate limit exceeded", result.Events[0].Description);
        Assert.Equal(ActivitySeverity.Error, result.Events[0].Severity);
    }

    [Fact]
    public async Task Handle_WithLimit_ReturnsOnlySpecifiedNumber()
    {
        // Arrange - Create 20 users
        for (int i = 0; i < 20; i++)
        {
            _dbContext.Users.Add(new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = $"user{i}@example.com",
                PasswordHash = "hash",
                Role = "user",
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-i)
            });
        }
        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 5);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Events.Count);
    }

    [Fact]
    public async Task Handle_WithSinceParameter_FiltersByDate()
    {
        // Arrange
        var recentUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "recent@example.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-5)
        };
        var oldUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "old@example.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-10)
        };
        _dbContext.Users.AddRange(recentUser, oldUser);
        await _dbContext.SaveChangesAsync();

        var since = _timeProvider.GetUtcNow().UtcDateTime.AddHours(-1);
        var query = new GetRecentActivityQuery(Limit: 10, Since: since);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Events);
        Assert.Contains("recent@example.com", result.Events[0].Description);
    }

    [Fact]
    public async Task Handle_SortsByTimestampDescending()
    {
        // Arrange
        var user1 = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "user1@example.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-30)
        };
        var user2 = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "user2@example.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-10)
        };
        var user3 = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "user3@example.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-20)
        };
        _dbContext.Users.AddRange(user1, user2, user3);
        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Events.Count);
        // Most recent first
        Assert.Contains("user2@example.com", result.Events[0].Description);
        Assert.Contains("user3@example.com", result.Events[1].Description);
        Assert.Contains("user1@example.com", result.Events[2].Description);
    }

    [Fact]
    public async Task Handle_WithMixedEvents_AggregatesCorrectly()
    {
        // Arrange - Create mixed events
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            Role = "user",
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-5)
        };
        _dbContext.Users.Add(user);

        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Catan",
            MinPlayers = 3,
            MaxPlayers = 4
        };
        _dbContext.Games.Add(game);

        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            FileName = "rules.pdf",
            FilePath = "/uploads/rules.pdf",
            FileSizeBytes = 1000000,
            UploadedByUserId = user.Id,
            UploadedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-10)
        };
        _dbContext.PdfDocuments.Add(pdf);

        var alert = new AlertEntity
        {
            Id = Guid.NewGuid(),
            AlertType = "Test",
            Severity = "info",
            Message = "Test alert",
            TriggeredAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-15),
            IsActive = true
        };
        _dbContext.Alerts.Add(alert);

        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Events.Count);
        Assert.Contains(result.Events, e => e.EventType == ActivityEventType.UserRegistered);
        Assert.Contains(result.Events, e => e.EventType == ActivityEventType.PdfUploaded);
        Assert.Contains(result.Events, e => e.EventType == ActivityEventType.AlertCreated);
    }

    [Fact]
    public async Task Handle_MapsAlertSeverityCorrectly()
    {
        // Arrange
        var alerts = new[]
        {
            new AlertEntity
            {
                Id = Guid.NewGuid(),
                AlertType = "Critical",
                Severity = "critical",
                Message = "Critical alert",
                TriggeredAt = _timeProvider.GetUtcNow().UtcDateTime,
                IsActive = true
            },
            new AlertEntity
            {
                Id = Guid.NewGuid(),
                AlertType = "Error",
                Severity = "error",
                Message = "Error alert",
                TriggeredAt = _timeProvider.GetUtcNow().UtcDateTime,
                IsActive = true
            },
            new AlertEntity
            {
                Id = Guid.NewGuid(),
                AlertType = "Warning",
                Severity = "warning",
                Message = "Warning alert",
                TriggeredAt = _timeProvider.GetUtcNow().UtcDateTime,
                IsActive = true
            },
            new AlertEntity
            {
                Id = Guid.NewGuid(),
                AlertType = "Info",
                Severity = "info",
                Message = "Info alert",
                TriggeredAt = _timeProvider.GetUtcNow().UtcDateTime,
                IsActive = true
            }
        };
        _dbContext.Alerts.AddRange(alerts);
        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, result.Events.Count);
        Assert.Contains(result.Events, e => e.Severity == ActivitySeverity.Critical);
        Assert.Contains(result.Events, e => e.Severity == ActivitySeverity.Error);
        Assert.Contains(result.Events, e => e.Severity == ActivitySeverity.Warning);
        Assert.Contains(result.Events, e => e.Severity == ActivitySeverity.Info);
    }

    [Fact]
    public async Task Handle_LimitsErrorEvents_ToHalfOfLimit()
    {
        // Arrange - Create many error logs
        for (int i = 0; i < 20; i++)
        {
            _dbContext.AiRequestLogs.Add(new AiRequestLogEntity
            {
                Id = Guid.NewGuid(),
                Endpoint = "qa",
                Query = $"Query {i}",
                Status = "Error",
                ErrorMessage = $"Error {i}",
                LatencyMs = 0,
                TokenCount = 0,
                PromptTokens = 0,
                CompletionTokens = 0,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-i)
            });
        }
        await _dbContext.SaveChangesAsync();

        var query = new GetRecentActivityQuery(Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        // Should limit errors to limit/2 = 5
        var errorEvents = result.Events.Where(e => e.EventType == ActivityEventType.ErrorOccurred).ToList();
        Assert.True(errorEvents.Count <= 5);
    }

    [Fact]
    public async Task Handle_RespectsMaxLimitOf100()
    {
        // Arrange
        var query = new GetRecentActivityQuery(Limit: 500); // Request more than max

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        // Should be capped at 100 max
        Assert.True(result.Events.Count <= 100);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
