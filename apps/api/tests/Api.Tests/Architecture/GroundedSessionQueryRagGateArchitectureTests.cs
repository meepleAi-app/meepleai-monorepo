using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4751 — whoever uses <c>AskGroundedSessionQuery</c> starts a retrieval on the rulebook of a game, and that
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
/// Using the query is an instruction inside a method body, which reflection does not see, so this follows
/// <see cref="ConstantTimeComparisonArchitectureTests"/>: an inventory of the sites, and a scan that cannot
/// diverge from it without turning red.
/// </para>
/// <para>
/// <b>What it looks for.</b> Not the construction <c>new AskGroundedSessionQuery(</c>, which is bypassed by
/// anything that is not that exact text — a target-typed <c>new(...)</c> (the form the analyzers suggest), a
/// space or a line break after <c>new</c>, a <c>using</c> alias, a lambda, <c>Activator.CreateInstance</c>. It
/// looks for any source file that <i>names the type</i>, as a whole word: a file that names it can build it, and
/// one that does not name it can only get one from somewhere that does. The type's own two files (the
/// definition and its handler) are the only exceptions.
/// </para>
/// <para>
/// A site either owns the gate — its file calls <c>EnsureCanAskAboutGameAsync</c> and depends on
/// <c>IAnswerPreconditions</c> — or says, in the inventory, why it does not. The second kind is a list of gaps
/// to close, not of reasons to be at peace with them.
/// </para>
/// <para>
/// <b>Limits, admitted.</b> A comment is not a use, and is skipped (<see cref="SourceScanner"/>): a file that
/// names the type only in a comment is not seen — a cref in documentation costs no inventory entry. A string
/// literal <i>is</i> counted, so <c>Type.GetType("…AskGroundedSessionQuery")</c> is seen; a name assembled at
/// run time is not. A call site that receives a query from a factory in another file without naming the type
/// is not seen either; the factory is. And it cannot see that the gate call comes <i>before</i> the use, or
/// that its denial is handled: that is for the tests of the handler
/// (<c>SubmitRuleDisputeCommandHandlerTests</c>: a denial never reaches the retrieval).
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4751")]
public sealed class GroundedSessionQueryRagGateArchitectureTests
{
    private const string QueryName = "AskGroundedSessionQuery";
    private const string GateCall = "EnsureCanAskAboutGameAsync(";
    private const string SharedStep = "IAnswerPreconditions";

    /// <summary>The type's own files, relative to <c>apps/api/src/Api</c>: its definition and its handler.</summary>
    private static readonly HashSet<string> s_ownFiles = new(StringComparer.Ordinal)
    {
        "BoundedContexts/KnowledgeBase/Application/Queries/AskGroundedSessionQuery.cs",
        "BoundedContexts/KnowledgeBase/Application/Queries/AskGroundedSessionQueryHandler.cs",
    };

    /// <summary>
    /// Every other file that names the query, relative to <c>apps/api/src/Api</c>. A null value: the file owns the
    /// gate. A text: it does not, and this is why — to be closed, not kept.
    /// </summary>
    private static readonly Dictionary<string, string?> s_users = new(StringComparer.Ordinal)
    {
        ["BoundedContexts/GameManagement/Application/Commands/GameNight/SubmitRuleDisputeCommand.cs"] = null,

        ["BoundedContexts/SessionTracking/Application/Commands/ChatCommandHandlers.cs"] =
            "NOT GATED: the chat of a session sends the query with the tier of the participant who writes and never " +
            "asks the ownership gate (no call to it in the file). It sits behind the feature flag " +
            "rag.live-image-retrieval, off by default. Inventoried so that it stays visible: it is a gap in ADR-096 " +
            "to close separately — whether the gate is checked upstream, by whoever opens the session, is not " +
            "something this test can see.",
    };

    // -----------------------------------------------------------------------
    // The gate, on the real source
    // -----------------------------------------------------------------------

