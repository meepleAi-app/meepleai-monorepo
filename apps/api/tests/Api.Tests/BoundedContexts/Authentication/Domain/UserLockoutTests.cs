using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

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
        user.IsLockedOut().Should().BeFalse();
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
        user.IsLockedOut().Should().BeTrue();
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

        user.IsLockedOut(mockTimeProvider.Object).Should().BeTrue();

        // Move time forward past the lockout duration (15 minutes)
        var afterLockout = lockTime.AddMinutes(16);
        mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(afterLockout);

        // Act & Assert
        user.IsLockedOut(mockTimeProvider.Object).Should().BeFalse();
    }

    #endregion

    #region RecordFailedLogin Tests

    [Fact]
    public void RecordFailedLogin_IncrementsCounter()
    {
        // Arrange
        var user = CreateTestUser();
        user.FailedLoginAttempts.Should().Be(0);

        // Act
        user.RecordFailedLogin();

        // Assert
        user.FailedLoginAttempts.Should().Be(1);
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
        user.FailedLoginAttempts.Should().Be(4);
        user.IsLockedOut().Should().BeFalse();
        user.LockedUntil.Should().BeNull();
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
        wasLocked.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(5);
        user.IsLockedOut().Should().BeTrue();
        user.LockedUntil.Should().NotBeNull();
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
        wasLocked.Should().BeTrue();
        user.IsLockedOut().Should().BeTrue();
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
        user.FailedLoginAttempts.Should().Be(5);

        // Act - Try to record another failed login while locked
        user.RecordFailedLogin();

        // Assert - Counter should not increment
        user.FailedLoginAttempts.Should().Be(5);
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
        domainEvents.Should().Contain(e => e is AccountLockedEvent);

        var lockEvent = domainEvents.OfType<AccountLockedEvent>().First();
        lockEvent.UserId.Should().Be(user.Id);
        lockEvent.FailedAttempts.Should().Be(5);
        lockEvent.IpAddress.Should().Be("192.168.1.1");
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
        user.FailedLoginAttempts.Should().Be(3);

        // Act
        user.RecordSuccessfulLogin();

        // Assert
        user.FailedLoginAttempts.Should().Be(0);
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
        user.LockedUntil.Should().NotBeNull();

        // Act
        user.RecordSuccessfulLogin();

        // Assert
        user.LockedUntil.Should().BeNull();
        user.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public void RecordSuccessfulLogin_NoFailedAttempts_NoOp()
    {
        // Arrange
        var user = CreateTestUser();
        user.FailedLoginAttempts.Should().Be(0);

        // Act
        user.RecordSuccessfulLogin();

        // Assert - No exception, no change
        user.FailedLoginAttempts.Should().Be(0);
        user.LockedUntil.Should().BeNull();
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
        user.IsLockedOut().Should().BeTrue();

        // Act
        user.Unlock(adminId);

        // Assert
        user.IsLockedOut().Should().BeFalse();
        user.FailedLoginAttempts.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void Unlock_NotLocked_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var adminId = Guid.NewGuid();

        // Act & Assert
        var act = () => user.Unlock(adminId);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().ContainEquivalentOf("not locked");
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
        user.FailedLoginAttempts.Should().Be(3);
        user.IsLockedOut().Should().BeFalse();

        // Act - Admin can still reset the counter
        user.Unlock(adminId);

        // Assert
        user.FailedLoginAttempts.Should().Be(0);
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
        domainEvents.Should().Contain(e => e is AccountUnlockedEvent);

        var unlockEvent = domainEvents.OfType<AccountUnlockedEvent>().First();
        unlockEvent.UserId.Should().Be(user.Id);
        unlockEvent.WasManualUnlock.Should().BeTrue();
        unlockEvent.UnlockedByAdminId.Should().Be(adminId);
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
        duration.Should().Be(TimeSpan.Zero);
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
        (duration > TimeSpan.Zero).Should().BeTrue();
        (duration <= TimeSpan.FromMinutes(10)).Should().BeTrue();
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
        user.FailedLoginAttempts.Should().Be(3);
        user.LockedUntil.Should().Be(lockedUntil);
    }

    #endregion
}
