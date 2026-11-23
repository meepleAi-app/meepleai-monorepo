using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #5116 — a revoked badge keeps its row, and the unique (UserId, BadgeId) index refuses a second one with 23505.
/// <c>UserBadgeGrant.GrantAsync</c> reinstates that row instead of awarding a new one. A caller that awards with
/// <c>UserBadge.Award</c> and <c>AddAsync</c> by itself, as both callers did before #5116, works until the user lost the
/// badge once, then fails the whole SaveChanges; in <c>BadgeEvaluationOnApprovalHandler</c> that failure is swallowed
/// together with every other badge of the same approval.
/// </summary>
/// <remarks>
/// <b>Text scan.</b> It looks for the call <c>UserBadge.Award(</c> under <c>BoundedContexts</c>, outside the grant. A call
/// through an alias or a <c>using static</c> is not seen.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "5116")]
public sealed class UserBadgeAwardGoesThroughGrantArchitectureTests
{
    private const string Grant = "SharedGameCatalog/Application/Services/UserBadgeGrant.cs";

    private static readonly Regex AwardCall = new(@"\bUserBadge\.Award\(", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    [Fact]
    public void BadgesAreAwardedOnlyThroughUserBadgeGrant()
    {
        var contexts = Path.Combine(LocateApiSrc(), "BoundedContexts");

        var callers = Directory.EnumerateFiles(contexts, "*.cs", SearchOption.AllDirectories)
            .Where(path => AwardCall.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(contexts, path).Replace('\\', '/'))
            .ToList();

        callers.Should().Equal(
            [Grant],
            "an award next to a revoked row is a 23505: give a badge with UserBadgeGrant.GrantAsync, which reinstates it");
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
