using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// ADR-095 D4, fetta 2 (#4165) — gate di architettura: <b>ogni generazione LLM passa dal gateway</b>
/// (<c>Services/LlmClients/</c>, dietro <c>ILlmService</c>).
/// <para>
/// Una chiamata diretta a un provider è fuori dal registro costi, dai circuit breaker, dalla
/// credenziale gestita dall'admin e, dalla fetta 3, dal tetto di spesa. Prima di questo gate
/// <c>ChunkTranslationService</c> chiamava OpenRouter con un suo <c>HttpClient</c>, e una seconda
/// copia dello stesso schema (<c>VisionOcrAdapter</c>) è rimasta nel repository sette mesi senza
/// essere mai registrata.
/// </para>
/// <para>
/// Due regole complementari. L'endpoint di generazione si riconosce dal percorso anche quando la base
/// URL arriva da un client nominato. Un host scritto per esteso si riconosce anche quando il percorso
/// è costruito altrove. Gli host fuori dal gateway sono ammessi solo in file dichiarati, ciascuno con
/// la ragione per cui non genera testo.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4165")]
public sealed class LlmProviderEgressArchitectureTests
{
    private const string GatewayDirectory = "Services/LlmClients/";

    private static readonly string[] GenerationEndpoints = ["chat/completions"];

    private static readonly string[] ProviderHosts =
    [
        "openrouter.ai",
        "api.openai.com",
        "api.anthropic.com",
        "api.deepseek.com",
        "generativelanguage.googleapis.com",
        "api.mistral.ai",
        "api.groq.com",
        "api.together.xyz",
    ];

    /// <summary>
    /// File fuori dal gateway che possono nominare l'host di un provider, con il motivo. Nessuno di
    /// questi genera testo: se uno comincia a farlo, il suo posto è il gateway, non questa lista.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> DeclaredHostReferences =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Extensions/InfrastructureServiceExtensions.cs"] =
                "base URL del client nominato «OpenRouter» usato dal gateway e delle sonde /v1/models (#936)",
            ["BoundedContexts/Administration/Infrastructure/External/OpenRouterService.cs"] =
                "auth/key: limite e consumo della chiave, nessuna generazione",
            ["Services/Providers/Quota/OpenRouterQuotaProvider.cs"] =
                "auth/key: quota residua, nessuna generazione",
            ["Services/Providers/Quota/DeepSeekQuotaProvider.cs"] =
                "user/balance: saldo, nessuna generazione",
        };

    [Fact]
    public void GenerationEndpoints_AreCalledOnlyFromTheGateway()
    {
        var offenders = FilesOutsideGatewayContaining(GenerationEndpoints);

        offenders.Should().BeEmpty(
            "una generazione fuori dal gateway non passa dal registro costi né dal tetto di spesa "
            + "(ADR-095 D4): usa ILlmService. Trovati: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ProviderHosts_AreNamedOnlyByTheGatewayOrByDeclaredFiles()
    {
        var undeclared = FilesOutsideGatewayContaining(ProviderHosts)
            .Where(path => !DeclaredHostReferences.ContainsKey(path))
            .ToList();

        undeclared.Should().BeEmpty(
            "un host di provider fuori dal gateway è o una generazione diretta (va spostata dietro "
            + "ILlmService) o un'API di gestione senza costo di generazione (va dichiarata in "
            + "DeclaredHostReferences con il motivo). Non dichiarati: " + string.Join(", ", undeclared));
    }

    /// <summary>Tiene la lista onesta: una voce che non serve più va tolta, non lasciata a coprire altro.</summary>
    [Fact]
    public void DeclaredHostReferences_AreStillNeeded()
    {
        var withHosts = FilesOutsideGatewayContaining(ProviderHosts).ToHashSet(StringComparer.Ordinal);

        var stale = DeclaredHostReferences.Keys.Where(path => !withHosts.Contains(path)).ToList();

        stale.Should().BeEmpty("queste voci non nominano più un host di provider: rimuovile");
    }

    private static List<string> FilesOutsideGatewayContaining(IReadOnlyCollection<string> needles)
    {
        var apiSrc = LocateApiSrc();
        var result = new List<string>();
        foreach (var path in Directory.EnumerateFiles(apiSrc, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(apiSrc, path).Replace('\\', '/');
            if (relative.StartsWith(GatewayDirectory, StringComparison.Ordinal)
                || relative.StartsWith("obj/", StringComparison.Ordinal)
                || relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (needles.Any(needle => SourceScanner.FindOccurrencesOutsideComments(text, needle).Count > 0))
            {
                result.Add(relative);
            }
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    /// Risale dalla cartella dei binari fino a trovare i sorgenti dell'API. Non cerca <c>.git</c>: in un
    /// worktree <c>.git</c> è un file, e i gate che ne cercano la cartella falliscono tutti.
    /// </summary>
    private static string LocateApiSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "apps", "api", "src", "Api")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the meepleai-monorepo repo");
        return Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
    }
}
