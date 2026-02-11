using Api.BoundedContexts.Gamification.Application.Queries.GetAchievements;
using Api.BoundedContexts.Gamification.Application.Queries.GetRecentAchievements;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Gamification.Application.Validators;

/// <summary>
/// Tests for Gamification query validators.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public sealed class AchievementValidatorTests
{
    #region GetAchievementsQueryValidator Tests

    private readonly GetAchievementsQueryValidator _getAchievementsValidator = new();

    [Fact]
    public void GetAchievements_ValidQuery_PassesValidation()
    {
        // Arrange
        var query = new GetAchievementsQuery(Guid.NewGuid());

        // Act
        var result = _getAchievementsValidator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GetAchievements_EmptyUserId_FailsValidation()
    {
        // Arrange
        var query = new GetAchievementsQuery(Guid.Empty);

        // Act
        var result = _getAchievementsValidator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required.");
    }

    #endregion

    #region GetRecentAchievementsQueryValidator Tests

    private readonly GetRecentAchievementsQueryValidator _getRecentValidator = new();

    [Fact]
    public void GetRecent_ValidQuery_PassesValidation()
    {
        // Arrange
        var query = new GetRecentAchievementsQuery(Guid.NewGuid(), 5);

        // Act
        var result = _getRecentValidator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void GetRecent_EmptyUserId_FailsValidation()
    {
        // Arrange
        var query = new GetRecentAchievementsQuery(Guid.Empty, 5);

        // Act
        var result = _getRecentValidator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required.");
    }

    [Fact]
    public void GetRecent_LimitZero_FailsValidation()
    {
        // Arrange
        var query = new GetRecentAchievementsQuery(Guid.NewGuid(), 0);

        // Act
        var result = _getRecentValidator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorMessage("Limit must be between 1 and 20.");
    }

    [Fact]
    public void GetRecent_LimitExceeds20_FailsValidation()
    {
        // Arrange
        var query = new GetRecentAchievementsQuery(Guid.NewGuid(), 21);

        // Act
        var result = _getRecentValidator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Limit)
            .WithErrorMessage("Limit must be between 1 and 20.");
    }

    [Fact]
    public void GetRecent_LimitOne_PassesValidation()
    {
        // Arrange
        var query = new GetRecentAchievementsQuery(Guid.NewGuid(), 1);

        // Act
        var result = _getRecentValidator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Limit);
    }

    [Fact]
    public void GetRecent_LimitTwenty_PassesValidation()
    {
        // Arrange
        var query = new GetRecentAchievementsQuery(Guid.NewGuid(), 20);

        // Act
        var result = _getRecentValidator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Limit);
    }

    #endregion
}
