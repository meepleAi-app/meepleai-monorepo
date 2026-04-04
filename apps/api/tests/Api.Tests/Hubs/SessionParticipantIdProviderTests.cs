using System.Security.Claims;
using Api.Hubs;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Hubs;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SessionParticipantIdProviderTests
{
    [Fact]
    public void GetUserId_WithJwtNameIdentifierClaim_ReturnsUserId()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

        // Act
        var result = SessionParticipantIdProvider.ResolveUserId(principal, sessionToken: null);

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetUserId_WithSubClaim_ReturnsUserId()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var claims = new[] { new Claim("sub", userId) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

        // Act
        var result = SessionParticipantIdProvider.ResolveUserId(principal, sessionToken: null);

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetUserId_WithSessionToken_ReturnsGuestToken()
    {
        // Arrange
        var token = "ABC123";
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // no claims

        // Act
        var result = SessionParticipantIdProvider.ResolveUserId(principal, sessionToken: token);

        // Assert
        result.Should().Be("guest:ABC123");
    }

    [Fact]
    public void GetUserId_WithNoClaims_AndNoToken_ReturnsNull()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = SessionParticipantIdProvider.ResolveUserId(principal, sessionToken: null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetUserId_WithNullPrincipal_AndNoToken_ReturnsNull()
    {
        // Act
        var result = SessionParticipantIdProvider.ResolveUserId(user: null, sessionToken: null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetUserId_JwtClaimTakesPriorityOverSessionToken()
    {
        // Arrange — both JWT and session token present
        var userId = Guid.NewGuid().ToString();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

        // Act
        var result = SessionParticipantIdProvider.ResolveUserId(principal, sessionToken: "guest-token-123");

        // Assert — JWT claim wins
        result.Should().Be(userId);
    }

    [Fact]
    public void GetUserId_NameIdentifierTakesPriorityOverSub()
    {
        // Arrange — both NameIdentifier and sub claims present
        var nameId = "name-id-user";
        var subId = "sub-user";
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, nameId),
            new Claim("sub", subId)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

        // Act
        var result = SessionParticipantIdProvider.ResolveUserId(principal, sessionToken: null);

        // Assert — NameIdentifier wins
        result.Should().Be(nameId);
    }

    [Fact]
    public void GetUserId_EmptySessionToken_ReturnsNull()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var result = SessionParticipantIdProvider.ResolveUserId(principal, sessionToken: "");

        // Assert
        result.Should().BeNull();
    }
}
