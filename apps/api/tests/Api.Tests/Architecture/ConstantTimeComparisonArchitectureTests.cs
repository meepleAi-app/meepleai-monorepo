using System.Text.RegularExpressions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// Issue #3657 — i confronti di segreti passano da <c>CryptographicOperations.FixedTimeEquals</c>.
///
/// <para>
/// <b>Perché strutturale e non cronometrico.</b> La suite originale
/// (<c>TimingAttackSecurityTests</c>) misurava tempi di parete: 5 test su 8 erano
/// <c>[Fact(Skip = "LIMITE: un test di timing non e esprimibile su runner condiviso — la misura dipende dalla contesa di CPU, non dal codice (vedi #3953). Va riscritto come benchmark con baseline o cancellato")]</c>, e la motivazione
/// era corretta — JIT, scheduling, carico e GC rendono la misura inaffidabile. Ma la garanzia che
/// quei test volevano dare non è «i tempi sono simili»: è <b>«il confronto non termina in anticipo
/// sul primo byte diverso»</b>, e quella dipende interamente dalla primitiva usata. È verificabile
/// leggendo il codice, senza cronometri e senza varianza.
/// </para>
/// <para>
/// Un test che misura il tempo può fallire per il rumore della macchina e passare su un confronto
/// vulnerabile che per caso ha impiegato lo stesso tempo. Questo non può: se qualcuno sostituisce
/// <c>FixedTimeEquals</c> con <c>SequenceEqual</c> o <c>==</c>, diventa rosso in modo deterministico.
/// </para>
/// <para>
/// Segue il pattern a scansione del sorgente già usato da
/// <c>EgressHttpClientPinArchitectureTests</c> e <c>NonCriticalHealthCheckStatusArchitectureTests</c>.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.Security)]
[Trait("BoundedContext", "Architecture")]
[Trait("OWASP", "A07-Authentication")]
public class ConstantTimeComparisonArchitectureTests
{
    /// <summary>
    /// I confronti che devono essere a tempo costante, con il segreto che proteggono.
    /// Aggiungendo un nuovo confronto di segreti, registralo qui: il test
    /// <see cref="EverySiteUsingFixedTimeEquals_IsRegisteredInTheInventory"/> fallisce finché non lo fai,
    /// così l'inventario non può divergere dal codice in silenzio.
    /// </summary>
    private static readonly (string RelativePath, string Secret)[] ConstantTimeSites =
    [
        ("Services/PasswordHashingService.cs", "hash PBKDF2 della password"),
        ("BoundedContexts/Authentication/Domain/ValueObjects/PasswordHash.cs", "hash PBKDF2 della password (value object)"),
        (RegisterHandlerPath, "token di bootstrap del primo amministratore"),
        ("BoundedContexts/UserNotifications/Infrastructure/Slack/SlackSignatureValidator.cs", "firma HMAC del webhook Slack"),
    ];

    private const string ConstantTimeCall = "CryptographicOperations.FixedTimeEquals";

    private const string RegisterHandlerPath =
        "BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommandHandler.cs";

    /// <summary>The two variables that hold the bootstrap token in <c>TryClaimBootstrapAdminAsync</c>.</summary>
    private static readonly string[] BootstrapTokenVariables = ["providedToken", "configuredToken"];

