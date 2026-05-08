using Api.BoundedContexts.Authentication.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Services;

/// <summary>
/// Unit tests for StagingAccessGuard (wave 1 email allowlist).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class StagingAccessGuardTests
{
    private static IStagingAccessGuard CreateGuard(string? allowedEmails)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "STAGING_ALLOWED_EMAILS", allowedEmails }
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
        return new StagingAccessGuard(config);
    }

    [Fact]
    public void IsEmailAllowed_WhenAllowlistEmpty_ReturnsTrue()
    {
        // Default safe: empty allowlist = open access (dev/prod scenarios)
        var guard = CreateGuard(null);
        guard.IsEmailAllowed("anyone@example.com").Should().BeTrue();
    }

    [Fact]
    public void IsEmailAllowed_WhenEmailInList_ReturnsTrue()
    {
        var guard = CreateGuard("badsworm@gmail.com,marco@example.com");
        guard.IsEmailAllowed("badsworm@gmail.com").Should().BeTrue();
        guard.IsEmailAllowed("marco@example.com").Should().BeTrue();
    }

    [Fact]
    public void IsEmailAllowed_WhenEmailNotInList_ReturnsFalse()
    {
        var guard = CreateGuard("badsworm@gmail.com");
        guard.IsEmailAllowed("hacker@evil.com").Should().BeFalse();
    }

    [Fact]
    public void IsEmailAllowed_CaseInsensitive_ReturnsTrue()
    {
        var guard = CreateGuard("Badsworm@Gmail.com");
        guard.IsEmailAllowed("badsworm@gmail.com").Should().BeTrue();
        guard.IsEmailAllowed("BADSWORM@GMAIL.COM").Should().BeTrue();
    }

    [Fact]
    public void IsEmailAllowed_WhitespaceTolerant_ReturnsTrue()
    {
        var guard = CreateGuard(" badsworm@gmail.com , marco@example.com ");
        guard.IsEmailAllowed("badsworm@gmail.com").Should().BeTrue();
        guard.IsEmailAllowed("marco@example.com").Should().BeTrue();
    }

    [Fact]
    public void IsEmailAllowed_WhenEmailNullOrEmpty_ReturnsFalse()
    {
        var guard = CreateGuard("badsworm@gmail.com");
        guard.IsEmailAllowed(null!).Should().BeFalse();
        guard.IsEmailAllowed("").Should().BeFalse();
        guard.IsEmailAllowed("   ").Should().BeFalse();
    }

    [Fact]
    public void HasNonEmptyAllowlist_WhenEmpty_ReturnsFalse()
    {
        var guard = CreateGuard(null);
        guard.HasNonEmptyAllowlist.Should().BeFalse();
    }

    [Fact]
    public void HasNonEmptyAllowlist_WhenWhitespaceOnly_ReturnsFalse()
    {
        var guard = CreateGuard("  ");
        guard.HasNonEmptyAllowlist.Should().BeFalse();
    }

    [Fact]
    public void HasNonEmptyAllowlist_WhenPopulated_ReturnsTrue()
    {
        var guard = CreateGuard("badsworm@gmail.com");
        guard.HasNonEmptyAllowlist.Should().BeTrue();
    }
}