    [Fact]
    public void EveryFileThatNamesTheGroundedSessionQuery_IsInTheInventory_AndTheInventoryHasNoGhosts()
    {
        var apiSrc = LocateApiSrc();
        var actual = FilesNamingTheQuery(apiSrc, s_ownFiles);
        var declared = s_users.Keys.ToHashSet(StringComparer.Ordinal);

        actual.Except(declared).Should().BeEmpty(
            "a file that names AskGroundedSessionQuery can start a retrieval on a game's rulebook, and must check " +
            "the RAG ownership gate for whoever asked (IAnswerPreconditions.EnsureCanAskAboutGameAsync, ADR-096) — " +
            "or be added to the inventory with the reason it does not. Not in the inventory:\n" +
            string.Join('\n', actual.Except(declared)));
        declared.Except(actual).Should().BeEmpty(
            "the inventory names files that no longer name the query: update it instead of letting it drift");
    }

    [Fact]
    public void EveryUserThatOwnsTheGate_CallsItAndDependsOnTheSharedStep()
    {
        var apiSrc = LocateApiSrc();

        foreach (var (file, _) in s_users.Where(s => s.Value is null))
        {
            var text = File.ReadAllText(PathOf(apiSrc, file));

            SourceScanner.FindCodeOccurrences(text, GateCall).Should().NotBeEmpty(
                $"{file} names the grounded session query and is declared to own the RAG ownership gate, but it " +
                $"does not call {GateCall.TrimEnd('(')}: a retrieval on the rulebook would start for anyone");
            SourceScanner.FindCodeOccurrences(text, SharedStep).Should().NotBeEmpty(
                $"{file} must reach the gate through {SharedStep}, the single point the answering paths share, " +
                "and not through a copy of the check");
        }
    }

    [Fact]
    public void EveryUserWithoutTheGate_SaysWhy()
    {
        foreach (var (file, reason) in s_users.Where(s => s.Value is not null))
        {
            reason.Should().NotBeNullOrWhiteSpace($"{file} is inventoried without the gate: write the reason");
            reason!.Length.Should().BeGreaterThan(
                40, $"the reason for {file} must say what is missing and where it is checked, not just name it");
        }
    }

    // -----------------------------------------------------------------------
    // The scan itself: the forms the first version of this gate did not see
    // -----------------------------------------------------------------------

    /// <summary>The ways to use the type, each of which the exact-text search for its constructor missed.</summary>
    private static readonly (string Form, string Code)[] s_usesTheScanMustSee =
    [
        ("the exact form", "var q = new AskGroundedSessionQuery(id, \"q\", null, id);"),
        ("target-typed new (the form analyzers suggest)", "AskGroundedSessionQuery q = new(id, \"q\", null, id);"),
        ("spaces around the name", "var q = new  AskGroundedSessionQuery (id, \"q\", null, id);"),
        ("new and the type on separate lines", "var q = new\n        AskGroundedSessionQuery(id, \"q\", null, id);"),
        ("a using alias", "using Q = Api.BoundedContexts.KnowledgeBase.Application.Queries.AskGroundedSessionQuery;"),
        ("a lambda", "Func<AskGroundedSessionQuery> make = () => new(id, \"q\", null, id);"),
        ("Activator.CreateInstance", "var q = Activator.CreateInstance(typeof(AskGroundedSessionQuery), id, \"q\", null, id);"),
        ("the name in a string", "var t = Type.GetType(\"Api.BoundedContexts.KnowledgeBase.Application.Queries.AskGroundedSessionQuery\");"),
        ("a generic argument", "services.AddSingleton<IValidator<AskGroundedSessionQuery>, V>();"),
        ("the fully qualified name", "var q = new Api.BoundedContexts.KnowledgeBase.Application.Queries.AskGroundedSessionQuery(id, \"q\", null, id);"),
    ];

    public static TheoryData<string, string> UsesTheScanMustSee()
    {
        var data = new TheoryData<string, string>();
        foreach (var (form, code) in s_usesTheScanMustSee)
        {
            data.Add(form, code);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(UsesTheScanMustSee))]
    public void NamesTheQuery_SeesEveryWayToUseTheType(string form, string code)
    {
        NamesTheQuery(code).Should().BeTrue($"«{form}» names the query and must be in the inventory");
    }

