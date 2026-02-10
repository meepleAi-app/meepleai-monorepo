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
    private readonly Mock<IAchievementRepository> _achievementRepoMock;
    private readonly Mock<IUserAchievementRepository> _userAchievementRepoMock;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<ILogger<GetAchievementsQueryHandler>> _loggerMock;
    private readonly GetAchievementsQueryHandler _handler;

    public GetAchievementsQueryHandlerTests()
    {
        _achievementRepoMock = new Mock<IAchievementRepository>();
        _userAchievementRepoMock = new Mock<IUserAchievementRepository>();
        _cacheMock = new Mock<IHybridCacheService>();
        _loggerMock = new Mock<ILogger<GetAchievementsQueryHandler>>();

        // Default: cache executes factory directly so handler logic is tested
        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<AchievementDto>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<List<AchievementDto>>>, string[], TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));

        _handler = new GetAchievementsQueryHandler(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Null Checks

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

    #region Handle - Mapping and Progress

    [Fact]
    public async Task Handle_WithAchievementsAndProgress_ReturnsMappedDtos()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var achievement1 = Achievement.Create(
            "STREAK_7_DAYS", "Giocatore Costante", "Play 7 days in a row",
            "https://icons.example.com/streak7.png", 50,
            AchievementRarity.Uncommon, AchievementCategory.Streak, 7);
        var achievement2 = Achievement.Create(
            "COLLECTOR_100", "Collezionista", "Own 100 games",
            "https://icons.example.com/collector.png", 200,
            AchievementRarity.Epic, AchievementCategory.Milestone, 100);

        var userAchievement = UserAchievement.Create(userId, achievement1.Id);
        userAchievement.UpdateProgress(60);

        _achievementRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Achievement> { achievement1, achievement2 });

        _userAchievementRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAchievement> { userAchievement });

        var query = new GetAchievementsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);

        var dto1 = result.First(d => d.Code == "STREAK_7_DAYS");
        dto1.Id.Should().Be(achievement1.Id);
        dto1.Name.Should().Be("Giocatore Costante");
        dto1.Description.Should().Be("Play 7 days in a row");
        dto1.IconUrl.Should().Be("https://icons.example.com/streak7.png");
        dto1.Points.Should().Be(50);
        dto1.Rarity.Should().Be("Uncommon");
        dto1.Category.Should().Be("Streak");
        dto1.Threshold.Should().Be(7);
        dto1.Progress.Should().Be(60);
        dto1.IsUnlocked.Should().BeFalse();
        dto1.UnlockedAt.Should().BeNull();

        var dto2 = result.First(d => d.Code == "COLLECTOR_100");
        dto2.Id.Should().Be(achievement2.Id);
        dto2.Points.Should().Be(200);
        dto2.Rarity.Should().Be("Epic");
        dto2.Category.Should().Be("Milestone");
        dto2.Threshold.Should().Be(100);
        dto2.Progress.Should().BeNull();
        dto2.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithNoUserAchievements_ReturnsAllWithDefaultProgress()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var achievement = Achievement.Create(
            "FIRST_GAME", "Prima Partita", "Play your first game",
            "https://icons.example.com/first.png", 10,
            AchievementRarity.Common, AchievementCategory.Milestone, 1);

        _achievementRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Achievement> { achievement });

        _userAchievementRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAchievement>());

        var query = new GetAchievementsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Code.Should().Be("FIRST_GAME");
        dto.Progress.Should().BeNull();
        dto.IsUnlocked.Should().BeFalse();
        dto.UnlockedAt.Should().BeNull();
    }

    #endregion

    #region Handle - Null Query

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Handle - Unlocked Achievement

    [Fact]
    public async Task Handle_WithUnlockedAchievement_SetsIsUnlockedTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var achievement = Achievement.Create(
            "EXPERT_CHESS", "Esperto di Scacchi", "Reach expertise in Chess",
            "https://icons.example.com/chess.png", 100,
            AchievementRarity.Rare, AchievementCategory.Expertise, 10);

        var userAchievement = UserAchievement.Create(userId, achievement.Id);
        userAchievement.UpdateProgress(100); // Unlocks the achievement

        _achievementRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Achievement> { achievement });

        _userAchievementRepoMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserAchievement> { userAchievement });

        var query = new GetAchievementsQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.IsUnlocked.Should().BeTrue();
        dto.UnlockedAt.Should().NotBeNull();
        dto.Progress.Should().Be(100);
        dto.Rarity.Should().Be("Rare");
        dto.Category.Should().Be("Expertise");
    }

    #endregion
}
