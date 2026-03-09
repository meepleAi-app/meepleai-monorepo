using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

/// <summary>
/// Unit tests for User entity account lockout functionality.
/// Issue #3339: Account lockout after failed login attempts.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class UserLockoutTests
{
    private User CreateTestUser()
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("SecurePassword123!"),
            role: Role.User
        );
    }

    #region IsLockedOut Tests

    [Fact]
    public void IsLockedOut_NewUser_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.False(user.IsLockedOut());
    }

    [Fact]
    public void IsLockedOut_LockedUser_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();

        // Lock the account (5 failed attempts)
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin();
        }

        // Act & Assert
        Assert.True(user.IsLockedOut());
    }

    [Fact]
    public void IsLockedOut_ExpiredLockout_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var mockTimeProvider = new Mock<TimeProvider>();

        // First, lock the account at "now"
        var lockTime = new DateTimeOffset(2026, 2, 4, 10, 0, 0, TimeSpan.Zero);
        mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(lockTime);

        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(timeProvider: mockTimeProvider.Object);
        }

        Assert.True(user.IsLockedOut(mockTimeProvider.Object));

        // Move time forward past the lockout duration (15 minutes)
        var afterLockout = lockTime.AddMinutes(16);
        mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(afterLockout);

        // Act & Assert
        Assert.False(user.IsLockedOut(mockTimeProvider.Object));
    }

    #endregion

    #region RecordFailedLogin Tests

    [Fact]
    public void RecordFailedLogin_IncrementsCounter()
    {
        // Arrange
        var user = CreateTestUser();
        Assert.Equal(0, user.FailedLoginAttempts);

        // Act
        user.RecordFailedLogin();

        // Assert
        Assert.Equal(1, user.FailedLoginAttempts);
    }

    [Fact]
    public void RecordFailedLogin_BelowThreshold_DoesNotLock()
    {
        // Arrange
        var user = CreateTestUser();

        // Act - 4 failed attempts (below default threshold of 5)
        for (int i = 0; i < 4; i++)
        {
            user.RecordFailedLogin();
        }

        // Assert
        Assert.Equal(4, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut());
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void RecordFailedLogin_AtThreshold_LocksAccount()
    {
        // Arrange
        var user = CreateTestUser();

        // Act - 5 failed attempts (reaches default threshold)
        bool wasLocked = false;
        for (int i = 0; i < 5; i++)
        {
            wasLocked = user.RecordFailedLogin();
        }

        // Assert
        Assert.True(wasLocked);
        Assert.Equal(5, user.FailedLoginAttempts);
        Assert.True(user.IsLockedOut());
        Assert.NotNull(user.LockedUntil);
    }

    [Fact]
    public void RecordFailedLogin_CustomThreshold_RespectsCustomValue()
    {
        // Arrange
        var user = CreateTestUser();

        // Act - 3 failed attempts with threshold of 3
        bool wasLocked = false;
        for (int i = 0; i < 3; i++)
        {
            wasLocked = user.RecordFailedLogin(maxAttempts: 3);
        }

        // Assert
        Assert.True(wasLocked);
        Assert.True(user.IsLockedOut());
    }

    [Fact]
    public void RecordFailedLogin_WhenLocked_DoesNotIncrementCounter()
    {
        // Arrange
        var user = CreateTestUser();

        // Lock the account
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin();
        }
        Assert.Equal(5, user.FailedLoginAttempts);

        // Act - Try to record another failed login while locked
        user.RecordFailedLogin();

        // Assert - Counter should not increment
        Assert.Equal(5, user.FailedLoginAttempts);
    }

    [Fact]
    public void RecordFailedLogin_RaisesAccountLockedEvent()
    {
        // Arrange
        var user = CreateTestUser();

        // Act - Lock the account
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(ipAddress: "192.168.1.1");
        }

        // Assert - Check domain events
        var domainEvents = user.DomainEvents;
        Assert.Contains(domainEvents, e => e is AccountLockedEvent);

        var lockEvent = domainEvents.OfType<AccountLockedEvent>().First();
        Assert.Equal(user.Id, lockEvent.UserId);
        Assert.Equal(5, lockEvent.FailedAttempts);
        Assert.Equal("192.168.1.1", lockEvent.IpAddress);
    }

    #endregion

    #region RecordSuccessfulLogin Tests

    [Fact]
    public void RecordSuccessfulLogin_ResetsFailedAttempts()
    {
        // Arrange
        var user = CreateTestUser();
        for (int i = 0; i < 3; i++)
        {
            user.RecordFailedLogin();
        }
        Assert.Equal(3, user.FailedLoginAttempts);

        // Act
        user.RecordSuccessfulLogin();

        // Assert
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public void RecordSuccessfulLogin_ClearsLockedUntil()
    {
        // Arrange
        var user = CreateTestUser();

        // This shouldn't happen in practice (can't login if locked),
        // but test the behavior anyway for edge cases
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin();
        }
        Assert.NotNull(user.LockedUntil);

        // Act
        user.RecordSuccessfulLogin();

        // Assert
        Assert.Null(user.LockedUntil);
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public void RecordSuccessfulLogin_NoFailedAttempts_NoOp()
    {
        // Arrange
        var user = CreateTestUser();
        Assert.Equal(0, user.FailedLoginAttempts);

        // Act
        user.RecordSuccessfulLogin();

        // Assert - No exception, no change
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
    }

    #endregion

    #region Unlock Tests

    [Fact]
    public void Unlock_LockedAccount_UnlocksSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var adminId = Guid.NewGuid();

        // Lock the account
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin();
        }
        Assert.True(user.IsLockedOut());

        // Act
        user.Unlock(adminId);

        // Assert
        Assert.False(user.IsLockedOut());
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
    }

    [Fact]
    public void Unlock_NotLocked_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var adminId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => user.Unlock(adminId));
        Assert.Contains("not locked", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unlock_WithFailedAttemptsButNotLocked_UnlocksSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var adminId = Guid.NewGuid();

        // 3 failed attempts (not enough to lock)
        for (int i = 0; i < 3; i++)
        {
            user.RecordFailedLogin();
        }
        Assert.Equal(3, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut());

        // Act - Admin can still reset the counter
        user.Unlock(adminId);

        // Assert
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public void Unlock_RaisesAccountUnlockedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var adminId = Guid.NewGuid();

        // Lock the account
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin();
        }

        // Clear previous events
        user.ClearDomainEvents();

        // Act
        user.Unlock(adminId);

        // Assert
        var domainEvents = user.DomainEvents;
        Assert.Contains(domainEvents, e => e is AccountUnlockedEvent);

        var unlockEvent = domainEvents.OfType<AccountUnlockedEvent>().First();
        Assert.Equal(user.Id, unlockEvent.UserId);
        Assert.True(unlockEvent.WasManualUnlock);
        Assert.Equal(adminId, unlockEvent.UnlockedByAdminId);
    }

    #endregion

    #region GetRemainingLockoutDuration Tests

    [Fact]
    public void GetRemainingLockoutDuration_NotLocked_ReturnsZero()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var duration = user.GetRemainingLockoutDuration();

        // Assert
        Assert.Equal(TimeSpan.Zero, duration);
    }

    [Fact]
    public void GetRemainingLockoutDuration_Locked_ReturnsPositiveDuration()
    {
        // Arrange
        var user = CreateTestUser();
        var mockTimeProvider = new Mock<TimeProvider>();
        var now = new DateTimeOffset(2026, 2, 4, 10, 0, 0, TimeSpan.Zero);
        mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(now);

        // Lock the account
        for (int i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(lockoutMinutes: 15, timeProvider: mockTimeProvider.Object);
        }

        // Move time forward 5 minutes
        var laterTime = now.AddMinutes(5);
        mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(laterTime);

        // Act
        var duration = user.GetRemainingLockoutDuration(mockTimeProvider.Object);

        // Assert - Should be approximately 10 minutes remaining
        Assert.True(duration > TimeSpan.Zero);
        Assert.True(duration <= TimeSpan.FromMinutes(10));
    }

    #endregion

    #region RestoreLockoutState Tests

    [Fact]
    public void RestoreLockoutState_RestoresCorrectly()
    {
        // Arrange
        var user = CreateTestUser();
        var lockedUntil = DateTime.UtcNow.AddMinutes(10);

        // Act
        user.RestoreLockoutState(failedLoginAttempts: 3, lockedUntil: lockedUntil);

        // Assert
        Assert.Equal(3, user.FailedLoginAttempts);
        Assert.Equal(lockedUntil, user.LockedUntil);
    }

    #endregion
}
