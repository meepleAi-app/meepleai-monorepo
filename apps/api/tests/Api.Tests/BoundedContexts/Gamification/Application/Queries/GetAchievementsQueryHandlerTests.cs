using Api.BoundedContexts.Gamification.Application.DTOs;
using Api.BoundedContexts.Gamification.Application.Queries.GetAchievements;
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
/// Unit tests for GetAchievementsQueryHandler.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public sealed class GetAchievementsQueryHandlerTests
{
    private readonly Mock<IAchievementRepository> _achievementRepoMock = new();
    private readonly Mock<IUserAchievementRepository> _userAchievementRepoMock = new();
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<GetAchievementsQueryHandler>> _loggerMock = new();

    private GetAchievementsQueryHandler CreateHandler() =>
        new GetAchievementsQueryHandler(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);

    #region Constructor Null Guard Tests

    [Fact]
    public void Constructor_NullAchievementRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetAchievementsQueryHandler(
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
        var act = () => new GetAchievementsQueryHandler(
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
        var act = () => new GetAchievementsQueryHandler(
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
        var act = () => new GetAchievementsQueryHandler(
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

    #region Cache Hit Tests

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedResultWithoutCallingRepositories()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetAchievementsQuery(userId);
        var expectedCacheKey = $"achievements:user:{userId}";

        var cachedResult = new List<AchievementDto>
        {
            new AchievementDto
            {
                Id = Guid.NewGuid(),
                Code = "STREAK_7",
                Name = "Weekly Streak",
                Points = 50,
                IsUnlocked = true
            }
        };

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                expectedCacheKey,
                It.IsAny<Func<CancellationToken, Task<List<AchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResult);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(cachedResult);
        result.Should().HaveCount(1);
        result[0].Code.Should().Be("STREAK_7");

        // Repositories should NOT be called when cache hits
        _achievementRepoMock.Verify(
            r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
        _userAchievementRepoMock.Verify(
            r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Cache Miss Tests

    [Fact]
    public async Task Handle_CacheMiss_CallsRepositoriesAndMapsResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetAchievementsQuery(userId);

        var achievement = Achievement.Create(
            code: "STREAK_7",
            name: "Weekly Streak",
            description: "Play 7 days in a row",
            iconUrl: "/icons/streak.png",
            points: 50,
            rarity: AchievementRarity.Common,
            category: AchievementCategory.Streak,
            threshold: 7);

        var achievements = (IReadOnlyList<Achievement>)new List<Achievement> { achievement };
        var userAchievements = (IReadOnlyList<UserAchievement>)new List<UserAchievement>();

        _achievementRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(achievements);

        _userAchievementRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAchievements);

        // Cache miss: invoke the factory delegate
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<AchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<List<AchievementDto>>> factory,
                string[]? __, TimeSpan? ___, CancellationToken ct) => factory(ct));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Id.Should().Be(achievement.Id);
        dto.Code.Should().Be("STREAK_7");
        dto.Name.Should().Be("Weekly Streak");
        dto.Points.Should().Be(50);
        dto.Rarity.Should().Be("Common");
        dto.Category.Should().Be("Streak");
        dto.Threshold.Should().Be(7);
        dto.IsUnlocked.Should().BeFalse();
        dto.Progress.Should().BeNull();
        dto.UnlockedAt.Should().BeNull();

        _achievementRepoMock.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
        _userAchievementRepoMock.Verify(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CacheMiss_WithUnlockedUserAchievement_SetsIsUnlockedTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetAchievementsQuery(userId);

        var achievement = Achievement.Create(
            code: "COLLECTOR_100",
            name: "Grand Collector",
            description: "Own 100 games",
            iconUrl: "/icons/collector.png",
            points: 200,
            rarity: AchievementRarity.Rare,
            category: AchievementCategory.Milestone,
            threshold: 100);

        var userAchievement = UserAchievement.Create(userId, achievement.Id);
        userAchievement.UpdateProgress(100); // unlocks it

        var achievements = (IReadOnlyList<Achievement>)new List<Achievement> { achievement };
        var userAchievements = (IReadOnlyList<UserAchievement>)new List<UserAchievement> { userAchievement };

        _achievementRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(achievements);

        _userAchievementRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAchievements);

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<AchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<List<AchievementDto>>> factory,
                string[]? __, TimeSpan? ___, CancellationToken ct) => factory(ct));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.IsUnlocked.Should().BeTrue();
        dto.Progress.Should().Be(100);
        dto.UnlockedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_CacheMiss_WithPartialProgress_SetsCorrectProgress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetAchievementsQuery(userId);

        var achievement = Achievement.Create(
            code: "STREAK_30",
            name: "Monthly Master",
            description: "Play 30 days in a row",
            iconUrl: "/icons/monthly.png",
            points: 100,
            rarity: AchievementRarity.Uncommon,
            category: AchievementCategory.Streak,
            threshold: 30);

        var userAchievement = UserAchievement.Create(userId, achievement.Id);
        userAchievement.UpdateProgress(50); // half-way

        var achievements = (IReadOnlyList<Achievement>)new List<Achievement> { achievement };
        var userAchievements = (IReadOnlyList<UserAchievement>)new List<UserAchievement> { userAchievement };

        _achievementRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(achievements);

        _userAchievementRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userAchievements);

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<AchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<List<AchievementDto>>> factory,
                string[]? __, TimeSpan? ___, CancellationToken ct) => factory(ct));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.IsUnlocked.Should().BeFalse();
        dto.Progress.Should().Be(50);
        dto.UnlockedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CacheMiss_NoActiveAchievements_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetAchievementsQuery(userId);

        _achievementRepoMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Achievement>)new List<Achievement>());

        _userAchievementRepoMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<UserAchievement>)new List<UserAchievement>());

        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<AchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Func<CancellationToken, Task<List<AchievementDto>>> factory,
                string[]? __, TimeSpan? ___, CancellationToken ct) => factory(ct));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion
}
