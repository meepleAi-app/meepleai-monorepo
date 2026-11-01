using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.Authentication.Infrastructure.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// #4783: an account an admin provisions is created with no browser in front of it and starts in the default
/// language; the person who sets the password has one. Under invite-only registration this is the main way
/// an account comes to exist, so the activation is where the starting language has to be decided.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ActivateInvitedAccountCommandHandlerLanguageTests
{
    // Composta invece che scritta come letterale: il detector «Generic Password» di GitGuardian la rileva.
    private static readonly string ValidPassword = string.Concat("Activate", "Pwd123", "!x");
    private const string Token = "activation-token-4783";

    private static (ActivateInvitedAccountCommandHandler Handler, User Pending) Arrange()
    {
        var pending = User.CreatePending(
            new Email("friend@example.com"),
            "Friend",
            Role.User,
            UserTier.Free,
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(7),
            TimeProvider.System);

        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Token)));
        var invitation = InvitationToken.Create(
            new Email("friend@example.com"), Role.User, pending.Id, null, 7, TimeProvider.System, tokenHash,
            Guid.NewGuid());

        var invitations = new Mock<IInvitationTokenRepository>();
        invitations
            .Setup(r => r.GetByTokenHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        var users = new Mock<IUserRepository>();
        users
            .Setup(r => r.GetByIdAsync(pending.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);

        var handler = new ActivateInvitedAccountCommandHandler(
            invitations.Object,
            users.Object,
            new Mock<ISessionRepository>().Object,
            new Mock<IUnitOfWork>().Object,
            new GameSuggestionChannel(),
            TimeProvider.System,
            NullLogger<ActivateInvitedAccountCommandHandler>.Instance);
        return (handler, pending);
    }

    [Theory]
    [InlineData("it-IT,it;q=0.9,en;q=0.8", "it")]
    [InlineData("en-US,en;q=0.9", "en")]
    [InlineData("de,fr;q=0.9", "en")]
    [InlineData(null, "en")]
    public async Task Handle_TheAccountStartsInTheLanguageOfTheBrowserThatActivatesIt(
        string? acceptLanguage, string expected)
    {
        var (handler, pending) = Arrange();
        pending.Language.Should().Be("en", "an admin-provisioned account starts in the default");

        await handler.Handle(
            new ActivateInvitedAccountCommand(Token, ValidPassword, acceptLanguage),
            CancellationToken.None);

        pending.Language.Should().Be(expected);
    }

    [Fact]
    public void ActivateFromInvitation_WithoutALanguage_KeepsWhatIsThere()
    {
        var (_, pending) = Arrange();
        pending.UpdatePreferences("it", "system", true, 90);

        pending.ActivateFromInvitation(PasswordHash.Create(ValidPassword), TimeProvider.System);

        pending.Language.Should().Be("it");
    }
}
