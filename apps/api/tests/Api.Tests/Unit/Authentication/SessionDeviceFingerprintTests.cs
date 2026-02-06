using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Xunit;

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
        Assert.NotNull(session.DeviceFingerprint);
        Assert.NotEmpty(session.DeviceFingerprint);
        Assert.Equal(44, session.DeviceFingerprint.Length); // Base64 SHA256 (32 bytes = 44 chars)
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
        Assert.Null(session.DeviceFingerprint);
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
        Assert.Equal(session1.DeviceFingerprint, session2.DeviceFingerprint);
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
        Assert.NotEqual(session1.DeviceFingerprint, session2.DeviceFingerprint);
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
        Assert.Equal(session1.DeviceFingerprint, session2.DeviceFingerprint); // Normalized to lowercase
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
        Assert.Equal(session1.DeviceFingerprint, session2.DeviceFingerprint); // Trimmed before hash
    }
}
