using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class SlackDeepLinkValidatorTests
{
    [Theory]
    [InlineData("/notifications/123", "/notifications/123")]
    [InlineData("/share-requests/abc-def", "/share-requests/abc-def")]
    [InlineData("/settings/notifications", "/settings/notifications")]
    public void Validate_ValidAbsolutePath_ReturnsPath(string input, string expected)
    {
        SlackDeepLinkValidator.Validate(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrEmpty_ReturnsNull(string? input)
    {
        SlackDeepLinkValidator.Validate(input).Should().BeNull();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://evil.com/phishing")]
    [InlineData("http://meepleai.app/notifications")]
    [InlineData("//evil.com/path")]
    public void Validate_ContainsSchemeOrProtocol_ReturnsNull(string input)
    {
        SlackDeepLinkValidator.Validate(input).Should().BeNull();
    }

    [Theory]
    [InlineData("notifications/123")]
    [InlineData("relative/path")]
    [InlineData("../etc/passwd")]
    public void Validate_RelativePath_ReturnsNull(string input)
    {
        SlackDeepLinkValidator.Validate(input).Should().BeNull();
    }
}
