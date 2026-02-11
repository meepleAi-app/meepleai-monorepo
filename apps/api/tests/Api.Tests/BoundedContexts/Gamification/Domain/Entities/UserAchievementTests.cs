using Api.BoundedContexts.Gamification.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Gamification.Domain.Entities;

/// <summary>
/// Unit tests for UserAchievement aggregate root entity.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public class UserAchievementTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsUserAchievementWithCorrectDefaults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var achievementId = Guid.NewGuid();

        // Act
        var userAchievement = UserAchievement.Create(userId, achievementId);

        // Assert
        userAchievement.Should().NotBeNull();
        userAchievement.Id.Should().NotBe(Guid.Empty);
        userAchievement.UserId.Should().Be(userId);
        userAchievement.AchievementId.Should().Be(achievementId);
        userAchievement.Progress.Should().Be(0);
        userAchievement.UnlockedAt.Should().BeNull();
        userAchievement.IsUnlocked.Should().BeFalse();
        userAchievement.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        userAchievement.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var act = () => UserAchievement.Create(Guid.Empty, Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Fact]
    public void Create_WithEmptyAchievementId_ThrowsArgumentException()
    {
        // Act
        var act = () => UserAchievement.Create(Guid.NewGuid(), Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("achievementId");
    }

    #endregion

    #region UpdateProgress Tests

    [Fact]
    public void UpdateProgress_WithValidValue_SetsProgressCorrectly()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();

        // Act
        var result = userAchievement.UpdateProgress(50);

        // Assert
        result.Should().BeFalse();
        userAchievement.Progress.Should().Be(50);
    }

    [Fact]
    public void UpdateProgress_WithNegativeValue_ClampsToZero()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();

        // Act
        var result = userAchievement.UpdateProgress(-10);

        // Assert
        result.Should().BeFalse();
        userAchievement.Progress.Should().Be(0);
    }

    [Fact]
    public void UpdateProgress_WithValueOver100_ClampsTo100()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();

        // Act
        var result = userAchievement.UpdateProgress(150);

        // Assert
        result.Should().BeTrue();
        userAchievement.Progress.Should().Be(100);
    }

    [Fact]
    public void UpdateProgress_At100_UnlocksAchievement()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();

        // Act
        var result = userAchievement.UpdateProgress(100);

        // Assert
        result.Should().BeTrue();
        userAchievement.Progress.Should().Be(100);
        userAchievement.IsUnlocked.Should().BeTrue();
        userAchievement.UnlockedAt.Should().NotBeNull();
        userAchievement.UnlockedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateProgress_At99_DoesNotUnlock()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();

        // Act
        var result = userAchievement.UpdateProgress(99);

        // Assert
        result.Should().BeFalse();
        userAchievement.Progress.Should().Be(99);
        userAchievement.IsUnlocked.Should().BeFalse();
        userAchievement.UnlockedAt.Should().BeNull();
    }

    [Fact]
    public void UpdateProgress_WhenAlreadyUnlocked_ReturnsFalseAndDoesNotChangeProgress()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();
        userAchievement.UpdateProgress(100);
        var originalUnlockedAt = userAchievement.UnlockedAt;
        var originalUpdatedAt = userAchievement.UpdatedAt;

        // Act
        var result = userAchievement.UpdateProgress(50);

        // Assert
        result.Should().BeFalse();
        userAchievement.Progress.Should().Be(100);
        userAchievement.UnlockedAt.Should().Be(originalUnlockedAt);
        userAchievement.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public void UpdateProgress_SetsUpdatedAt()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();
        userAchievement.UpdatedAt.Should().BeNull();

        // Act
        userAchievement.UpdateProgress(25);

        // Assert
        userAchievement.UpdatedAt.Should().NotBeNull();
        userAchievement.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateProgress_MultipleUpdates_TracksLatestUpdatedAt()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();

        // Act
        userAchievement.UpdateProgress(25);
        var firstUpdate = userAchievement.UpdatedAt;

        userAchievement.UpdateProgress(75);
        var secondUpdate = userAchievement.UpdatedAt;

        // Assert
        secondUpdate.Should().BeOnOrAfter(firstUpdate!.Value);
    }

    #endregion

    #region IsUnlocked Property Tests

    [Fact]
    public void IsUnlocked_WhenNotUnlocked_ReturnsFalse()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();

        // Assert
        userAchievement.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public void IsUnlocked_WhenUnlocked_ReturnsTrue()
    {
        // Arrange
        var userAchievement = CreateTestUserAchievement();
        userAchievement.UpdateProgress(100);

        // Assert
        userAchievement.IsUnlocked.Should().BeTrue();
    }

    #endregion

    #region Reconstitute Factory Tests

    [Fact]
    public void Reconstitute_WithAllParameters_RestoresAllFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var achievementId = Guid.NewGuid();
        var progress = 75;
        var unlockedAt = (DateTime?)null;
        var createdAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 6, 20, 14, 0, 0, DateTimeKind.Utc);

        // Act
        var userAchievement = UserAchievement.Reconstitute(
            id, userId, achievementId, progress,
            unlockedAt, createdAt, updatedAt);

        // Assert
        userAchievement.Id.Should().Be(id);
        userAchievement.UserId.Should().Be(userId);
        userAchievement.AchievementId.Should().Be(achievementId);
        userAchievement.Progress.Should().Be(progress);
        userAchievement.UnlockedAt.Should().BeNull();
        userAchievement.IsUnlocked.Should().BeFalse();
        userAchievement.CreatedAt.Should().Be(createdAt);
        userAchievement.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Reconstitute_WithUnlockedAt_RestoresUnlockedState()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var achievementId = Guid.NewGuid();
        var progress = 100;
        var unlockedAt = new DateTime(2025, 6, 20, 14, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 6, 20, 14, 0, 0, DateTimeKind.Utc);

        // Act
        var userAchievement = UserAchievement.Reconstitute(
            id, userId, achievementId, progress,
            unlockedAt, createdAt, updatedAt);

        // Assert
        userAchievement.Progress.Should().Be(100);
        userAchievement.UnlockedAt.Should().Be(unlockedAt);
        userAchievement.IsUnlocked.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static UserAchievement CreateTestUserAchievement()
    {
        return UserAchievement.Create(Guid.NewGuid(), Guid.NewGuid());
    }

    #endregion
}
