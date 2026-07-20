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
/// Issue #3250: EmailService.SendRawEmailAsync must delegate to the injected IEmailSender
/// (Resend/SMTP selected by EMAIL_PROVIDER) instead of instantiating a direct SmtpClient,
/// so alert (EmailAlertChannel) and queued (EmailProcessorJob) emails honor the configured
/// transport. Previously it always sent over SMTP, so on a Resend-only prod they failed
/// silently while templated emails succeeded via Resend.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class EmailServiceRawTransportTests
{
    private readonly Mock<ILogger<EmailService>> _logger = new();
    private readonly Mock<IEmailSender> _sender = new();
    private readonly IConfiguration _configuration;

    public EmailServiceRawTransportTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:FromAddress"] = "noreply@meepleai.app",
                ["Email:FromName"] = "MeepleAI",
                ["Email:EnableSsl"] = "false",
            })
            .Build();
    }

    [Fact]
    public async Task SendRawEmailAsync_DelegatesToInjectedSender()
    {
        // Arrange
        EmailRequest? captured = null;
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<EmailRequest, CancellationToken>((req, _) => captured = req)
            .Returns(Task.CompletedTask);

        var service = new EmailService(_configuration, _logger.Object, _sender.Object);

        // Act
        await service.SendRawEmailAsync(
            toEmail: "ops@meepleai.app",
            subject: "🚨 [CRITICAL] health.postgres - MeepleAI",
            htmlBody: "<p>alert</p>",
            ct: CancellationToken.None);

        // Assert
        _sender.Verify(
            s => s.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "SendRawEmailAsync MUST delegate to IEmailSender so it honors EMAIL_PROVIDER (Resend), not a hardcoded SmtpClient.");

        captured.Should().NotBeNull();
        captured!.ToEmail.Should().Be("ops@meepleai.app");
        captured.FromEmail.Should().Be("noreply@meepleai.app");
        captured.FromName.Should().Be("MeepleAI");
        captured.Subject.Should().Be("🚨 [CRITICAL] health.postgres - MeepleAI");
        captured.HtmlBody.Should().Be("<p>alert</p>");
    }

    [Fact]
    public async Task SendRawEmailAsync_WrapsSenderFailureInInvalidOperationException()
    {
        // Arrange
        _sender
            .Setup(s => s.SendAsync(It.IsAny<EmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("transport down"));

        var service = new EmailService(_configuration, _logger.Object, _sender.Object);

        // Act
        var act = () => service.SendRawEmailAsync(
            toEmail: "ops@meepleai.app",
            subject: "subj",
            htmlBody: "<p>body</p>",
            ct: CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Failed to send raw email");
    }
}
