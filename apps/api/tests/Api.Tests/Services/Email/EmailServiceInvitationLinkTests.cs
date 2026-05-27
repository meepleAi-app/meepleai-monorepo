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
    public async Task SendInvitationEmailAsync_RedeemLinkUsesInvitesPath()
    {
        // Arrange — Issue #1629 collateral fix: link path was /accept-invite (query string token)
        // but the actual Next.js route is /invites/[token]. Without this fix the link in
        // delivered emails returned 404.
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
            $"{FrontendBaseUrl}/invites/abc123",
            "the redeem URL must point to the /invites/{token} path that exists in apps/web/src/app/(public)/invites/[token].");
        captured.HtmlBody.Should().NotContain(
            "/accept-invite?token=",
            "the legacy /accept-invite?token= URL is a 404 — must be removed in this PR.");
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
