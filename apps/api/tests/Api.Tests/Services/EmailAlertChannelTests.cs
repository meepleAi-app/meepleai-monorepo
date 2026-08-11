using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="EmailAlertChannel"/>.
/// Validates that health/monitoring alerts are delivered through the shared
/// <see cref="IEmailService"/>.SendRawEmailAsync transport (authenticated SMTP_* creds),
/// NOT the channel's former self-configured SmtpClient — which on staging sent
/// unauthenticated (empty Alerting:Email:Username/Password) → Gmail rejected with
/// "5.7.0 Authentication Required".
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EmailAlertChannelTests
{
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ILogger<EmailAlertChannel>> _logger = new();

    private EmailAlertChannel CreateChannel(EmailConfiguration emailConfig)
    {
        var alertingConfig = new AlertingConfiguration { Email = emailConfig };
        return new EmailAlertChannel(
            Options.Create(alertingConfig),
            _emailService.Object,
            _logger.Object);
    }

    private static EmailConfiguration EnabledConfig(params string[] recipients) => new()
    {
        Enabled = true,
        To = recipients.ToList()
    };

    [Fact]
    public async Task SendAsync_SendsOncePerRecipient_WithSeverityPrefixedSubjectAndHtmlBody_ReturnsTrue()
    {
        // Arrange
        var channel = CreateChannel(EnabledConfig("ops1@example.com", "ops2@example.com"));

        // Act
        var result = await channel.SendAsync("health.postgres", "critical", "DB down");

        // Assert
        result.Should().BeTrue();

        const string expectedSubject = "🚨 [CRITICAL] health.postgres - MeepleAI";
        foreach (var recipient in new[] { "ops1@example.com", "ops2@example.com" })
        {
            _emailService.Verify(s => s.SendRawEmailAsync(
                recipient,
                expectedSubject,
                It.Is<string>(body => body.Contains("DB down") && body.Contains("CRITICAL ALERT")),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        _emailService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_WhenSuppressEmailMetadataSet_SkipsTransport_ReturnsFalse()
    {
        // Arrange — a Degraded (warning) health alert tagged for email suppression.
        // Non-critical health warnings must NOT email (they still reach Slack/DB); only
        // Unhealthy (critical) transitions email. This kills the flapping warning-mail
        // spam from bggapi/reranker/embedding/shared-catalog-fts.
        var channel = CreateChannel(EnabledConfig("ops@example.com"));
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["_suppress_email"] = true
        };

        // Act
        var result = await channel.SendAsync("health.bggapi", "warning", "BGG degraded", metadata);

        // Assert — not emailed, but not treated as a transport failure either
        result.Should().BeFalse();
        _emailService.Verify(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WhenSuppressEmailMetadataAbsent_StillSends()
    {
        // Arrange — a critical (Unhealthy) health alert carries no suppression flag
        var channel = CreateChannel(EnabledConfig("ops@example.com"));
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["service"] = "bggapi"
        };

        // Act
        var result = await channel.SendAsync("health.bggapi", "critical", "BGG down", metadata);

        // Assert — normal delivery is unaffected by the suppression path
        result.Should().BeTrue();
        _emailService.Verify(s => s.SendRawEmailAsync(
            "ops@example.com",
            It.Is<string>(sub => sub.Contains("CRITICAL", StringComparison.Ordinal)),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenTransportThrows_ReturnsFalse_AndDoesNotPropagate()
    {
        // Arrange
        var channel = CreateChannel(EnabledConfig("ops@example.com"));
        _emailService
            .Setup(s => s.SendRawEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP auth failed"));

        // Act — resilience contract (IAlertChannel): a transport failure must NOT
        // propagate (would abort sibling channels), it must be swallowed → false.
        var result = await channel.SendAsync("health.redis", "warning", "Redis degraded");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_WhenDisabled_ReturnsFalse_WithoutCallingTransport()
    {
        // Arrange
        var channel = CreateChannel(new EmailConfiguration
        {
            Enabled = false,
            To = { "ops@example.com" }
        });

        // Act
        var result = await channel.SendAsync("health.test", "info", "test message");

        // Assert
        result.Should().BeFalse();
        _emailService.Verify(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WhenNoRecipients_ReturnsFalse_WithoutCallingTransport()
    {
        // Arrange — Enabled but empty To list
        var channel = CreateChannel(new EmailConfiguration { Enabled = true });

        // Act
        var result = await channel.SendAsync("health.test", "info", "test message");

        // Assert
        result.Should().BeFalse();
        _emailService.Verify(s => s.SendRawEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