    [Theory]
    [InlineData("var h = typeof(AskGroundedSessionQueryHandler);")]
    [InlineData("var q = new MyAskGroundedSessionQuery();")]
    [InlineData("var q = new AskGroundedSessionQueryExtended();")]
    [InlineData("var q = new _AskGroundedSessionQuery();")]
    [InlineData("var q = new AskGroundedSessionQuery2();")]
    [InlineData("var q = new askgroundedsessionquery();")]
    public void NamesTheQuery_IsAWholeWordMatch_NotASubstringOfAnotherName(string code)
    {
        NamesTheQuery(code).Should().BeFalse("it is another identifier, not the query");
    }

    [Theory]
    [InlineData("// AskGroundedSessionQuery is sent by the Arbitro")]
    [InlineData("/* new AskGroundedSessionQuery(id) */ var x = 1;")]
    [InlineData("/// <see cref=\"AskGroundedSessionQuery\"/>\nvar x = 1;")]
    [InlineData("var x = 1; // AskGroundedSessionQuery")]
    public void NamesTheQuery_ANameOnlyInAComment_IsNotAUse_AndIsNotSeen(string code)
    {
        // Documents the limit: a comment is not a use. A cref in documentation costs no inventory entry; what the
        // comment mentions cannot run. The price is a name that is only in a comment going unseen, which is
        // harmless, since it builds nothing.
        NamesTheQuery(code).Should().BeFalse();
    }

    [Fact]
    public void FilesNamingTheQuery_FindsEveryFormInItsOwnFile_AndOnlyThose()
    {
        var root = Path.Combine(Path.GetTempPath(), "grounded-gate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var expected = new List<string>();
            var index = 0;
            foreach (var (_, code) in s_usesTheScanMustSee)
            {
                var relative = $"Some/Feature/Use{index++}.cs";
                Write(root, relative, $"namespace Some.Feature;\nclass C\n{{\n    void M(System.Guid id)\n    {{\n        {code}\n    }}\n}}\n");
                expected.Add(relative);
            }

            Write(root, "Some/Feature/CommentOnly.cs", "// AskGroundedSessionQuery\nclass D { }\n");
            Write(root, "Some/Feature/Lookalike.cs", "class E { object o = new MyAskGroundedSessionQuery(); }\n");
            Write(root, "Own/Definition.cs", "record AskGroundedSessionQuery(System.Guid GameId);\n");
            Write(root, "Own/Handler.cs", "class H : IHandler<AskGroundedSessionQuery> { }\n");
            Write(root, "obj/Generated.cs", "class G { AskGroundedSessionQuery q = new(); }\n");
            Write(root, "bin/Generated.cs", "class G { AskGroundedSessionQuery q = new(); }\n");

            var found = FilesNamingTheQuery(
                root, new HashSet<string>(StringComparer.Ordinal) { "Own/Definition.cs", "Own/Handler.cs" });

            found.Should().BeEquivalentTo(
                expected,
                "every way to use the type is seen; a comment, a lookalike, the type's own files and the build " +
                "output are not");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Source scanning
    // -----------------------------------------------------------------------

    /// <summary>Whether the text names the query as a whole word, outside comments (string literals count).</summary>
    private static bool NamesTheQuery(string text)
    {
        foreach (var offset in SourceScanner.FindOccurrencesOutsideComments(text, QueryName))
        {
            var before = offset > 0 ? text[offset - 1] : ' ';
            var afterIndex = offset + QueryName.Length;
            var after = afterIndex < text.Length ? text[afterIndex] : ' ';
            if (!IsPartOfAnIdentifier(before) && !IsPartOfAnIdentifier(after))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartOfAnIdentifier(char c) => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>Every source file under <paramref name="root"/> that names the query, except the excluded ones, with '/' paths.</summary>
    private static HashSet<string> FilesNamingTheQuery(string root, ISet<string> excluded)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal)
                || relative.StartsWith("bin/", StringComparison.Ordinal)
                || excluded.Contains(relative))
            {
                continue;
            }

            var text = File.ReadAllText(path);

            // a cheap filter before the character-by-character scan
            if (text.Contains(QueryName, StringComparison.OrdinalIgnoreCase) && NamesTheQuery(text))
            {
                found.Add(relative);
            }
        }

        return found;
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
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
