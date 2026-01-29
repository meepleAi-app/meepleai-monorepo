using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for User entity Level/XP functionality (Issue #3141).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class UserEntityLevelTests
{
    [Fact]
    public void Constructor_SetsDefaultLevelAndExperience()
    {
        // Arrange & Act
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("test@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User
        );

        // Assert
        user.Level.Should().Be(1);
        user.ExperiencePoints.Should().Be(0);
    }

    [Fact]
    public void SetLevel_WithValidLevel_UpdatesLevel()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("test@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User
        );

        // Act
        user.SetLevel(5);

        // Assert
        user.Level.Should().Be(5);
    }

    [Fact]
    public void SetLevel_WithNegativeLevel_ThrowsArgumentException()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("test@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User
        );

        // Act & Assert
        Assert.Throws<ArgumentException>(() => user.SetLevel(-1));
    }

    [Fact]
    public void AddExperience_WithValidPoints_AddsToExperience()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("test@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User
        );

        // Act
        user.AddExperience(100);
        user.AddExperience(50);

        // Assert
        user.ExperiencePoints.Should().Be(150);
    }

    [Fact]
    public void AddExperience_WithNegativePoints_ThrowsArgumentException()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("test@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User
        );

        // Act & Assert
        Assert.Throws<ArgumentException>(() => user.AddExperience(-10));
    }
}
