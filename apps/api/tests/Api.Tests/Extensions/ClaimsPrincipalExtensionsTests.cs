using System.Security.Claims;
using Api.Extensions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Extensions;

/// <summary>
/// Issue #2845 / finding #HH: role claims are PascalCase ("SuperAdmin" / "Admin"
/// / "Editor"), so handlers that checked only <c>IsInRole("Admin")</c> denied a
/// superadmin the admin path. These helpers centralize the case-correct check.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWithRole(string? role)
    {
        var claims = role is null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.Role, role) };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("Admin", true)]
    [InlineData("Editor", false)]
    [InlineData("User", false)]
    [InlineData("Creator", false)]
    public void IsAdmin_ReturnsExpected(string role, bool expected)
    {
        PrincipalWithRole(role).IsAdmin().Should().Be(expected);
    }

    [Fact]
    public void IsAdmin_WithNoRoleClaim_ReturnsFalse()
    {
        PrincipalWithRole(null).IsAdmin().Should().BeFalse();
    }

    [Theory]
    [InlineData("SuperAdmin", true)]
    [InlineData("Admin", true)]
    [InlineData("Editor", true)]
    [InlineData("User", false)]
    [InlineData("Creator", false)]
    public void IsAdminOrEditor_ReturnsExpected(string role, bool expected)
    {
        PrincipalWithRole(role).IsAdminOrEditor().Should().Be(expected);
    }

    [Fact]
    public void IsAdminOrEditor_WithNoRoleClaim_ReturnsFalse()
    {
        PrincipalWithRole(null).IsAdminOrEditor().Should().BeFalse();
    }
}
