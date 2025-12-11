using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Comprehensive domain tests for Session entity.
/// Tests session lifecycle, validation, expiration, revocation, and edge cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SessionEntityTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesSession()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var token = SessionToken.Generate();
        var lifetime = TimeSpan.FromDays(7);
        var ipAddress = "192.168.1.1";
        var userAgent = "Mozilla/5.0";

        // Act
        var session = new Session(id, userId, token, lifetime, ipAddress, userAgent);

        // Assert
        Assert.Equal(id, session.Id);
        Assert.Equal(userId, session.UserId);
        Assert.Equal(token.ComputeHash(), session.TokenHash);
        Assert.NotNull(session.IpAddress);
        Assert.Equal(ipAddress, session.IpAddress);
        Assert.Equal(userAgent, session.UserAgent);
        Assert.True(session.ExpiresAt > session.CreatedAt);
        Assert.Null(session.RevokedAt);
        Assert.Null(session.LastSeenAt);
    }

    [Fact]
    public void Constructor_WithDefaultLifetime_UsesThirtyDays()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var token = SessionToken.Generate();

        // Act
        var session = new Session(id, userId, token);

        // Assert
        var expectedExpiration = session.CreatedAt.Add(Session.DefaultLifetime);
        Assert.Equal(expectedExpiration, session.ExpiresAt);
        Assert.Equal(TimeSpan.FromDays(30), Session.DefaultLifetime);
    }

    [Fact]
    public void Constructor_WithCustomLifetime_UsesProvidedDuration()
    {
        // Arrange
        var customLifetime = TimeSpan.FromHours(2);
        var session = new SessionBuilder()
            .WithLifetime(customLifetime)
            .Build();

        // Act
        var actualLifetime = session.ExpiresAt - session.CreatedAt;

        // Assert
        Assert.Equal(customLifetime.TotalSeconds, actualLifetime.TotalSeconds, precision: 1);
    }

    [Fact]
    public void Constructor_WithoutOptionalParameters_CreatesValidSession()
    {
        // Arrange & Act
        var session = new SessionBuilder().Build();

        // Assert
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.NotEqual(Guid.Empty, session.UserId);
        Assert.NotNull(session.TokenHash);
        Assert.Null(session.IpAddress);
        Assert.Null(session.UserAgent);
    }
    [Fact]
    public void IsValid_WithValidSession_ReturnsTrue()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var session = new SessionBuilder()
            .WithLifetime(TimeSpan.FromDays(30))
            .Build();

        // Act
        var isValid = session.IsValid(timeProvider);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_WithExpiredSession_ReturnsFalse()
    {
        // Arrange
        var session = new SessionBuilder()
            .Expired()
            .Build();

        // Use time provider set to future (session is already expired)
        var timeProvider = new FakeTimeProvider(DateTime.UtcNow.AddDays(1));

        // Act
        var isValid = session.IsValid(timeProvider);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_WithRevokedSession_ReturnsFalse()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var session = new SessionBuilder()
            .Revoked()
            .Build();

        // Act
        var isValid = session.IsValid(timeProvider);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_ExactlyAtExpiration_ReturnsFalse()
    {
        // Arrange
        var session = new SessionBuilder()
            .WithLifetime(TimeSpan.FromHours(1))
            .Build();

        var timeProvider = new FakeTimeProvider(session.ExpiresAt);

        // Act
        var isValid = session.IsValid(timeProvider);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_OneSecondBeforeExpiration_ReturnsTrue()
    {
        // Arrange
        var session = new SessionBuilder()
            .WithLifetime(TimeSpan.FromHours(1))
            .Build();

        var timeProvider = new FakeTimeProvider(session.ExpiresAt.AddSeconds(-1));

        // Act
        var isValid = session.IsValid(timeProvider);

        // Assert
        Assert.True(isValid);
    }
    [Fact]
    public void IsExpired_WithValidSession_ReturnsFalse()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var session = new SessionBuilder()
            .ExpiresInDays(30)
            .Build();

        // Act
        var isExpired = session.IsExpired(timeProvider);

        // Assert
        Assert.False(isExpired);
    }

    [Fact]
    public void IsExpired_WithExpiredSession_ReturnsTrue()
    {
        // Arrange
        var session = new SessionBuilder()
            .Expired()
            .Build();

        // Use time provider set to future (session is already expired)
        var timeProvider = new FakeTimeProvider(DateTime.UtcNow.AddDays(1));

        // Act
        var isExpired = session.IsExpired(timeProvider);

        // Assert
        Assert.True(isExpired);
    }

    [Fact]
    public void IsExpired_ExactlyAtExpirationTime_ReturnsTrue()
    {
        // Arrange
        var session = new SessionBuilder()
            .WithLifetime(TimeSpan.FromHours(1))
            .Build();

        var timeProvider = new FakeTimeProvider(session.ExpiresAt);

        // Act
        var isExpired = session.IsExpired(timeProvider);

        // Assert
        Assert.True(isExpired);
    }
    [Fact]
    public void IsRevoked_WithActiveSession_ReturnsFalse()
    {
        // Arrange
        var session = new SessionBuilder().Build();

        // Act
        var isRevoked = session.IsRevoked();

        // Assert
        Assert.False(isRevoked);
    }

    [Fact]
    public void IsRevoked_WithRevokedSession_ReturnsTrue()
    {
        // Arrange
        var session = new SessionBuilder()
            .Revoked()
            .Build();

        // Act
        var isRevoked = session.IsRevoked();

        // Assert
        Assert.True(isRevoked);
    }
    [Fact]
    public void UpdateLastSeen_SetsTimestamp()
    {
        // Arrange
        var session = new SessionBuilder().Build();
        var beforeUpdate = DateTime.UtcNow;

        // Act
        session.UpdateLastSeen();

        // Assert
        Assert.NotNull(session.LastSeenAt);
        Assert.True(session.LastSeenAt >= beforeUpdate);
        Assert.True(session.LastSeenAt <= DateTime.UtcNow);
    }

    [Fact]
    public void UpdateLastSeen_CalledMultipleTimes_UpdatesTimestamp()
    {
        // Arrange
        var session = new SessionBuilder().Build();
        session.UpdateLastSeen();
        var firstUpdate = session.LastSeenAt;

        Thread.Sleep(10); // Small delay to ensure different timestamp

        // Act
        session.UpdateLastSeen();

        // Assert
        Assert.NotNull(session.LastSeenAt);
        Assert.True(session.LastSeenAt > firstUpdate);
    }
    [Fact]
    public void Revoke_WithActiveSession_RevokesSuccessfully()
    {
        // Arrange
        var session = new SessionBuilder().Build();
        var beforeRevoke = DateTime.UtcNow;

        // Act
        session.Revoke();

        // Assert
        Assert.NotNull(session.RevokedAt);
        Assert.True(session.RevokedAt >= beforeRevoke);
        Assert.True(session.RevokedAt <= DateTime.UtcNow);
        Assert.True(session.IsRevoked());
    }

    [Fact]
    public void Revoke_AlreadyRevoked_ThrowsDomainException()
    {
        // Arrange
        var session = new SessionBuilder()
            .Revoked()
            .Build();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => session.Revoke());
        Assert.Contains("already revoked", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Revoke_IsIdempotent_ThrowsOnSecondCall()
    {
        // Arrange
        var session = new SessionBuilder().Build();
        session.Revoke();
        var firstRevokedAt = session.RevokedAt;

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => session.Revoke());
        Assert.Equal(firstRevokedAt, session.RevokedAt); // Timestamp unchanged
    }
    [Fact]
    public void Extend_WithValidSession_ExtendsSuccessfully()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var session = new SessionBuilder().Build();
        var originalExpiresAt = session.ExpiresAt;
        var extension = TimeSpan.FromDays(7);

        // Act
        session.Extend(extension, timeProvider);

        // Assert
        Assert.Equal(originalExpiresAt.Add(extension), session.ExpiresAt);
        Assert.True(session.ExpiresAt > originalExpiresAt);
    }

    [Fact]
    public void Extend_RevokedSession_ThrowsDomainException()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var session = new SessionBuilder()
            .Revoked()
            .Build();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            session.Extend(TimeSpan.FromDays(7), timeProvider));
        Assert.Contains("revoked", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extend_ExpiredSession_ThrowsDomainException()
    {
        // Arrange
        var session = new SessionBuilder()
            .Expired()
            .Build();

        var timeProvider = new FakeTimeProvider(DateTime.UtcNow.AddDays(1));

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            session.Extend(TimeSpan.FromDays(7), timeProvider));
        Assert.Contains("expired", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extend_WithZeroDuration_ThrowsDomainException()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var session = new SessionBuilder().Build();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            session.Extend(TimeSpan.Zero, timeProvider));
        Assert.Contains("positive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extend_WithNegativeDuration_ThrowsDomainException()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var session = new SessionBuilder().Build();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            session.Extend(TimeSpan.FromDays(-1), timeProvider));
        Assert.Contains("positive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Builder_CreateDefault_ProducesValidSession()
    {
        // Act
        var session = SessionBuilder.CreateDefault();

        // Assert
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.NotNull(session.TokenHash);
        Assert.False(session.IsRevoked());
    }

    [Fact]
    public void Builder_CreateExpired_ProducesExpiredSession()
    {
        // Act
        var session = SessionBuilder.CreateExpired();

        // Use time provider set to future (session is already expired)
        var timeProvider = new FakeTimeProvider(DateTime.UtcNow.AddDays(1));

        // Assert
        Assert.True(session.IsExpired(timeProvider));
    }
}
