using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4883: <c>GetSessionDetailsForSystemQuery</c> returns the same details as <c>GetSessionDetailsQuery</c>
/// (session code, participants, scores, shared notes) with no caller to authorize. It exists for the
/// game-night bridge, which reads the companion session for an organizer who need not sit in it. Its only
/// protection would otherwise be a comment, and a comment does not stop an endpoint from sending it and
/// handing every authenticated user what the checked query refuses them.
/// <para>
/// So the consumers are a closed list. Adding one means editing this test, which is where the
/// question "does this caller authorize the read itself?" gets asked.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4883")]
public sealed class SessionDetailsSystemQueryArchitectureTests
{
    private const string QueryName = "GetSessionDetailsForSystemQuery";

    private const string MarkerFile =
        "apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetSessionDetailsQuery.cs";

    /// <summary>
    /// Files allowed to mention the query, relative to <c>apps/api/src/Api/</c>. The first two define and
    /// handle it; the others send it after authorizing the flow themselves (the organizer going live, and
    /// the follow-up that completes the night for the organizer).
    /// </summary>
    private static readonly string[] AllowedFiles =
    [
        "BoundedContexts/SessionTracking/Application/Queries/GetSessionDetailsQuery.cs",
        "BoundedContexts/SessionTracking/Application/Queries/GetSessionDetailsQueryHandler.cs",
        "BoundedContexts/GameManagement/Application/Commands/GameNights/GoLiveSessionCommandHandler.cs",
        "BoundedContexts/GameManagement/Application/Commands/GameNights/CompleteNightLinkForLiveSessionCommand.cs",
    ];

    [Fact]
    public void SystemQuery_IsMentionedOnlyByItsDefinitionAndTheBridgeHandlers()
    {
        var apiRoot = LocateApiSourceRoot();

        var mentioning = Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains(QueryName, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(apiRoot, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        mentioning.Should().NotBeEmpty("a scan that finds nothing would be a scan that checked nothing");
        mentioning.Should().BeEquivalentTo(
            AllowedFiles,
            $"{QueryName} has no caller check: only the listed files may use it, each one authorizing the "
            + "flow itself. An endpoint must send GetSessionDetailsQuery, which checks the caller (#4883)");
    }

    [Fact]
    public void SystemQuery_IsNeverReachableFromAnEndpoint()
    {
        var routing = Path.Combine(LocateApiSourceRoot(), "Routing");
        Directory.Exists(routing).Should().BeTrue("the endpoints live under Routing/");

        var offenders = Directory.EnumerateFiles(routing, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains(QueryName, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(routing, f).Replace('\\', '/'))
            .ToList();

        offenders.Should().BeEmpty(
            $"an endpoint that sends {QueryName} would answer the session code to anyone authenticated");
    }

    /// <summary>
    /// Walks up to the API sources recognizing them by a known file, not by <c>.git</c>, which in a
    /// worktree is a file.
    /// </summary>
    private static string LocateApiSourceRoot()
    {
        var marker = MarkerFile.Replace('/', Path.DirectorySeparatorChar);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, marker)))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        return Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
    }
}
