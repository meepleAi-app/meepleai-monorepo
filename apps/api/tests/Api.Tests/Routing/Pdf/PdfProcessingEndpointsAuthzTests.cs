using Api.Infrastructure.Entities;
using Api.Routing;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Routing.Pdf;

/// <summary>
/// Issue #4084 — <c>PdfProcessingEndpoints.IsAdminEditorOrSuperAdmin</c> must accept
/// <c>SuperAdmin</c>, not just <c>Admin</c>/<c>Editor</c>.
///
/// <para>Measured before this fix: <c>POST /api/v1/ingest/pdf/{pdfId}/rulespec</c> gave a
/// real <b>403</b> for <c>badsworm@gmail.com</c> (role <c>superadmin</c>), and <b>200</b>
/// for an account whose role is the literal string <c>admin</c>. Three handlers in
/// <c>PdfProcessingEndpoints.cs</c> — generate rulespec, index, extract text — compared the
/// session role against the string literals <c>"Admin"</c>/<c>"Editor"</c> only, which is
/// exactly the anti-pattern #3994 already corrected in <c>AgentTierLimits</c>.</para>
///
/// <para>This targets the extracted predicate directly rather than the three HTTP handlers:
/// all three now delegate to it unchanged otherwise (same <c>TryGetActiveSession</c> wiring,
/// same 403 branch), so a unit test on the shared predicate covers the authorization
/// decision for all three without a Testcontainers-backed HTTP round trip.</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4084")]
public sealed class PdfProcessingEndpointsAuthzTests
{
    [Theory]
    [InlineData("Admin")]
    [InlineData("Editor")]
    [InlineData("SuperAdmin")]
    [InlineData("admin")] // OrdinalIgnoreCase, matches the literal-role string the DB stores
    [InlineData("superadmin")]
    public void IsAdminEditorOrSuperAdmin_AcceptsPrivilegedRoles(string role)
    {
        PdfProcessingEndpoints.IsAdminEditorOrSuperAdmin(role).Should().BeTrue();
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Creator")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("NotARole")]
    public void IsAdminEditorOrSuperAdmin_RejectsEverythingElse(string? role)
    {
        PdfProcessingEndpoints.IsAdminEditorOrSuperAdmin(role).Should().BeFalse();
    }

    [Fact]
    public void IsAdminEditorOrSuperAdmin_MatchesEveryUserRoleEnumValue_ExceptUser()
    {
        // #4084: enumerate the real enum instead of hand-picked strings, so a future role
        // added to UserRole (e.g. #3690 added SuperAdmin=3) is forced through this test
        // rather than silently falling through to "rejected" like SuperAdmin did here.
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var expected = role is UserRole.Admin or UserRole.Editor or UserRole.SuperAdmin;
            PdfProcessingEndpoints.IsAdminEditorOrSuperAdmin(role.ToString()).Should().Be(
                expected,
                because: $"UserRole.{role} {(expected ? "must" : "must not")} pass the rulespec/index/extract gate");
        }
    }
}
