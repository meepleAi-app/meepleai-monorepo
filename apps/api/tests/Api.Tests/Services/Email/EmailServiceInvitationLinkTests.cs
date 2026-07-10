using Api.Services;
using Api.Services.Email;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// Issue #1629: covers the refactor of EmailService.SendInvitationEmailAsync to delegate
/// raw transmission to IEmailSender (instead of constructing SmtpClient directly) AND the
/// collateral bug fix on the redeem link path (/accept-invite → /invites/{token}).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EmailServiceInvitationLinkTests
{
    private const string FrontendBaseUrl = "https://app.test.meepleai.example";

    private readonly Mock<ILogger<EmailService>> _logger = new();
    private readonly Mock<IEmailSender> _sender = new();
    private readonly IConfiguration _configuration;

    public EmailServiceInvitationLinkTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FromAddress"] = "noreply@meepleai.app",
                ["Email:FromName"] = "MeepleAI",
                ["Frontend:BaseUrl"] = FrontendBaseUrl,
                ["Email:EnableSsl"] = "false",
            })
            .Build();
    }

    [Fact]
    public async Task SendInvitationEmailAsync_DelegatesToInjectedSender()
    {
        // Arrange
        EmailRequest? captured = null;
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<EmailRequest, CancellationToken>((req, _) => captured = req)
            .Returns(Task.CompletedTask);

        var service = new EmailService(_configuration, _logger.Object, _sender.Object);

        // Act
        await service.SendInvitationEmailAsync(
            toEmail: "recipient@example.com",
            role: "User",
            token: "raw-token-value",
            invitedByName: "Admin",
            ct: CancellationToken.None);

        // Assert
        _sender.Verify(
            s => s.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "EmailService MUST delegate raw transport to IEmailSender when one is injected.");

        captured.Should().NotBeNull();
        captured!.ToEmail.Should().Be("recipient@example.com");
        captured.FromEmail.Should().Be("noreply@meepleai.app");
        captured.FromName.Should().Be("MeepleAI");
        captured.Subject.Should().Contain("invited");
    }

    [Fact]
    public async Task SendInvitationEmailAsync_RedeemLinkUsesAcceptInvitePath()
    {
        // Arrange — the base-invitation redeem flow lives at /accept-invite?token= →
        // POST /auth/accept-invitation (AcceptInvitationCommandHandler creates the user from
        // token+password; no pending user needed). Issue #1629 wrongly repointed this link at
        // /invites/{token}, which is the game-night RSVP landing (game_night_invitations) and
        // returns "Invito non trovato" for invitation tokens. The base flow has no pending user,
        // so /setup-account (activate-account) is NOT its redeem either — /accept-invite is.
        EmailRequest? captured = null;
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<EmailRequest, CancellationToken>((req, _) => captured = req)
            .Returns(Task.CompletedTask);

        var service = new EmailService(_configuration, _logger.Object, _sender.Object);

        // Act
        await service.SendInvitationEmailAsync(
            toEmail: "recipient@example.com",
            role: "User",
            token: "abc123",
            invitedByName: "Admin",
            ct: CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.HtmlBody.Should().Contain(
            $"{FrontendBaseUrl}/accept-invite?token=abc123",
            "the redeem URL must point to /accept-invite?token= (apps/web/src/app/(public)/accept-invite) which calls POST /auth/accept-invitation.");
        captured.HtmlBody.Should().NotContain(
            "/invites/abc123",
            "/invites/{token} is the game-night RSVP landing, not the user-invitation redeem — it 404s for invitation tokens.");
    }

    [Fact]
    public async Task SendInvitationEmailAsync_WrapsSenderFailureInInvalidOperationException()
    {
        // Arrange
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transport down"));

        var service = new EmailService(_configuration, _logger.Object, _sender.Object);

        // Act
        var act = () => service.SendInvitationEmailAsync(
            toEmail: "recipient@example.com",
            role: "User",
            token: "tok",
            invitedByName: "Admin",
            ct: CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to send invitation email");
    }
}
