using Api.Services.Email;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Resend;
using Xunit;

namespace Api.Tests.Services.Email;

/// <summary>
/// Issue #1629: unit coverage for ResendEmailSender. The key behaviour validated here is
/// the FROM override — Resend can only send from a verified domain, so the transport must
/// pin FROM to RESEND_FROM_EMAIL regardless of the caller's address (which may be a dev-only
/// value like noreply@meepleai.dev used for Mailpit). This bug was caught by the E2E test:
/// without the override Resend rejected with "domain meepleai.dev is not verified".
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ResendEmailSenderTests
{
    private readonly Mock<IResend> _resend = new();

    private static IConfiguration Config(string? resendFrom)
    {
        var dict = new Dictionary<string, string?>();
        if (resendFrom is not null)
        {
            dict["RESEND_FROM_EMAIL"] = resendFrom;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static EmailRequest Request(string fromEmail = "noreply@meepleai.dev") => new()
    {
        FromEmail = fromEmail,
        FromName = "MeepleAI",
        ToEmail = "recipient@example.com",
        Subject = "Hello",
        HtmlBody = "<p>body</p>",
    };

    [Fact]
    public async Task SendAsync_PinsFromToResendVerifiedDomain_NotCallerAddress()
    {
        // Arrange
        EmailMessage? captured = null;
        _resend
            .Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(new ResendResponse<Guid>(Guid.NewGuid(), new ResendRateLimit()));

        var sender = new ResendEmailSender(
            _resend.Object,
            Config(resendFrom: "noreply@meepleai.app"),
            Mock.Of<ILogger<ResendEmailSender>>());

        // Act — caller passes the dev .dev address; the transport must override it.
        await sender.SendAsync(Request(fromEmail: "noreply@meepleai.dev"), CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.From.Email.Should().Be("noreply@meepleai.app",
            "Resend transport must send from the verified domain, not the caller's dev address.");
        captured.From.Email.Should().NotContain("meepleai.dev",
            "the unverified .dev address must be overridden to avoid Resend ValidationError.");
    }

    [Fact]
    public async Task SendAsync_FallsBackToCallerFrom_WhenResendFromUnset()
    {
        // Arrange
        EmailMessage? captured = null;
        _resend
            .Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => captured = m)
            .ReturnsAsync(new ResendResponse<Guid>(Guid.NewGuid(), new ResendRateLimit()));

        var sender = new ResendEmailSender(
            _resend.Object,
            Config(resendFrom: null),
            Mock.Of<ILogger<ResendEmailSender>>());

        // Act
        await sender.SendAsync(Request(fromEmail: "caller@example.com"), CancellationToken.None);

        // Assert
        captured!.From.Email.Should().Be("caller@example.com");
    }

    [Fact]
    public async Task SendAsync_WrapsResendExceptionInInvalidOperationException()
    {
        // Arrange
        _resend
            .Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResendException(System.Net.HttpStatusCode.UnprocessableEntity, ErrorType.ValidationError, "domain not verified"));

        var sender = new ResendEmailSender(
            _resend.Object,
            Config(resendFrom: "noreply@meepleai.app"),
            Mock.Of<ILogger<ResendEmailSender>>());

        // Act
        var act = () => sender.SendAsync(Request(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Resend*");
    }
}
