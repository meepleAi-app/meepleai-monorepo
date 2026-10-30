using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #4749 — architecture gate: <b>a query on <c>UserLibraryEntries</c> must not filter or project
/// <c>.GameId</c></b>.
/// <para>
/// #3662 renamed the column to <c>SharedGameId</c> and left <c>UserLibraryEntryEntity.GameId</c> as a
/// computed compatibility property (<c>SharedGameId ?? Guid.Empty</c>) outside the EF model
/// (<c>builder.Ignore(e =&gt; e.GameId)</c>). On an entity already in memory it works; inside a query EF
/// cannot translate it (<c>Translation of member 'GameId' … failed. This commonly occurs when the
/// specified member is unmapped</c>). Two handlers did exactly that, so <c>batch-status</c> answered 500
/// on every call and the «già in libreria» badge never appeared.
/// </para>
/// <para>
/// The gate reads each statement that starts at <c>UserLibraryEntries</c> (or
/// <c>Set&lt;UserLibraryEntryEntity&gt;()</c>) up to its <c>;</c> and fails when it contains
/// <c>.GameId</c>. It is a text scan, not a semantic one: a <c>.GameId</c> that belongs to another,
/// mapped entity in the same chain is a false positive and goes in <see cref="OtherEntitiesGameId"/> with
/// the reason. Uses AFTER materialization (<c>entries.Select(e =&gt; e.GameId)</c>) are different statements
/// and are not flagged.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4749")]
public sealed class UserLibraryEntryGameIdQueryArchitectureTests
{
    /// <summary>
    /// Files whose flagged statement reads the <c>GameId</c> of ANOTHER entity, which is a mapped
    /// column. Adding an entry means "I checked: this <c>.GameId</c> is not
    /// <c>UserLibraryEntryEntity.GameId</c>". The key is the path under <c>apps/api/src/Api</c>.
    /// </summary>
    private static readonly Dictionary<string, string> OtherEntitiesGameId = new(StringComparer.Ordinal)
    {
        ["BoundedContexts/UserLibrary/Infrastructure/Persistence/UserGamebookViewRepository.cs"] =
            "`.Count(t => t.GameId == priv.Id)` reads TextChunkEntity.GameId, a mapped column of "
            + "another table; the library entry itself is read through SharedGameId/PrivateGameId",
    };

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    private static readonly Regex QueryStart = new(
        @"\bUserLibraryEntries\b|Set<UserLibraryEntryEntity>\s*\(\s*\)",
        RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex GameIdMember = new(@"\.GameId\b", RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex BlockComment = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex LineComment = new(@"//[^\n]*", RegexOptions.CultureInvariant, RegexTimeout);

    [Fact]
    public void QueriesOnUserLibraryEntries_DoNotUseTheUnmappedGameId()
    {
        var offenders = ScanSources()
            .Where(s => s.UsesGameId)
            .Where(s => !OtherEntitiesGameId.ContainsKey(s.File))
            .Select(s => $"{s.File}:{s.Line}")
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "UserLibraryEntryEntity.GameId is a compatibility property outside the EF model: inside a "
            + "query EF cannot translate it and the call answers 500 (#4749, batch-status). Filter and "
            + "project on SharedGameId (`e.SharedGameId ?? Guid.Empty` when the result must be a Guid). "
            + "If the `.GameId` belongs to another, mapped entity in the same chain, add the file to "
            + "OtherEntitiesGameId with the reason. Offenders:\n"
            + string.Join('\n', offenders));
    }

    /// <summary>
    /// Keeps the exemption list honest: an entry whose file no longer has a flagged statement would
    /// silently grant a pass to the next <c>.GameId</c> that lands in that file.
    /// </summary>
    [Fact]
    public void OtherEntitiesGameIdExemptions_HaveNoStaleEntries()
    {
        var flaggedFiles = ScanSources()
            .Where(s => s.UsesGameId)
            .Select(s => s.File)
            .ToHashSet(StringComparer.Ordinal);

        var stale = OtherEntitiesGameId.Keys
            .Where(file => !flaggedFiles.Contains(file))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "every exemption must still correspond to a flagged statement. Stale:\n"
            + string.Join('\n', stale));
    }

    [Fact]
    public void Scanner_FindsTheQueries()
    {
        // Guards the gate itself: a scanner that silently matches nothing would make the gate above
        // pass vacuously.
        ScanSources().Should().HaveCountGreaterThan(30,
            "the API runs dozens of statements on UserLibraryEntries; finding almost none means the "
            + "scanner broke, not that the codebase is clean");
    }

