using Api.SharedKernel.Domain.ValueObjects;
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
        ((Action)(() => user.SetLevel(-1))).Should().Throw<ArgumentException>();
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
        ((Action)(() => user.AddExperience(-10))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetLevel_WithLevelAbove100_ThrowsArgumentException()
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
        var exception = ((Action)(() => user.SetLevel(101))).Should().Throw<ArgumentException>().Which;
        exception.Message.Should().Contain("cannot exceed 100");
    }

    [Fact]
    public void SetLevel_WithMaxLevel_UpdatesSuccessfully()
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
        user.SetLevel(100);

        // Assert
        user.Level.Should().Be(100);
    }

    [Fact]
    public void SetLevel_WithSameLevel_DoesNotEmitEvent()
    {
        // Arrange
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("test@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User
        );
        user.SetLevel(5);
        user.ClearDomainEvents(); // Clear the event from first SetLevel

        // Act
        user.SetLevel(5); // Same level

        // Assert
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetLevel_WithDifferentLevel_EmitsDomainEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User(
            id: userId,
            email: new Email("test@test.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Password123!"),
            role: Role.User
        );
        user.ClearDomainEvents(); // Clear any initial events

        // Act
        user.SetLevel(10);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var levelEvent = user.DomainEvents.First() as Api.BoundedContexts.Authentication.Domain.Events.UserLevelChangedEvent;
        levelEvent.Should().NotBeNull();
        levelEvent!.UserId.Should().Be(userId);
        levelEvent.OldLevel.Should().Be(1); // Default level
        levelEvent.NewLevel.Should().Be(10);
    }
}
