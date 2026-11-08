using System.Reflection;
using System.Text.RegularExpressions;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4844 — gate di architettura: <b>i codici macchina dei 409 hanno UNO stile</b>, minuscolo con
/// underscore (<c>session_paused</c>, <c>turn_changed</c>, <c>score_changed</c>, e lato client
/// <c>kb_not_ready</c>).
/// <para>
/// Il codice viaggia nel campo <c>error</c> del corpo HTTP e il client lo confronta come stringa
/// esatta (<c>error.code === 'session_paused'</c>): uno stile misto non è una questione estetica, è
/// un confronto che fallisce in silenzio e mostra la frase generica al posto di quella giusta. Prima
/// di #4844 lo stile era misto — <c>MAX_LIVE_SESSIONS_EXCEEDED</c> accanto a <c>turn_changed</c> — e
/// la scelta è stata presa prima di aggiungerne sei.
/// </para>
/// <para>
/// Due fonti, perché nessuna delle due basta da sola: i <b>letterali</b> passati come primo argomento
/// a <c>new ConflictException("codice", messaggio)</c> si vedono solo nei sorgenti (via
/// <see cref="SourceScanner"/>, che salta commenti e letterali di documentazione); le <b>costanti</b>
/// (<c>LiveSessionConflictCodes.*</c>, <c>MaxLiveSessionsExceededException.Code</c>,
/// <c>ScoreChangedCode</c>, <c>TurnChangedCode</c>) si leggono per riflessione, che non dipende da
/// come sono scritte.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed partial class ConflictErrorCodeStyleArchitectureTests
{
    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LowerSnake();

    /// <summary>
    /// Codici che non seguono lo stile, con la ragione. Una voce senza ragione è debito che nessuno sa
    /// più perché esiste; una voce che non corrisponde più a nulla la segnala
    /// <see cref="EveryExemptionStillAppliesToSomething"/>.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        ["MaxLiveSessionsExceededException.Code"] =
            "MAX_LIVE_SESSIONS_EXCEEDED è nato prima della regola (#2633, WS1 DEC-7) e il client lo confronta come " +
            "stringa esatta in useStartNextGame.ts e BlockedLiveSessionModal.tsx: rinominarlo è un cambio coordinato " +
            "server + client che #4844 non fa. Se si rinomina, togliere questa voce nella stessa PR.",
    };

    /// <summary>
    /// Le classi che dichiarano codici come costanti senza derivare da <see cref="ConflictException"/>.
    /// Solo i campi il cui nome finisce in <c>Code</c> (o tutti, per la classe dei codici) sono codici:
    /// un comando può avere altre costanti stringa che non viaggiano nel corpo HTTP.
    /// </summary>
    private static readonly Type[] CodeHolders =
    [
        typeof(LiveSessionConflictCodes),
        typeof(ConfirmScoreProposalCommand),
        typeof(AdvanceLiveSessionTurnCommand),
    ];

    [Fact]
    public void EveryCodeConstant_IsLowerSnake()
    {
        var offenders = CodeConstants()
            .Where(c => !Exempt.ContainsKey(c.Name))
            .Where(c => !LowerSnake().IsMatch(c.Value))
            .Select(c => $"{c.Name} = \"{c.Value}\"")
            .ToList();

        offenders.Should().BeEmpty(
            "un codice macchina di un 409 è minuscolo con underscore, come turn_changed e session_paused: " +
            "il client lo confronta come stringa esatta, e uno stile misto fallisce in silenzio. " +
            "Costanti fuori stile:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void EveryLiteralCodePassedToConflictException_IsLowerSnake()
    {
        var sites = ScanLiteralCodeSites();

        var offenders = sites
            .Where(s => !LowerSnake().IsMatch(s.Code))
            .Select(s => $"{s.RelativePath}: \"{s.Code}\"")
            .ToList();

        offenders.Should().BeEmpty(
            "il primo argomento di new ConflictException(codice, messaggio) è un codice macchina e segue lo " +
            "stile minuscolo con underscore. Se ti serve un codice nuovo, mettilo in una classe di costanti " +
            "(LiveSessionConflictCodes è il modello) e aggiungi quella classe a CodeHolders. Siti fuori stile:\n  " +
            string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Contro-prova delle due scansioni: se smettessero di vedere, i test sopra passerebbero sul vuoto.
    /// Due siti noti devono comparire — un letterale a due argomenti nel codice di produzione
    /// (<c>ab_test_budget_exhausted</c>, l'unico precedente prima di #4844) e la costante esentata.
    /// </summary>
    [Fact]
    public void TheScansSeeTheKnownSites()
    {
        ScanLiteralCodeSites().Select(s => s.Code)
            .Should().Contain("ab_test_budget_exhausted",
                "CreateAbTestCommandHandler passa quel codice come letterale: se lo scanner non lo vede, " +
                "non vede nessun letterale e il gate misura il vuoto");

        CodeConstants().Select(c => c.Name)
            .Should().Contain("MaxLiveSessionsExceededException.Code")
            .And.Contain("LiveSessionConflictCodes.SessionPaused")
            .And.Contain("ConfirmScoreProposalCommand.ScoreChangedCode")
            .And.Contain("AdvanceLiveSessionTurnCommand.TurnChangedCode");
    }

    [Fact]
    public void EveryExemptionStillAppliesToSomething()
    {
        var known = CodeConstants().Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var stale = Exempt.Keys.Where(k => !known.Contains(k)).ToList();

        stale.Should().BeEmpty(
            "un'esenzione che non corrisponde più a nessuna costante va RIMOSSA: " + string.Join(", ", stale));

        foreach (var (name, reason) in Exempt)
        {
            reason.Length.Should().BeGreaterThan(30, $"l'esenzione di {name} deve spiegare, non etichettare");
        }
    }

    private sealed record CodeConstant(string Name, string Value);

    private sealed record LiteralCodeSite(string RelativePath, string Code);

    private static List<CodeConstant> CodeConstants()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var constants = new List<CodeConstant>();

        var conflictTypes = typeof(ConflictException).Assembly.GetTypes()
            .Where(t => typeof(ConflictException).IsAssignableFrom(t));

        foreach (var type in conflictTypes.Concat(CodeHolders).Distinct())
        {
            var onlyCodeNamed = type != typeof(LiveSessionConflictCodes) && !typeof(ConflictException).IsAssignableFrom(type);
            foreach (var field in type.GetFields(flags))
            {
                if (!field.IsLiteral || field.FieldType != typeof(string))
                {
                    continue;
                }

                if (onlyCodeNamed && !field.Name.EndsWith("Code", StringComparison.Ordinal))
                {
                    continue;
                }

                if (field.GetRawConstantValue() is string value)
                {
                    constants.Add(new CodeConstant($"{type.Name}.{field.Name}", value));
                }
            }
        }

        return constants;
    }

    /// <summary>
    /// Ogni <c>ConflictException("…", …)</c> — e ogni <c>base("…", …)</c> in un file che deriva da
    /// <see cref="ConflictException"/> — il cui PRIMO argomento è un letterale che fa da codice.
    /// Un letterale seguito da <c>)</c> è un messaggio (forma a un argomento) e non conta; un
    /// <c>$"…"</c> è un messaggio interpolato e non conta.
    /// </summary>
    /// <remarks>
    /// 🔴 Esiste un terzo costruttore, <c>ConflictException(string message, Exception inner)</c>, e
    /// anche lì il primo letterale è seguito da una virgola: la prima stesura del gate denunciava
    /// come «codici fuori stile» una dozzina di messaggi come «Mechanic analysis was modified by
    /// another operation. Please retry.». Il criterio distingue i due casi dal SECONDO argomento: se è
    /// una stringa (<c>"…"</c> o <c>$"…"</c>) il primo è un codice, qualunque aspetto abbia; se è un
    /// identificatore e il primo letterale contiene spazi, è un messaggio con l'eccezione interna.
    /// Resta un limite dichiarato: <c>ConflictException("codice", variabileMessaggio)</c> si vede
    /// solo se il codice non ha spazi, cioè nei casi in cui il codice è almeno un token.
    /// </remarks>
    private static List<LiteralCodeSite> ScanLiteralCodeSites()
    {
        var sourceRoot = LocateApiSourceRoot();
        var sites = new List<LiteralCodeSite>();

        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            var relative = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');

            var needles = new List<string> { "ConflictException(" };
            if (text.Contains(": ConflictException", StringComparison.Ordinal))
            {
                needles.Add("base(");
            }

            foreach (var needle in needles)
            {
                foreach (var offset in SourceScanner.FindCodeOccurrences(text, needle))
                {
                    var code = LeadingLiteralFollowedByComma(text, offset + needle.Length);
                    if (code is not null)
                    {
                        sites.Add(new LiteralCodeSite(relative, code));
                    }
                }
            }
        }

        return sites;
    }

    private static string? LeadingLiteralFollowedByComma(string text, int index)
    {
        var i = index;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        if (i >= text.Length || text[i] != '"')
        {
            return null;
        }

        var start = i + 1;
        var end = SourceScanner.SkipTrivia(text, i);
        if (end == i)
        {
            return null;
        }

        var literal = text[start..(end - 1)];

        var j = end;
        while (j < text.Length && char.IsWhiteSpace(text[j]))
        {
            j++;
        }

        if (j >= text.Length || text[j] != ',')
        {
            return null;
        }

        j++;
        while (j < text.Length && char.IsWhiteSpace(text[j]))
        {
            j++;
        }

        var secondIsString = j < text.Length && (text[j] == '"' || (text[j] == '$' && j + 1 < text.Length && text[j + 1] == '"'));
        if (secondIsString)
        {
            return literal;
        }

        // Second argument is an identifier: `(message, innerException)` when the literal reads like a
        // sentence, `(code, messageVariable)` when it is a single token.
        return literal.Any(char.IsWhiteSpace) ? null : literal;
    }

    private static string LocateApiSourceRoot()
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

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        var root = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(root).Should().BeTrue($"API sources must exist at {root}");
        return root;
    }
}
