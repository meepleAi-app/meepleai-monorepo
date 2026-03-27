using Api.BoundedContexts.Administration.Application.Queries.UserStats;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Dashboard;

/// <summary>
/// Integration tests for GET /api/v1/users/me/stats endpoint (Issue #4585, #4578)
/// Epic #4575: Gaming Hub Dashboard - Phase 3
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
public sealed class UserStatsEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _dbName = null!;
    private MeepleAiDbContext _dbContext = null!;

    public UserStatsEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_user_stats_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);

        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector());

        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();

        _dbContext = new MeepleAiDbContext(optionsBuilder.Options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync();

    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_dbName);
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetUserStats_WithGamesAndSessions_ReturnsCorrectStats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedTestDataAsync(userId, gamesCount: 10, sessionsThisMonth: 5, sessionsLastMonth: 4);

        var mockHttpContext = TestHttpContextFactory.CreateMockHttpContextAccessor(userId);
        var services = new ServiceCollection();
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();
        var handler = new GetUserStatsQueryHandler(_dbContext, cache, mockHttpContext.Object);

        // Act
        var result = await handler.Handle(new GetUserStatsQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalGames.Should().Be(10);
        result.MonthlyPlays.Should().Be(5);
        result.MonthlyPlaysChange.Should().Be(25); // (5-4)/4*100 = 25%
    }

    [Fact]
    public async Task GetUserStats_WithNoData_ReturnsZeroStats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var mockHttpContext = TestHttpContextFactory.CreateMockHttpContextAccessor(userId);
        var services = new ServiceCollection();
        services.AddHybridCache();
        var cache = services.BuildServiceProvider().GetRequiredService<HybridCache>();
        var handler = new GetUserStatsQueryHandler(_dbContext, cache, mockHttpContext.Object);

        // Act
        var result = await handler.Handle(new GetUserStatsQuery(), CancellationToken.None);

        // Assert
        result.TotalGames.Should().Be(0);
        result.MonthlyPlays.Should().Be(0);
        result.MonthlyPlaysChange.Should().Be(0);
    }

    private async Task SeedTestDataAsync(Guid userId, int gamesCount, int sessionsThisMonth, int sessionsLastMonth)
    {
        // Create user
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"test-{userId}@test.com",
            DisplayName = "Test User",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        });

        // Add games to library (SharedGameId required by CK_UserLibraryEntry_GameSource check constraint)
        for (int i = 0; i < gamesCount; i++)
        {
            var sharedGameId = Guid.NewGuid();
            _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
            {
                Id = sharedGameId,
                Title = $"Test Game {i}",
                YearPublished = 2024,
                MinPlayers = 2,
                MaxPlayers = 4,
                PlayingTimeMinutes = 60,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                Status = 1
            });
            _dbContext.Set<UserLibraryEntryEntity>().Add(new UserLibraryEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SharedGameId = sharedGameId,
                AddedAt = DateTime.UtcNow,
                TimesPlayed = 0
            });
        }

        // Add sessions this month
        for (int i = 0; i < sessionsThisMonth; i++)
        {
            _dbContext.PlayRecords.Add(new PlayRecordEntity
            {
                Id = Guid.NewGuid(),
                CreatedByUserId = userId,
                GameName = $"Game {i}",
                SessionDate = DateTime.UtcNow.AddDays(-i - 1),
                Status = 2,
                ScoringConfigJson = "{}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Add sessions last month
        for (int i = 0; i < sessionsLastMonth; i++)
        {
            _dbContext.PlayRecords.Add(new PlayRecordEntity
            {
                Id = Guid.NewGuid(),
                CreatedByUserId = userId,
                GameName = $"Game Old {i}",
                SessionDate = DateTime.UtcNow.AddDays(-35 - i),
                Status = 2,
                ScoringConfigJson = "{}",
                CreatedAt = DateTime.UtcNow.AddDays(-35),
                UpdatedAt = DateTime.UtcNow.AddDays(-35)
            });
        }

        await _dbContext.SaveChangesAsync();
    }
}
