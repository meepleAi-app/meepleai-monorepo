using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #5110 — a domain event reaches its handlers only through <c>IDomainEventCollector</c>, which a repository fills
/// with <c>RepositoryBase.CollectDomainEvents</c> before <c>SaveChangesAsync</c> reads it. A repository that saves an
/// aggregate raising events and never collects them leaves every handler of those events inert, and no other test
/// sees it: handler tests call the handler by hand, aggregate tests read <c>DomainEvents</c> in memory. Found by hand
/// three times: SharedGame (#4566), ShareRequest (#5078), UserBadge (#5110).
/// </summary>
/// <remarks>
/// <para>
/// <b>Text scan.</b> An aggregate "raises events" when a class declared in a file under <c>Domain/</c> calls
/// <c>AddDomainEvent(</c>; a repository is a class under <c>Infrastructure/</c> that derives from
/// <c>RepositoryBase</c> and has an <c>AddAsync</c>, <c>Update</c>, <c>UpdateAsync</c> or <c>DeleteAsync</c> taking that
/// aggregate as its first parameter. Such a repository must call <c>CollectDomainEvents</c> somewhere in its file.
/// </para>
/// <para>
/// <b>Limits, admitted.</b> The match is by simple type name, so two aggregates with the same name in two contexts
/// (<c>Session</c>) are checked against each other's repositories too, which only makes the rule stricter. A file that
/// collects for one aggregate passes for all the ones it saves. An aggregate saved by something that is not a
/// <c>RepositoryBase</c> (a handler writing to the <c>DbContext</c> by hand) is not seen.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "5110")]
public sealed class RepositoryCollectsDomainEventsArchitectureTests
{
    private static readonly Regex DeclaredType = new(
        @"\b(?:class|record)\s+(\w+)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    [Fact]
    public void EveryRepositoryThatSavesAnAggregateRaisingEvents_CollectsThem()
    {
        var contexts = Path.Combine(LocateApiSrc(), "BoundedContexts");
        var files = Directory.EnumerateFiles(contexts, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path: path.Replace('\\', '/'), Text: File.ReadAllText(path)))
            .ToList();

        var raisingEvents = files
            .Where(f => f.Path.Contains("/Domain/", StringComparison.Ordinal)
                        && f.Text.Contains("AddDomainEvent(", StringComparison.Ordinal))
            .SelectMany(f => DeclaredType.Matches(f.Text).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var repositories = files
            .Where(f => f.Path.Contains("/Infrastructure/", StringComparison.Ordinal)
                        && f.Text.Contains(": RepositoryBase", StringComparison.Ordinal))
            .ToList();

        var offenders = new List<string>();
        foreach (var aggregate in raisingEvents)
        {
            var saves = new Regex(
                $@"\b(?:AddAsync|Update|UpdateAsync|DeleteAsync)\s*\(\s*(?:[\w.]+\.)?{Regex.Escape(aggregate)}\s+\w+",
                RegexOptions.None, TimeSpan.FromSeconds(2));
            offenders.AddRange(repositories
                .Where(r => saves.IsMatch(r.Text) && !r.Text.Contains("CollectDomainEvents(", StringComparison.Ordinal))
                .Select(r => $"{aggregate}: {r.Path[(r.Path.IndexOf("BoundedContexts/", StringComparison.Ordinal) + 16)..]}"));
        }

        raisingEvents.Should().Contain(["ShareRequest", "UserBadge"], "the scan must see the aggregates it was written for");
        offenders.Should().BeEmpty(
            "a repository that saves an aggregate raising events must call CollectDomainEvents, or no handler runs");
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
