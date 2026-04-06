using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
public sealed class SlackSignatureValidatorTests
{
    private const string TestSigningSecret = "8f742231b10e8888abcd99baed85bc4ab719a2dv";

    private static SlackSignatureValidator CreateSut(DateTimeOffset now)
    {
        var config = Options.Create(new SlackNotificationConfiguration
        {
            SigningSecret = TestSigningSecret,
            ClientId = "client",
            ClientSecret = "secret",
            RedirectUri = "https://meepleai.app/slack/callback"
        });
        var timeProvider = new FakeTimeProvider(now);
        return new SlackSignatureValidator(config, timeProvider);
    }

    private static string ComputeSignature(string timestamp, string body)
    {
        var baseString = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(TestSigningSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        return "v0=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void Validate_WithValidSignatureAndFreshTimestamp_ReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var body = "payload=%7B%22type%22%3A%22block_actions%22%7D";
        var sig = ComputeSignature(timestamp, body);

        var sut = CreateSut(now);
        sut.Validate(timestamp, body, sig).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithExpiredTimestamp_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        // Timestamp 6 minutes old — exceeds 5-minute window
        var oldTimestamp = now.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        var body = "payload=test";
        var sig = ComputeSignature(oldTimestamp, body);

        var sut = CreateSut(now);
        sut.Validate(oldTimestamp, body, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithFutureTimestamp_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        // Timestamp 6 minutes in the future (replay prevention)
        var futureTimestamp = now.AddMinutes(6).ToUnixTimeSeconds().ToString();
        var body = "payload=test";
        var sig = ComputeSignature(futureTimestamp, body);

        var sut = CreateSut(now);
        sut.Validate(futureTimestamp, body, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithWrongSignature_ReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var timestamp = now.ToUnixTimeSeconds().ToString();

        var sut = CreateSut(now);
        sut.Validate(timestamp, "payload=test", "v0=000wrongsignature").Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyTimestamp_ReturnsFalse()
    {
        var sut = CreateSut(DateTimeOffset.UtcNow);
        sut.Validate("", "body", "v0=sig").Should().BeFalse();
    }
}
