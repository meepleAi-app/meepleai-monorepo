using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Xunit;

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
        Assert.True(user.EmailVerified);
        Assert.NotNull(user.EmailVerifiedAt);
        Assert.Null(user.VerificationGracePeriodEndsAt); // Grace period cleared
        Assert.True(user.EmailVerifiedAt.Value.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public void VerifyEmail_RaisesEmailVerifiedEvent()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.VerifyEmail();

        // Assert
        Assert.Single(user.DomainEvents);
        var domainEvent = Assert.IsType<EmailVerifiedEvent>(user.DomainEvents.First());
        Assert.Equal(user.Id, domainEvent.UserId);
        Assert.Equal(user.EmailVerifiedAt, domainEvent.VerifiedAt);
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
        Assert.Equal(firstVerifiedAt, user.EmailVerifiedAt);
        Assert.Empty(user.DomainEvents);
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
        Assert.True(result);
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
        Assert.False(result);
    }

    [Fact]
    public void IsInGracePeriod_WhenNoGracePeriod_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.IsInGracePeriod();

        // Assert
        Assert.False(result);
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
        Assert.True(result);
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
        Assert.False(result); // Still in grace period
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
        Assert.False(result); // Verified users never require verification
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
        Assert.Equal(gracePeriodEnd, user.VerificationGracePeriodEndsAt);
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
        Assert.Null(user.VerificationGracePeriodEndsAt); // Should not set for verified users
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
        Assert.Null(user.VerificationGracePeriodEndsAt); // Grace period cleared
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
