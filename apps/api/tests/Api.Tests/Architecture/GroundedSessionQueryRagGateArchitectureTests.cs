using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4751 — whoever sends <c>AskGroundedSessionQuery</c> starts a retrieval on the rulebook of a game, and that
/// retrieval is behind the RAG ownership gate (ADR-096): the copyright tier protects the ANSWER, never the
/// retrieval, and a manual that went into the prompt for someone who may not read it is not made safe by what
/// the model then writes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a source scan, and why it is shaped like this.</b>
/// <see cref="AnswerPathsSharePreconditionsArchitectureTests"/> and <see cref="RagQueryIdentityArchitectureTests"/>
/// look at who depends on <c>IRagAccessService</c>, and the handler of the Arbitro depends on
/// <c>IAnswerPreconditions</c>: it is in neither inventory, so removing its gate call would have broken no gate.
/// Sending the query is an instruction inside a method body, which reflection does not see, so this follows
/// <see cref="ConstantTimeComparisonArchitectureTests"/>: an inventory of the sites, and a scan that cannot
/// diverge from it without turning red.
/// </para>
/// <para>
/// A site either owns the gate — its file calls <c>EnsureCanAskAboutGameAsync</c> and depends on
/// <c>IAnswerPreconditions</c> — or says, in the inventory, why it does not. The second kind is a list of
/// gaps to close, not of reasons to be at peace with them.
/// </para>
/// <para>
/// What it does not see: that the call comes <i>before</i> the send, or that its denial is handled. That is for
/// the tests of the handler (<c>SubmitRuleDisputeCommandHandlerTests</c>: a denial never reaches the retrieval).
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4751")]
public sealed class GroundedSessionQueryRagGateArchitectureTests
{
    private const string Sender = "new AskGroundedSessionQuery(";
    private const string GateCall = "EnsureCanAskAboutGameAsync(";
    private const string SharedStep = "IAnswerPreconditions";

    /// <summary>
    /// Every file that builds the query, relative to <c>apps/api/src/Api</c>. A null value: the file owns the
    /// gate. A text: it does not, and this is why — to be closed, not kept.
    /// </summary>
    private static readonly Dictionary<string, string?> s_senders = new(StringComparer.Ordinal)
    {
        ["BoundedContexts/GameManagement/Application/Commands/GameNight/SubmitRuleDisputeCommand.cs"] = null,

        ["BoundedContexts/SessionTracking/Application/Commands/ChatCommandHandlers.cs"] =
            "NOT GATED: the chat of a session sends the query with the tier of the participant who writes and never " +
            "asks the ownership gate (no call to it in the file). It sits behind the feature flag " +
            "rag.live-image-retrieval, off by default. Inventoried so that it stays visible: it is a gap in ADR-096 " +
            "to close separately — whether the gate is checked upstream, by whoever opens the session, is not " +
            "something this test can see.",
    };

    [Fact]
    public void EverySenderOfTheGroundedSessionQuery_IsInTheInventory_AndTheInventoryHasNoGhosts()
    {
        var apiSrc = LocateApiSrc();
        var actual = SendersIn(apiSrc);
        var declared = s_senders.Keys.ToHashSet(StringComparer.Ordinal);

        actual.Except(declared).Should().BeEmpty(
            "a file that sends AskGroundedSessionQuery starts a retrieval on a game's rulebook and must check the " +
            "RAG ownership gate for whoever asked (IAnswerPreconditions.EnsureCanAskAboutGameAsync, ADR-096) — or be " +
            "added to the inventory with the reason it does not. Not in the inventory:\n" +
            string.Join('\n', actual.Except(declared)));
        declared.Except(actual).Should().BeEmpty(
            "the inventory names files that no longer send the query: update it instead of letting it drift");
    }

    [Fact]
    public void EverySenderThatOwnsTheGate_CallsItAndDependsOnTheSharedStep()
    {
        var apiSrc = LocateApiSrc();

        foreach (var (file, _) in s_senders.Where(s => s.Value is null))
        {
            var text = File.ReadAllText(PathOf(apiSrc, file));

            SourceScanner.FindCodeOccurrences(text, GateCall).Should().NotBeEmpty(
                $"{file} sends the grounded session query and is declared to own the RAG ownership gate, but it " +
                $"does not call {GateCall.TrimEnd('(')}: a retrieval on the rulebook would start for anyone");
            SourceScanner.FindCodeOccurrences(text, SharedStep).Should().NotBeEmpty(
                $"{file} must reach the gate through {SharedStep}, the single point the answering paths share, " +
                "and not through a copy of the check");
        }
    }

    [Fact]
    public void EverySenderWithoutTheGate_SaysWhy()
    {
        foreach (var (file, reason) in s_senders.Where(s => s.Value is not null))
        {
            reason.Should().NotBeNullOrWhiteSpace($"{file} is inventoried without the gate: write the reason");
            reason!.Length.Should().BeGreaterThan(
                40, $"the reason for {file} must say what is missing and where it is checked, not just name it");
        }
    }

    // -----------------------------------------------------------------------
    // Source scanning
    // -----------------------------------------------------------------------

    private static HashSet<string> SendersIn(string apiSrc)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(apiSrc, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(apiSrc, path).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (!text.Contains("AskGroundedSessionQuery", StringComparison.Ordinal))
            {
                continue;
            }

            if (SourceScanner.FindCodeOccurrences(text, Sender).Count > 0)
            {
                found.Add(relative);
            }
        }

        return found;
    }

    private static string PathOf(string apiSrc, string relativePath)
    {
        var path = Path.Combine(apiSrc, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue(
            $"the site {relativePath} is in the inventory and must exist: if it moved, update the inventory");
        return path;
    }

    private static string LocateApiSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // In a git worktree `.git` is a file, not a directory: accepting both stops the climb at the right root
        // instead of going on to the main checkout.
        while (dir is not null)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                break;
            }

            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}
