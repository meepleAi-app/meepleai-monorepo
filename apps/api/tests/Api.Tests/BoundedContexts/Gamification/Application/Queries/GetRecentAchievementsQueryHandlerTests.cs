using Api.BoundedContexts.Gamification.Application.DTOs;
using Api.BoundedContexts.Gamification.Application.Queries.GetRecentAchievements;
using Api.BoundedContexts.Gamification.Domain.Entities;
using Api.BoundedContexts.Gamification.Domain.Enums;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Gamification.Application.Queries;

/// <summary>
/// Unit tests for GetRecentAchievementsQueryHandler.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public sealed class GetRecentAchievementsQueryHandlerTests
{
    private readonly Mock<IAchievementRepository> _achievementRepoMock = new();
    private readonly Mock<IUserAchievementRepository> _userAchievementRepoMock = new();
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<GetRecentAchievementsQueryHandler>> _loggerMock = new();

    private GetRecentAchievementsQueryHandler CreateHandler() =>
        new GetRecentAchievementsQueryHandler(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);

    #region Constructor Null Guard Tests

    [Fact]
    public void Constructor_NullAchievementRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetRecentAchievementsQueryHandler(
            null!,
            _userAchievementRepoMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("achievementRepository");
    }

    [Fact]
    public void Constructor_NullUserAchievementRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetRecentAchievementsQueryHandler(
            _achievementRepoMock.Object,
            null!,
            _cacheMock.Object,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("userAchievementRepository");
    }

    [Fact]
    public void Constructor_NullCache_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetRecentAchievementsQueryHandler(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            null!,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetRecentAchievementsQueryHandler(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _cacheMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Handle Null Guard Tests

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var act = async () => await handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Handle Behavior Tests

    [Fact]
    public async Task Handle_NoUnlockedAchievements_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetRecentAchievementsQuery(userId, Limit: 5);

        _userAchievementRepoMock
            .Setup(r => r.GetRecentUnlockedAsync(userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<UserAchievement>)new List<UserAchievement>());

        // Cache miss: invoke factory
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<RecentAchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<List<RecentAchievementDto>>> factory,
                string[]? __, TimeSpan? ___, CancellationToken ct) => factory(ct));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        _achievementRepoMock.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithUnlockedAchievements_ReturnsCorrectlyMappedDtos()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetRecentAchievementsQuery(userId, Limit: 5);

        var achievement = Achievement.Create(
            code: "STREAK_7",
            name: "Weekly Streak",
            description: "Play 7 days in a row",
            iconUrl: "/icons/streak.png",
            points: 50,
            rarity: AchievementRarity.Common,
            category: AchievementCategory.Streak,
            threshold: 7);

        var userAchievement = UserAchievement.Create(userId, achievement.Id);
        userAchievement.UpdateProgress(100); // unlock it

        var recentUnlocked = (IReadOnlyList<UserAchievement>)new List<UserAchievement> { userAchievement };
        var activeAchievements = (IReadOnlyList<Achievement>)new List<Achievement> { achievement };

        _userAchievementRepoMock
            .Setup(r => r.GetRecentUnlockedAsync(userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentUnlocked);

        _achievementRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeAchievements);

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<RecentAchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<List<RecentAchievementDto>>> factory,
                string[]? __, TimeSpan? ___, CancellationToken ct) => factory(ct));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.AchievementId.Should().Be(achievement.Id);
        dto.Code.Should().Be("STREAK_7");
        dto.Name.Should().Be("Weekly Streak");
        dto.Description.Should().Be("Play 7 days in a row");
        dto.IconUrl.Should().Be("/icons/streak.png");
        dto.Points.Should().Be(50);
        dto.Rarity.Should().Be("Common");
        dto.UnlockedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task Handle_LimitApplied_ReturnsOnlyRequestedCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var limit = 2;
        var query = new GetRecentAchievementsQuery(userId, Limit: limit);

        // Build 3 achievements
        var achievement1 = Achievement.Create("ACH_1", "Achievement 1", "Desc 1",
            "", 10, AchievementRarity.Common, AchievementCategory.Milestone, 1);
        var achievement2 = Achievement.Create("ACH_2", "Achievement 2", "Desc 2",
            "", 20, AchievementRarity.Common, AchievementCategory.Milestone, 2);
        var achievement3 = Achievement.Create("ACH_3", "Achievement 3", "Desc 3",
            "", 30, AchievementRarity.Common, AchievementCategory.Milestone, 3);

        var ua1 = UserAchievement.Create(userId, achievement1.Id);
        ua1.UpdateProgress(100);
        var ua2 = UserAchievement.Create(userId, achievement2.Id);
        ua2.UpdateProgress(100);
        var ua3 = UserAchievement.Create(userId, achievement3.Id);
        ua3.UpdateProgress(100);

        // The repo returns the MaxCacheLimit (20), cache stores full set, handler trims to query.Limit
        var recentUnlocked = (IReadOnlyList<UserAchievement>)new List<UserAchievement> { ua1, ua2, ua3 };
        var activeAchievements = (IReadOnlyList<Achievement>)new List<Achievement>
            { achievement1, achievement2, achievement3 };

        _userAchievementRepoMock
            .Setup(r => r.GetRecentUnlockedAsync(userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentUnlocked);

        _achievementRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeAchievements);

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<RecentAchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<List<RecentAchievementDto>>> factory,
                string[]? __, TimeSpan? ___, CancellationToken ct) => factory(ct));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - limit of 2 should be enforced
        result.Should().HaveCount(limit);
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedResultAndDoesNotCallRepositories()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetRecentAchievementsQuery(userId, Limit: 5);
        var expectedCacheKey = $"achievements:recent:{userId}";

        var cachedResult = new List<RecentAchievementDto>
        {
            new RecentAchievementDto
            {
                AchievementId = Guid.NewGuid(),
                Code = "STREAK_7",
                Name = "Weekly Streak",
                Points = 50,
                Rarity = "Common",
                UnlockedAt = DateTime.UtcNow
            }
        };

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                expectedCacheKey,
                It.IsAny<Func<CancellationToken, Task<List<RecentAchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResult);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Code.Should().Be("STREAK_7");

        // Repositories should NOT be called when cache hits
        _userAchievementRepoMock.Verify(
            r => r.GetRecentUnlockedAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _achievementRepoMock.Verify(
            r => r.GetActiveAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion
}
