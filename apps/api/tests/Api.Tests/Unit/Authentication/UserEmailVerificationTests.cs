using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for User email verification methods.
/// Issue #3672: Email verification flow with grace period.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public sealed class UserEmailVerificationTests
{
    [Fact]
    public void VerifyEmail_SetsFieldsCorrectly()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.VerifyEmail();

        // Assert
        user.EmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().NotBeNull();
        user.VerificationGracePeriodEndsAt.Should().BeNull(); // Grace period cleared
        user.EmailVerifiedAt.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void VerifyEmail_RaisesEmailVerifiedEvent()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.VerifyEmail();

        // Assert
        user.DomainEvents.Should().ContainSingle();
        var domainEvent = user.DomainEvents.First().Should().BeOfType<EmailVerifiedEvent>().Subject;
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.VerifiedAt.Should().Be(user.EmailVerifiedAt);
    }

    [Fact]
    public void VerifyEmail_IsIdempotent()
    {
        // Arrange
        var user = CreateTestUser();
        user.VerifyEmail();
        var firstVerifiedAt = user.EmailVerifiedAt;
        user.ClearDomainEvents();

        // Act - verify again
        user.VerifyEmail();

        // Assert - no change, no new events
        user.EmailVerifiedAt.Should().Be(firstVerifiedAt);
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void IsInGracePeriod_WhenGracePeriodInFuture_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.SetVerificationGracePeriod(now.AddDays(3));

        // Act
        var result = user.IsInGracePeriod(timeProvider);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsInGracePeriod_WhenGracePeriodPast_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.SetVerificationGracePeriod(now.AddDays(-1));

        // Act
        var result = user.IsInGracePeriod(timeProvider);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsInGracePeriod_WhenNoGracePeriod_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.IsInGracePeriod();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RequiresVerification_WhenUnverifiedAndPastGracePeriod_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.SetVerificationGracePeriod(now.AddDays(-1)); // Grace period ended

        // Act
        var result = user.RequiresVerification(timeProvider);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RequiresVerification_WhenUnverifiedInGracePeriod_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.SetVerificationGracePeriod(now.AddDays(5));

        // Act
        var result = user.RequiresVerification(timeProvider);

        // Assert
        result.Should().BeFalse(); // Still in grace period
    }

    [Fact]
    public void RequiresVerification_WhenVerified_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        user.VerifyEmail();

        // Act
        var result = user.RequiresVerification();

        // Assert
        result.Should().BeFalse(); // Verified users never require verification
    }

    [Fact]
    public void SetVerificationGracePeriod_WhenNotVerified_SetsGracePeriod()
    {
        // Arrange
        var user = CreateTestUser();
        var gracePeriodEnd = DateTime.UtcNow.AddDays(7);

        // Act
        user.SetVerificationGracePeriod(gracePeriodEnd);

        // Assert
        user.VerificationGracePeriodEndsAt.Should().Be(gracePeriodEnd);
    }

    [Fact]
    public void SetVerificationGracePeriod_WhenAlreadyVerified_DoesNothing()
    {
        // Arrange
        var user = CreateTestUser();
        user.VerifyEmail();

        // Act
        user.SetVerificationGracePeriod(DateTime.UtcNow.AddDays(7));

        // Assert
        user.VerificationGracePeriodEndsAt.Should().BeNull(); // Should not set for verified users
    }

    [Fact]
    public void VerifyEmail_ClearsGracePeriod()
    {
        // Arrange
        var user = CreateTestUser();
        user.SetVerificationGracePeriod(DateTime.UtcNow.AddDays(5));

        // Act
        user.VerifyEmail();

        // Assert
        user.VerificationGracePeriodEndsAt.Should().BeNull(); // Grace period cleared
    }

    /// <summary>
    /// Helper to create a test user.
    /// </summary>
    private static User CreateTestUser()
    {
        return new User(
            id: Guid.NewGuid(),
            email: Email.Parse("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("Test123!@#"),
            role: Role.User,
            tier: UserTier.Free
        );
    }
}
