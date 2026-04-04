using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Unit.Authentication;

/// <summary>
/// Unit tests for Session device fingerprint generation.
/// Issue #3677: Device tracking and limiting.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public sealed class SessionDeviceFingerprintTests
{
    [Fact]
    public void Session_WithUserAgent_GeneratesFingerprint()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0";
        var token = SessionToken.Generate();

        // Act
        var session = new Session(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            token: token,
            userAgent: userAgent
        );

        // Assert
        session.DeviceFingerprint.Should().NotBeNull();
        session.DeviceFingerprint.Should().NotBeEmpty();
        session.DeviceFingerprint.Length.Should().Be(44); // Base64 SHA256 (32 bytes = 44 chars)
    }

    [Fact]
    public void Session_WithoutUserAgent_HasNullFingerprint()
    {
        // Arrange
        var token = SessionToken.Generate();

        // Act
        var session = new Session(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            token: token,
            userAgent: null
        );

        // Assert
        session.DeviceFingerprint.Should().BeNull();
    }

    [Fact]
    public void Session_SameUserAgent_GeneratesSameFingerprint()
    {
        // Arrange
        var userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Safari/605.1";
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();

        // Act
        var session1 = new Session(Guid.NewGuid(), Guid.NewGuid(), token1, userAgent: userAgent);
        var session2 = new Session(Guid.NewGuid(), Guid.NewGuid(), token2, userAgent: userAgent);

        // Assert
        session2.DeviceFingerprint.Should().Be(session1.DeviceFingerprint);
    }

    [Fact]
    public void Session_DifferentUserAgent_GeneratesDifferentFingerprint()
    {
        // Arrange
        var userAgent1 = "Mozilla/5.0 (Windows NT 10.0) Chrome/120.0";
        var userAgent2 = "Mozilla/5.0 (Macintosh) Safari/605.1";
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();

        // Act
        var session1 = new Session(Guid.NewGuid(), Guid.NewGuid(), token1, userAgent: userAgent1);
        var session2 = new Session(Guid.NewGuid(), Guid.NewGuid(), token2, userAgent: userAgent2);

        // Assert
        session2.DeviceFingerprint.Should().NotBe(session1.DeviceFingerprint);
    }

    [Fact]
    public void Session_CaseInsensitiveUserAgent_GeneratesSameFingerprint()
    {
        // Arrange
        var userAgent1 = "Mozilla/5.0 Chrome";
        var userAgent2 = "MOZILLA/5.0 CHROME";
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();

        // Act
        var session1 = new Session(Guid.NewGuid(), Guid.NewGuid(), token1, userAgent: userAgent1);
        var session2 = new Session(Guid.NewGuid(), Guid.NewGuid(), token2, userAgent: userAgent2);

        // Assert
        session2.DeviceFingerprint.Should().Be(session1.DeviceFingerprint); // Normalized to lowercase
    }

    [Fact]
    public void Session_WhitespaceInUserAgent_NormalizedBeforeHash()
    {
        // Arrange
        var userAgent1 = "  Mozilla/5.0 Chrome  ";
        var userAgent2 = "Mozilla/5.0 Chrome";
        var token1 = SessionToken.Generate();
        var token2 = SessionToken.Generate();

        // Act
        var session1 = new Session(Guid.NewGuid(), Guid.NewGuid(), token1, userAgent: userAgent1);
        var session2 = new Session(Guid.NewGuid(), Guid.NewGuid(), token2, userAgent: userAgent2);

        // Assert
        session2.DeviceFingerprint.Should().Be(session1.DeviceFingerprint); // Trimmed before hash
    }
}
