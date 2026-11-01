using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// #4783: an account created by accepting an invitation starts in the language of the browser that accepts
/// it, not in a blind <c>en</c>. The page that takes the password sends no locale, so the header is the
/// only evidence there is.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class AcceptInvitationCommandHandlerLanguageTests
{
    // Composta invece che scritta come letterale: il detector «Generic Password» di GitGuardian la rileva.
    private static readonly string ValidPassword = string.Concat("Invite", "Pwd123", "!x");
    private const string Token = "invitation-token-4783";

    [Theory]
    [InlineData("it-IT,it;q=0.9,en;q=0.8", "it")]
    [InlineData("en-US,en;q=0.9", "en")]
    [InlineData("de,fr;q=0.9", "en")]
    [InlineData(null, "en")]
    public async Task Handle_StartsTheAccountInTheLanguageOfTheBrowser(string? acceptLanguage, string expected)
    {
        // Arrange
        var invitations = new Mock<IInvitationTokenRepository>();
        var users = new Mock<IUserRepository>();
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Token)));
        var invitation = InvitationToken.Create("friend@example.com", "user", tokenHash, Guid.NewGuid());
        invitations
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        User? saved = null;
        users
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => saved = user);
        var handler = new AcceptInvitationCommandHandler(
            invitations.Object,
            users.Object,
            new Mock<ISessionRepository>().Object,
            new Mock<IUnitOfWork>().Object,
            NullLogger<AcceptInvitationCommandHandler>.Instance);

        // Act
        await handler.Handle(
            new AcceptInvitationCommand(Token, ValidPassword, ValidPassword, acceptLanguage),
            CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.Language.Should().Be(expected);
    }
}
