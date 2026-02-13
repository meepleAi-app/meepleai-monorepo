using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Unit tests for AiInsightsService (Issue #3916).
/// Tests AI insights generation: recommendations, backlog alerts, rules reminders.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class AiInsightsServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepo;
    private readonly Mock<ILogger<AiInsightsService>> _mockLogger;
    private readonly AiInsightsService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public AiInsightsServiceTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockLibraryRepo = new Mock<IUserLibraryRepository>();
        _mockLogger = new Mock<ILogger<AiInsightsService>>();

        _service = new AiInsightsService(
            _mockLibraryRepo.Object,
            _dbContext,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(Skip = "WIP - AiInsightsService Issue #3916 - InMemory DB query issue")]
    public async Task GetInsightsAsync_WithRecentGames_ReturnsRecommendations()
    {
        // Arrange - seed recent games in library
        var game1 = SeedSharedGame("Wingspan", averageRating: 8.5m);
        var game2 = SeedSharedGame("Terraforming Mars", averageRating: 8.2m);
        var recentGame1 = CreateLibraryEntry(game1.Id);
        var recentGame2 = CreateLibraryEntry(game2.Id);

        _mockLibraryRepo
            .Setup(r => r.GetRecentlyPlayedAsync(_userId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserLibraryEntry> { recentGame1, recentGame2 }.AsReadOnly());

        // Seed similar game (high rating, not in library)
        var similarGame = SeedSharedGame("Spirit Island", averageRating: 8.9m);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetInsightsAsync(_userId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Insights);
        var recommendations = result.Insights.Where(i => i.Type == InsightType.Recommendation).ToList();
        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, r => r.Title == "Spirit Island");
    }

    [Fact]
    public async Task GetInsightsAsync_WithBacklogGames_ReturnsBacklogAlerts()
    {
        // Arrange - mock unplayed games
        var game1 = SeedSharedGame("Gloomhaven");
        var backlogEntry = CreateLibraryEntry(game1.Id);

        _mockLibraryRepo
            .Setup(r => r.GetRecentlyPlayedAsync(_userId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserLibraryEntry>());

        _mockLibraryRepo
            .Setup(r => r.GetUnplayedGamesAsync(_userId, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserLibraryEntry> { backlogEntry }.AsReadOnly());

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetInsightsAsync(_userId);

        // Assert
        Assert.NotNull(result);
        var backlogAlerts = result.Insights.Where(i => i.Type == InsightType.Backlog).ToList();
        Assert.NotEmpty(backlogAlerts);
        Assert.Contains(backlogAlerts, a => a.Title.Contains("Gloomhaven", StringComparison.Ordinal));
    }

    [Fact(Skip = "WIP - AiInsightsService Issue #3916 - InMemory DB query issue")]
    public async Task GetInsightsAsync_WithRulesChats_ReturnsRulesReminders()
    {
        // Arrange - seed chat with "regole" in title
        SeedChatThread("Regole Wingspan - Setup", DateTime.UtcNow.AddDays(-2));
        await _dbContext.SaveChangesAsync();

        _mockLibraryRepo
            .Setup(r => r.GetRecentlyPlayedAsync(_userId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserLibraryEntry>());

        _mockLibraryRepo
            .Setup(r => r.GetUnplayedGamesAsync(_userId, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserLibraryEntry>());

        // Act
        var result = await _service.GetInsightsAsync(_userId);

        // Assert
        Assert.NotNull(result);
        var rulesReminders = result.Insights.Where(i => i.Type == InsightType.RulesReminder).ToList();
        Assert.NotEmpty(rulesReminders);
        Assert.Contains(rulesReminders, r => r.Title.Contains("Wingspan", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetInsightsAsync_EmptyLibrary_ReturnsEmptyInsights()
    {
        // Arrange - no games, chats, or activity
        _mockLibraryRepo
            .Setup(r => r.GetRecentlyPlayedAsync(_userId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserLibraryEntry>());

        _mockLibraryRepo
            .Setup(r => r.GetUnplayedGamesAsync(_userId, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserLibraryEntry>());

        // Act
        var result = await _service.GetInsightsAsync(_userId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Insights);
        Assert.True(result.GeneratedAt <= DateTime.UtcNow);
        Assert.True(result.NextRefresh > result.GeneratedAt);
    }

    [Fact]
    public async Task GetInsightsAsync_ParallelExecution_CompletesUnder1Second()
    {
        // Arrange
        var game = SeedSharedGame("Catan");
        var entry = CreateLibraryEntry(game.Id);
        SeedChatThread("Regole Catan", DateTime.UtcNow.AddDays(-1));

        _mockLibraryRepo
            .Setup(r => r.GetRecentlyPlayedAsync(_userId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserLibraryEntry> { entry }.AsReadOnly());

        _mockLibraryRepo
            .Setup(r => r.GetUnplayedGamesAsync(_userId, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserLibraryEntry> { entry }.AsReadOnly());

        await _dbContext.SaveChangesAsync();

        // Act - measure execution time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _service.GetInsightsAsync(_userId);
        stopwatch.Stop();

        // Assert - performance requirement < 1s (p95)
        Assert.NotNull(result);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Expected <1000ms, got {stopwatch.ElapsedMilliseconds}ms");
    }

    // Helper methods for seeding test data
    private SharedGameEntity SeedSharedGame(string title, decimal? averageRating = null)
    {
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"{title} description",
            YearPublished = 2020,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 12,
            AverageRating = averageRating ?? 7.5m,
            ImageUrl = $"https://example.com/{title}.jpg",
            ThumbnailUrl = $"https://example.com/{title}-thumb.jpg",
            Status = 1, // Published
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _dbContext.SharedGames.Add(game);
        return game;
    }

    private UserLibraryEntry CreateLibraryEntry(Guid gameId)
    {
        return new UserLibraryEntry(Guid.NewGuid(), _userId, gameId);
    }

    private void SeedChatThread(string title, DateTime createdAt)
    {
        var chat = new ChatThreadEntity
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = title,
            Status = "active",
            CreatedAt = createdAt,
            LastMessageAt = createdAt,
            MessagesJson = "[]"
        };

        _dbContext.ChatThreads.Add(chat);
    }
}