    [Fact]
    public void Scanner_FlagsTheOriginalDefect_AndOnlyIt()
    {
        const string defect = """
            var entries = await _dbContext.UserLibraryEntries
                .Where(ulg => ulg.UserId == query.UserId && query.GameIds.Contains(ulg.GameId))
                .ToListAsync(cancellationToken);
            """;
        const string projected = """
            var games = await _dbContext.UserLibraryEntries
                .Select(e => new RecentGameDto(e.GameId, e.AddedAt))
                .ToListAsync(ct);
            """;
        const string fixedForm = """
            var entries = await _dbContext.UserLibraryEntries
                .Where(ulg => ulg.UserId == query.UserId && query.GameIds.Contains(ulg.SharedGameId!.Value))
                .Select(ulg => new { GameId = ulg.SharedGameId!.Value, ulg.IsFavorite })
                .ToListAsync(cancellationToken);
            """;
        const string afterMaterialization = """
            var entries = await _dbContext.UserLibraryEntries.ToListAsync(ct);
            var gameIds = entries.Select(e => e.GameId).ToList();
            """;
        const string onlyInAComment = """
            var entries = await _dbContext.UserLibraryEntries
                // not .GameId: the compatibility property is outside the EF model
                .Where(ulg => ulg.SharedGameId == id)
                .ToListAsync(ct);
            """;
        const string setForm = """
            var n = await _dbContext.Set<UserLibraryEntryEntity>().CountAsync(e => e.GameId == id, ct);
            """;

        FindStatements(defect, "x.cs").Should().ContainSingle(s => s.UsesGameId, "the original shape of #4749");
        FindStatements(projected, "x.cs").Should().ContainSingle(s => s.UsesGameId, "a projection is translated too");
        FindStatements(setForm, "x.cs").Should().ContainSingle(s => s.UsesGameId, "Set<UserLibraryEntryEntity>() is the same table");
        FindStatements(fixedForm, "x.cs").Should().ContainSingle(s => !s.UsesGameId, "SharedGameId, and an anonymous member named GameId, are fine");
        FindStatements(afterMaterialization, "x.cs").Should().ContainSingle(s => !s.UsesGameId, "a use after ToListAsync is another statement");
        FindStatements(onlyInAComment, "x.cs").Should().ContainSingle(s => !s.UsesGameId, "a comment is not code");
    }

    // -----------------------------------------------------------------------
    // Source scanning
    // -----------------------------------------------------------------------

    private sealed record QueryStatement(string File, int Line, bool UsesGameId);

    private static List<QueryStatement> ScanSources()
    {
        var apiSrc = LocateApiSrc();
        var statements = new List<QueryStatement>();

        foreach (var path in Directory.EnumerateFiles(apiSrc, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(apiSrc, path).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal)
                || relative.StartsWith("obj/", StringComparison.Ordinal)
                || relative.Contains("/Migrations/", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (!text.Contains("UserLibraryEntr", StringComparison.Ordinal))
            {
                continue;
            }

            statements.AddRange(FindStatements(text, relative));
        }

        return statements;
    }

    private static List<QueryStatement> FindStatements(string text, string file)
    {
        var code = StripComments(text);
        var statements = new List<QueryStatement>();

        foreach (Match start in QueryStart.Matches(code))
        {
            var end = code.IndexOf(';', start.Index + start.Length);
            if (end < 0)
            {
                continue;
            }

            var statement = code[start.Index..end];
            var line = code.AsSpan(0, start.Index).Count('\n') + 1;
            statements.Add(new QueryStatement(file, line, GameIdMember.IsMatch(statement)));
        }

        return statements;
    }

    /// <summary>Blanks comments but keeps every newline, so line numbers still point at the source.</summary>
    private static string StripComments(string text)
    {
        static string Blank(Match m) => new(m.Value.Select(c => c == '\n' ? '\n' : ' ').ToArray());

        return LineComment.Replace(BlockComment.Replace(text, Blank), Blank);
    }

    private static string LocateApiSrc()
    {
        // `.git` is a directory in a normal clone and a FILE inside a git worktree — probe both so
        // the gate runs identically in CI and in a developer worktree.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
            && !Directory.Exists(Path.Combine(dir.FullName, ".git"))
            && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var apiSrc = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiSrc).Should().BeTrue($"Api source must exist at {apiSrc}");
        return apiSrc;
    }
}
