using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.Queries.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries.Invitation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ValidateInvitationTokenQueryHandlerTests
{
    private readonly Mock<IInvitationTokenRepository> _invitationRepoMock = new();
    private readonly ValidateInvitationTokenQueryHandler _handler;

    public ValidateInvitationTokenQueryHandlerTests()
    {
        _handler = new ValidateInvitationTokenQueryHandler(_invitationRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ValidToken_ReturnsValidResponse()
    {
        // Arrange
        var rawToken = "test-raw-token-123";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var invitation = InvitationToken.Create("test@example.com", "User", tokenHash, Guid.NewGuid());

        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var query = new ValidateInvitationTokenQuery(rawToken);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Valid.Should().BeTrue();
        result.Role.Should().Be("User");
        result.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_TokenNotFound_ReturnsInvalid()
    {
        // Arrange
        _invitationRepoMock
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        var query = new ValidateInvitationTokenQuery("nonexistent-token");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Valid.Should().BeFalse();
        result.Role.Should().BeNull();
        result.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void Handle_DoesNotReturnEmail()
    {
        // Verify that ValidateInvitationTokenResponse does NOT have an Email property
        // This is a security requirement: the token validation endpoint should not expose
        // the email address associated with the invitation.
        var responseType = typeof(ValidateInvitationTokenResponse);
        var emailProperty = responseType.GetProperty("Email", BindingFlags.Public | BindingFlags.Instance);
        emailProperty.Should().BeNull("ValidateInvitationTokenResponse should NOT expose Email for security reasons");
    }
}
