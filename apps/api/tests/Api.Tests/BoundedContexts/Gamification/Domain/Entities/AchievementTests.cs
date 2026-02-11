using Api.BoundedContexts.Gamification.Domain.Entities;
using Api.BoundedContexts.Gamification.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Gamification.Domain.Entities;

/// <summary>
/// Unit tests for Achievement aggregate root entity.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public class AchievementTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsAchievementWithAllPropertiesSet()
    {
        // Arrange
        var code = "STREAK_7_DAYS";
        var name = "Giocatore Costante";
        var description = "Play games 7 days in a row";
        var iconUrl = "https://example.com/streak.png";
        var points = 50;
        var rarity = AchievementRarity.Rare;
        var category = AchievementCategory.Streak;
        var threshold = 7;

        // Act
        var achievement = Achievement.Create(
            code, name, description, iconUrl,
            points, rarity, category, threshold);

        // Assert
        achievement.Should().NotBeNull();
        achievement.Id.Should().NotBe(Guid.Empty);
        achievement.Code.Should().Be(code);
        achievement.Name.Should().Be(name);
        achievement.Description.Should().Be(description);
        achievement.IconUrl.Should().Be(iconUrl);
        achievement.Points.Should().Be(points);
        achievement.Rarity.Should().Be(rarity);
        achievement.Category.Should().Be(category);
        achievement.Threshold.Should().Be(threshold);
        achievement.IsActive.Should().BeTrue();
        achievement.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCode_ThrowsArgumentException(string? invalidCode)
    {
        // Act
        var act = () => Achievement.Create(
            invalidCode!,
            "Name",
            "Description",
            "https://example.com/icon.png",
            10,
            AchievementRarity.Common,
            AchievementCategory.Milestone,
            5);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("code");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ThrowsArgumentException(string? invalidName)
    {
        // Act
        var act = () => Achievement.Create(
            "VALID_CODE",
            invalidName!,
            "Description",
            "https://example.com/icon.png",
            10,
            AchievementRarity.Common,
            AchievementCategory.Milestone,
            5);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyDescription_ThrowsArgumentException(string? invalidDescription)
    {
        // Act
        var act = () => Achievement.Create(
            "VALID_CODE",
            "Valid Name",
            invalidDescription!,
            "https://example.com/icon.png",
            10,
            AchievementRarity.Common,
            AchievementCategory.Milestone,
            5);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("description");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithZeroOrNegativePoints_ThrowsArgumentOutOfRangeException(int invalidPoints)
    {
        // Act
        var act = () => Achievement.Create(
            "VALID_CODE",
            "Valid Name",
            "Valid Description",
            "https://example.com/icon.png",
            invalidPoints,
            AchievementRarity.Common,
            AchievementCategory.Milestone,
            5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("points");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public void Create_WithZeroOrNegativeThreshold_ThrowsArgumentOutOfRangeException(int invalidThreshold)
    {
        // Act
        var act = () => Achievement.Create(
            "VALID_CODE",
            "Valid Name",
            "Valid Description",
            "https://example.com/icon.png",
            10,
            AchievementRarity.Common,
            AchievementCategory.Milestone,
            invalidThreshold);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("threshold");
    }

    [Fact]
    public void Create_WithNullIconUrl_DefaultsToEmptyString()
    {
        // Act
        var achievement = Achievement.Create(
            "VALID_CODE",
            "Valid Name",
            "Valid Description",
            null!,
            10,
            AchievementRarity.Common,
            AchievementCategory.Milestone,
            5);

        // Assert
        achievement.IconUrl.Should().Be(string.Empty);
    }

    #endregion

    #region Reconstitute Factory Tests

    [Fact]
    public void Reconstitute_WithAllParameters_RestoresAllFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var code = "COLLECTOR_100";
        var name = "Master Collector";
        var description = "Collect 100 games in your library";
        var iconUrl = "https://example.com/collector.png";
        var points = 100;
        var rarity = AchievementRarity.Epic;
        var category = AchievementCategory.Milestone;
        var threshold = 100;
        var isActive = false;
        var createdAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var achievement = Achievement.Reconstitute(
            id, code, name, description, iconUrl,
            points, rarity, category, threshold,
            isActive, createdAt);

        // Assert
        achievement.Id.Should().Be(id);
        achievement.Code.Should().Be(code);
        achievement.Name.Should().Be(name);
        achievement.Description.Should().Be(description);
        achievement.IconUrl.Should().Be(iconUrl);
        achievement.Points.Should().Be(points);
        achievement.Rarity.Should().Be(rarity);
        achievement.Category.Should().Be(category);
        achievement.Threshold.Should().Be(threshold);
        achievement.IsActive.Should().Be(isActive);
        achievement.CreatedAt.Should().Be(createdAt);
    }

    #endregion

    #region Deactivate Tests

    [Fact]
    public void Deactivate_WhenActive_SetsIsActiveToFalse()
    {
        // Arrange
        var achievement = CreateTestAchievement();
        achievement.IsActive.Should().BeTrue();

        // Act
        achievement.Deactivate();

        // Assert
        achievement.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_RemainsInactive()
    {
        // Arrange
        var achievement = CreateTestAchievement();
        achievement.Deactivate();

        // Act
        achievement.Deactivate();

        // Assert
        achievement.IsActive.Should().BeFalse();
    }

    #endregion

    #region Activate Tests

    [Fact]
    public void Activate_WhenInactive_SetsIsActiveToTrue()
    {
        // Arrange
        var achievement = CreateTestAchievement();
        achievement.Deactivate();
        achievement.IsActive.Should().BeFalse();

        // Act
        achievement.Activate();

        // Assert
        achievement.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_WhenAlreadyActive_RemainsActive()
    {
        // Arrange
        var achievement = CreateTestAchievement();
        achievement.IsActive.Should().BeTrue();

        // Act
        achievement.Activate();

        // Assert
        achievement.IsActive.Should().BeTrue();
    }

    #endregion

    #region Rarity and Category Enum Tests

    [Theory]
    [InlineData(AchievementRarity.Common)]
    [InlineData(AchievementRarity.Uncommon)]
    [InlineData(AchievementRarity.Rare)]
    [InlineData(AchievementRarity.Epic)]
    [InlineData(AchievementRarity.Legendary)]
    public void Create_WithDifferentRarities_SetsRarityCorrectly(AchievementRarity rarity)
    {
        // Act
        var achievement = Achievement.Create(
            "CODE", "Name", "Description",
            "https://example.com/icon.png",
            10, rarity, AchievementCategory.Milestone, 5);

        // Assert
        achievement.Rarity.Should().Be(rarity);
    }

    [Theory]
    [InlineData(AchievementCategory.Streak)]
    [InlineData(AchievementCategory.Milestone)]
    [InlineData(AchievementCategory.Expertise)]
    [InlineData(AchievementCategory.Social)]
    [InlineData(AchievementCategory.Special)]
    public void Create_WithDifferentCategories_SetsCategoryCorrectly(AchievementCategory category)
    {
        // Act
        var achievement = Achievement.Create(
            "CODE", "Name", "Description",
            "https://example.com/icon.png",
            10, AchievementRarity.Common, category, 5);

        // Assert
        achievement.Category.Should().Be(category);
    }

    #endregion

    #region Helper Methods

    private static Achievement CreateTestAchievement()
    {
        return Achievement.Create(
            "TEST_ACHIEVEMENT",
            "Test Achievement",
            "Test achievement description",
            "https://example.com/test.png",
            25,
            AchievementRarity.Common,
            AchievementCategory.Milestone,
            10);
    }

    #endregion
}