    /// <summary>A comparison of a token as a string: it ends at the first different character.</summary>
    private static readonly Regex VariableTimeTokenCompare = new(
        @"\b(providedToken|configuredToken)\b\s*(==|!=)"
        + @"|(==|!=)\s*\b(providedToken|configuredToken)\b"
        + @"|\b(providedToken|configuredToken)\s*\.\s*(Equals|CompareTo|StartsWith|EndsWith|SequenceEqual)\s*\("
        + @"|\bstring\s*\.\s*(Equals|Compare|CompareOrdinal)\s*\([^)]*\b(providedToken|configuredToken)\b",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    [Fact]
    public void SecretComparisons_UseFixedTimeEquals()
    {
        var apiSrc = LocateApiSrc();

        var missing = ConstantTimeSites
            .Where(site => !ReadSource(apiSrc, site.RelativePath).Contains(ConstantTimeCall, StringComparison.Ordinal))
            .Select(site => $"{site.RelativePath} — protegge: {site.Secret}")
            .ToList();

        missing.Should().BeEmpty(
            $"un confronto di segreti che non passa da {ConstantTimeCall} termina al primo byte "
            + "diverso, e il tempo di risposta rivela quanti byte iniziali erano corretti — è il "
            + "presupposto di un attacco a forza bruta byte per byte (OWASP A07:2021). "
            + "Siti senza confronto a tempo costante:\n" + string.Join('\n', missing));
    }

    [Fact]
    public void SecretComparisons_DoNotUseVariableTimeEquality()
    {
        var apiSrc = LocateApiSrc();

        var offenders = new List<string>();

        foreach (var site in ConstantTimeSites)
        {
            var text = ReadSource(apiSrc, site.RelativePath);

            // SequenceEqual è la sostituzione plausibile: stessa firma d'uso su byte[], ma esce
            // al primo elemento diverso. `==` su byte[] confronta i riferimenti, quindi non è un
            // rischio di timing ma un bug funzionale che altri test coglierebbero.
            foreach (var (line, number) in NumberedLines(text))
            {
                if (line.Contains(".SequenceEqual(", StringComparison.Ordinal))
                {
                    offenders.Add($"{site.RelativePath}:{number}  {line.Trim()}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "SequenceEqual esce al primo byte diverso: usato su un segreto reintroduce esattamente "
            + "la perdita di tempo che FixedTimeEquals esiste per evitare. Occorrenze:\n"
            + string.Join('\n', offenders));
    }

    /// <summary>
    /// #5178 — the presence of <c>FixedTimeEquals</c> in the file does not stop a string comparison of the bootstrap
    /// token placed before it, as a shortcut: the response time would tell again how much of the token's start is right.
    /// This replaces the wall-clock test of the registration, which could not see that and failed on database noise.
    /// </summary>
    [Fact]
    public void BootstrapAdminToken_IsNeverComparedAsAString()
    {
        var text = ReadSource(LocateApiSrc(), RegisterHandlerPath);

        // A scan that no longer finds the variables would pass for nothing: renamed, they must be renamed here too.
        foreach (var variable in BootstrapTokenVariables)
        {
            text.Should().Contain(variable,
                $"the scan looks for comparisons of {variable}; if the variable was renamed, update {nameof(BootstrapTokenVariables)}");
        }

        var offenders = NumberedLines(text)
            .Where(l => !l.Line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(l => VariableTimeTokenCompare.IsMatch(l.Line))
            .Select(l => $"{RegisterHandlerPath}:{l.Number}  {l.Line.Trim()}")
            .ToList();

        offenders.Should().BeEmpty(
            $"the bootstrap token is compared only through {ConstantTimeCall}, on its bytes, after the length check. "
            + "Occurrences:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void EverySiteUsingFixedTimeEquals_IsRegisteredInTheInventory()
    {
        var apiSrc = LocateApiSrc();
        var registered = ConstantTimeSites
            .Select(site => site.RelativePath)
            .ToHashSet(StringComparer.Ordinal);

        var unregistered = new List<string>();

        foreach (var path in Directory.EnumerateFiles(apiSrc, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(apiSrc, path).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal)
                || relative.StartsWith("obj/", StringComparison.Ordinal))
            {
                continue;
            }

            if (File.ReadAllText(path).Contains(ConstantTimeCall, StringComparison.Ordinal)
                && !registered.Contains(relative))
            {
                unregistered.Add(relative);
            }
        }

        unregistered.Should().BeEmpty(
            "un nuovo confronto a tempo costante è comparso senza essere registrato in "
            + $"{nameof(ConstantTimeSites)}. Aggiungilo con il segreto che protegge: l'inventario è "
            + "ciò che rende gli altri due test capaci di accorgersi di una rimozione. "
            + "Non registrati:\n" + string.Join('\n', unregistered));
    }

    // -----------------------------------------------------------------------
    // Source scanning
    // -----------------------------------------------------------------------

    private static string ReadSource(string apiSrc, string relativePath)
    {
        var path = Path.Combine(apiSrc, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue(
            $"il sito registrato in {nameof(ConstantTimeSites)} deve esistere: {relativePath}. "
            + "Se il file è stato spostato, aggiorna l'inventario invece di rimuoverlo.");

        return File.ReadAllText(path);
    }

    private static IEnumerable<(string Line, int Number)> NumberedLines(string text)
        => text.Split('\n').Select((line, index) => (line, index + 1));

    private static string LocateApiSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        // In un git worktree `.git` è un file, non una directory: accettare entrambi fa fermare
        // la risalita alla root giusta invece di proseguire verso il checkout principale.
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
