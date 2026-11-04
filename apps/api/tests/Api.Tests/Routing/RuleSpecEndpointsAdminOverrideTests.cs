using Api.Routing;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Routing;

/// <summary>
/// Regression tests for the admin override on comment mutation (#3994).
/// <para>
/// The three call sites enumerated the role as a string literal, and were wrong in two ways at
/// once: all three excluded <c>superadmin</c>, and the DELETE site compared <c>"Admin"</c>
/// capitalised under <c>StringComparison.Ordinal</c> while the other two compared <c>"admin"</c>.
/// Since <c>Role.Value</c> is lowercase by construction, that branch was dead for all five roles —
/// so <c>DELETE /api/v1/comments/{commentId}</c> answered 403 even to an admin, from the day the
/// override was written.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "3994")]
public class RuleSpecEndpointsAdminOverrideTests
{
    /// <summary>
    /// The set the override must grant: editor, admin, superadmin — i.e.
    /// <c>HasPermission(Role.Admin)</c>. It is also what the frontend already promises:
    /// <c>CommentItem.tsx</c> renders the delete button for <c>owner || admin || superadmin</c>.
    /// </summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public void HasAdminOverride_PrivilegedRoles_Granted(string role)
    {
        RuleSpecEndpoints.HasAdminOverride(role).Should().BeTrue();
    }

    /// <summary>
    /// Editor passes the endpoint filter (<c>RequireAdminOrEditorSession</c>) but gets no override:
    /// it stays ownership-only, exactly as before this change. Creator and user never reach the
    /// handler — the filter answers 403 — and are listed here to pin the predicate anyway.
    /// </summary>
    [Theory]
    [InlineData("editor")]
    [InlineData("creator")]
    [InlineData("user")]
    public void HasAdminOverride_UnprivilegedRoles_NotGranted(string role)
    {
        RuleSpecEndpoints.HasAdminOverride(role).Should().BeFalse();
    }

    /// <summary>
    /// The defect that made the DELETE branch dead. <c>Role.Value</c> is lowercase by construction,
    /// but the predicate must not depend on the caller's casing — that dependency is what broke.
    /// </summary>
    [Theory]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    [InlineData("AdMiN")]
    [InlineData("SuperAdmin")]
    [InlineData("SUPERADMIN")]
    public void HasAdminOverride_IsCasingIndependent(string role)
    {
        RuleSpecEndpoints.HasAdminOverride(role).Should().BeTrue();
    }

    /// <summary>
    /// Fails closed: an absent or unknown role grants nothing. <c>Role.TryParse</c> returns false
    /// and the override is denied, rather than <c>Role.Parse</c> throwing a 500 out of an endpoint.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("administrator")]
    [InlineData("wizard")]
    public void HasAdminOverride_AbsentOrUnknownRole_FailsClosed(string? role)
    {
        RuleSpecEndpoints.HasAdminOverride(role).Should().BeFalse();
    }

    /// <summary>
    /// Ties the predicate to the hierarchy instead of to a list of literals: if
    /// <c>HasPermission(Role.Admin)</c> ever changes, this test moves with it rather than silently
    /// disagreeing. #3994 measured zero divergences between the repo's three hierarchy tables, so
    /// the hierarchy — not an enumeration — is the thing to depend on.
    /// </summary>
    [Theory]
    [InlineData("user")]
    [InlineData("creator")]
    [InlineData("editor")]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public void HasAdminOverride_AgreesWithHasPermissionAdmin(string role)
    {
        var expected = Role.Parse(role).HasPermission(Role.Admin);

        RuleSpecEndpoints.HasAdminOverride(role).Should().Be(expected);
    }

    /// <summary>
    /// The exact comparison the DELETE site used, kept as a test so the reason the branch was dead
    /// stays legible: <c>Role.Value</c> never matches a capitalised literal under Ordinal.
    /// </summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("superadmin")]
    [InlineData("editor")]
    [InlineData("creator")]
    [InlineData("user")]
    public void TheOldComparison_WasDeadForEveryRole(string role)
    {
        var value = Role.Parse(role).Value;

        string.Equals(value, "Admin", StringComparison.Ordinal).Should().BeFalse();
    }
}
