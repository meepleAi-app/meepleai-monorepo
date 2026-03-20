using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Api.Tests.Constants;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;

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
        session.Id.Should().Be(id);
        session.UserId.Should().Be(userId);
        session.TokenHash.Should().Be(token.ComputeHash());
        session.IpAddress.Should().NotBeNull();
        session.IpAddress.Should().Be(ipAddress);
        session.UserAgent.Should().Be(userAgent);
        (session.ExpiresAt > session.CreatedAt).Should().BeTrue();
        session.RevokedAt.Should().BeNull();
        session.LastSeenAt.Should().BeNull();
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
        session.ExpiresAt.Should().Be(expectedExpiration);
        Session.DefaultLifetime.Should().Be(TimeSpan.FromDays(30));
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
        actualLifetime.TotalSeconds.Should().BeApproximately(customLifetime.TotalSeconds, 0.1);
    }

    [Fact]
    public void Constructor_WithoutOptionalParameters_CreatesValidSession()
    {
        // Arrange & Act
        var session = new SessionBuilder().Build();

        // Assert
        session.Id.Should().NotBe(Guid.Empty);
        session.UserId.Should().NotBe(Guid.Empty);
        session.TokenHash.Should().NotBeNull();
        session.IpAddress.Should().BeNull();
        session.UserAgent.Should().BeNull();
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
        isValid.Should().BeTrue();
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
        isValid.Should().BeFalse();
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
        isValid.Should().BeFalse();
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
        isValid.Should().BeFalse();
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
        isValid.Should().BeTrue();
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
        isExpired.Should().BeFalse();
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
        isExpired.Should().BeTrue();
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
        isExpired.Should().BeTrue();
    }
    [Fact]
    public void IsRevoked_WithActiveSession_ReturnsFalse()
    {
        // Arrange
        var session = new SessionBuilder().Build();

        // Act
        var isRevoked = session.IsRevoked();

        // Assert
        isRevoked.Should().BeFalse();
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
        isRevoked.Should().BeTrue();
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
        session.LastSeenAt.Should().NotBeNull();
        (session.LastSeenAt >= beforeUpdate).Should().BeTrue();
        (session.LastSeenAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateLastSeen_CalledMultipleTimes_UpdatesTimestamp()
    {
        // Arrange
        var session = new SessionBuilder().Build();
        session.UpdateLastSeen();
        var firstUpdate = session.LastSeenAt;

        await Task.Delay(TestConstants.Timing.TinyDelay); // Ensure different timestamp

        // Act
        session.UpdateLastSeen();

        // Assert
        session.LastSeenAt.Should().NotBeNull();
        (session.LastSeenAt > firstUpdate).Should().BeTrue();
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
        session.RevokedAt.Should().NotBeNull();
        (session.RevokedAt >= beforeRevoke).Should().BeTrue();
        (session.RevokedAt <= DateTime.UtcNow).Should().BeTrue();
        session.IsRevoked().Should().BeTrue();
    }

    [Fact]
    public void Revoke_AlreadyRevoked_ThrowsDomainException()
    {
        // Arrange
        var session = new SessionBuilder()
            .Revoked()
            .Build();

        // Act & Assert
        var act = () => session.Revoke();
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().ContainEquivalentOf("already revoked");
    }

    [Fact]
    public void Revoke_IsIdempotent_ThrowsOnSecondCall()
    {
        // Arrange
        var session = new SessionBuilder().Build();
        session.Revoke();
        var firstRevokedAt = session.RevokedAt;

        // Act & Assert
        var act = () => session.Revoke();
        var exception = act.Should().Throw<DomainException>().Which;
        session.RevokedAt.Should().Be(firstRevokedAt); // Timestamp unchanged
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
        session.ExpiresAt.Should().Be(originalExpiresAt.Add(extension));
        (session.ExpiresAt > originalExpiresAt).Should().BeTrue();
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
        var act = () =>
            session.Extend(TimeSpan.FromDays(7), timeProvider);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().ContainEquivalentOf("revoked");
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
        var act = () =>
            session.Extend(TimeSpan.FromDays(7), timeProvider);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().ContainEquivalentOf("expired");
    }

    [Fact]
    public void Extend_WithZeroDuration_ThrowsDomainException()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var session = new SessionBuilder().Build();

        // Act & Assert
        var act = () =>
            session.Extend(TimeSpan.Zero, timeProvider);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().ContainEquivalentOf("positive");
    }

    [Fact]
    public void Extend_WithNegativeDuration_ThrowsDomainException()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var session = new SessionBuilder().Build();

        // Act & Assert
        var act = () =>
            session.Extend(TimeSpan.FromDays(-1), timeProvider);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().ContainEquivalentOf("positive");
    }
    [Fact]
    public void Builder_CreateDefault_ProducesValidSession()
    {
        // Act
        var session = SessionBuilder.CreateDefault();

        // Assert
        session.Id.Should().NotBe(Guid.Empty);
        session.TokenHash.Should().NotBeNull();
        session.IsRevoked().Should().BeFalse();
    }

    [Fact]
    public void Builder_CreateExpired_ProducesExpiredSession()
    {
        // Act
        var session = SessionBuilder.CreateExpired();

        // Use time provider set to future (session is already expired)
        var timeProvider = new FakeTimeProvider(DateTime.UtcNow.AddDays(1));

        // Assert
        session.IsExpired(timeProvider).Should().BeTrue();
    }
}
