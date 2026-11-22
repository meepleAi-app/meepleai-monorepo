using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #5113, #5119 — the recipients of an alert were chosen with <c>.Where(u =&gt; u.Role == "admin")</c> in ten places.
/// That leaves out every superadmin, and where every administrator is one (the seeded admin is) it tells nobody: the
/// log said «No admin users found». The recipients come from a helper that names its roles and why
/// (<c>ShareRequestAlertRecipients</c>, <c>OperationalAlertRecipients</c>), not from a literal typed at each site.
/// </summary>
/// <remarks>
/// <b>Text scan, narrow on purpose.</b> It looks for a <c>Where</c> whose whole predicate is <c>x.Role == "admin"</c>,
/// the shape all ten sites had. A permission check (<c>user.Role == "admin"</c> in an <c>if</c>) is not a recipient
/// filter and is not matched; a filter written another way (<c>"admin" == u.Role</c>, <c>Equals</c>) is not seen.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "5119")]
public sealed class NoHandWrittenAdminRecipientFilterArchitectureTests
{
    private static readonly Regex AdminOnlyFilter = new(
        @"\.Where\(\s*\w+\s*=>\s*\w+\.Role\s*==\s*""admin""\s*\)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    [Fact]
    public void NoAlertChoosesItsRecipientsWithAnAdminOnlyLiteral()
    {
        var contexts = Path.Combine(LocateApiSrc(), "BoundedContexts");

        var offenders = Directory.EnumerateFiles(contexts, "*.cs", SearchOption.AllDirectories)
            .Where(path => AdminOnlyFilter.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(contexts, path).Replace('\\', '/'))
            .ToList();

        offenders.Should().BeEmpty(
            "a recipient filter on Role == \"admin\" leaves out the superadmins; use the helper of the alert's kind");
    }

    private static string LocateApiSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // In a git worktree `.git` is a file, not a directory: accepting both stops the climb at the right root.
        while (dir is not null)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                break;
            }

            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the tests run inside the repository");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}
