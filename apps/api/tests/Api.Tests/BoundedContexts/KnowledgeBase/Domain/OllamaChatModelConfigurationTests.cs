using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using RagStrategy = Api.BoundedContexts.KnowledgeBase.Domain.Enums.RagStrategy;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// #4025 — il modello di chat di Ollama è configurabile, e il provider che serve una richiesta è
/// osservabile.
/// </summary>
/// <remarks>
/// <para>
/// Il difetto: <c>infra/docker-compose.yml</c> passa <c>OLLAMA_CHAT_MODEL</c> a <c>ollama-pull</c>
/// per decidere quale modello scaricare, e documenta il knob
/// («<c>OLLAMA_CHAT_MODEL=qwen2.5:1.5b make dev</c> scarica ~1GB invece di 4,7GB»). L'API non
/// leggeva quella variabile: chiedeva <c>llama3:8b</c> hardcoded. Due manopole con lo stesso nome e
/// nessun collegamento — chi la girava otteneva il download richiesto e un <c>404 model not
/// found</c> alla prima chat su Ollama.
/// </para>
/// <para>
/// Questi test sono <c>Category=Unit</c>: mordono nel job <i>Backend Fast</i> su ogni PR verso
/// main-dev, non in una suite che gira una volta al mese.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4025")]
public sealed class OllamaChatModelConfigurationTests
{
    // ─── la regola di risoluzione, come funzione pura ───────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SenzaValoreConfigurato_UsaIlDefault(string? configured)
    {
        AgentDefaults.ResolveOllamaChatModel(configured)
            .Should().Be(AgentDefaults.OllamaChatModelDefault);
    }

    [Theory]
    [InlineData("qwen2.5:3b", "qwen2.5:3b")]
    [InlineData("  qwen2.5:3b  ", "qwen2.5:3b")]
    [InlineData("mistral", "mistral")]
    public void ConUnValoreConfigurato_LoUsa(string configured, string expected)
    {
        AgentDefaults.ResolveOllamaChatModel(configured).Should().Be(expected);
    }

    // ─── il cablaggio con l'ambiente ─────────────────────────────────────────────────────────────

    [Fact]
    public void LaPropertyLeggeLaVariabileDAmbiente()
    {
        // Muta una variabile globale al processo, quindi in teoria è esposta alla parallelizzazione
        // di xUnit fra classi. In pratica nessun altro punto della suite legge OLLAMA_CHAT_MODEL —
        // verificabile con:
        //   grep -rn "OLLAMA_CHAT_MODEL" apps/api --include=*.cs
        // che fuori da AgentDefaults.cs e da questo file non trova nulla. Se un giorno ne trovasse,
        // questo test va spostato in una fixture che serializza, non lasciato a sperare.
        var original = Environment.GetEnvironmentVariable(AgentDefaults.OllamaChatModelEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(AgentDefaults.OllamaChatModelEnvVar, "qwen2.5:3b");
            AgentDefaults.OllamaFallbackModel.Should().Be("qwen2.5:3b");

            Environment.SetEnvironmentVariable(AgentDefaults.OllamaChatModelEnvVar, null);
            AgentDefaults.OllamaFallbackModel.Should().Be(AgentDefaults.OllamaChatModelDefault);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AgentDefaults.OllamaChatModelEnvVar, original);
        }
    }

    // ─── il default del codice e quello di compose non possono divergere ────────────────────────

    [Fact]
    public void IlDefaultDelCodiceCoincideConQuelloDiCompose()
    {
        // Un default che diverge riporta lo stesso difetto in forma più difficile da vedere: con la
        // variabile impostata tutto funziona, senza di essa compose scarica un modello e l'API ne
        // chiede un altro.
        var found = ComposeChatModelDefaults();

        found.Should().NotBeEmpty(
            "l'espressione ${OLLAMA_CHAT_MODEL:-…} deve esistere nei file compose, altrimenti "
            + "questo test passerebbe senza confrontare niente");

        foreach (var (file, value) in found)
        {
            value.Should().Be(
                AgentDefaults.OllamaChatModelDefault,
                $"{file} definisce il default del modello di chat e deve coincidere con "
                + $"AgentDefaults.{nameof(AgentDefaults.OllamaChatModelDefault)}");
        }
    }

    [Fact]
    public void LaScansioneTrovaEntrambiIFileCompose()
    {
        // Controprova del test precedente: se l'espressione sparisse da uno dei due file — per
        // esempio perché il servizio api smette di ricevere la variabile — quel test continuerebbe
        // a passare sull'altro, e il cablaggio sarebbe rotto in silenzio.
        var files = ComposeChatModelDefaults().Select(f => f.File).ToList();

        files.Should().Contain("docker-compose.yml", "è il file che decide cosa scarica ollama-pull");
        files.Should().Contain("compose.dev.yml", "è il file che passa la variabile al servizio api");
    }

    // ─── il provider che ha servito la richiesta è osservabile ──────────────────────────────────

    [Fact]
    public async Task IlProviderEIlModelloCheHannoServitoLaRichiestaSonoOsservabili()
    {
        // Il secondo DoD di #4025. Senza questo, «la chat ha risposto» non dice CHI ha risposto, e
        // un test parametrico su due provider potrebbe passare due volte sullo stesso.
        var original = Environment.GetEnvironmentVariable(AgentDefaults.OllamaChatModelEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(AgentDefaults.OllamaChatModelEnvVar, "qwen2.5:3b");

            var result = await CreateSelector().SelectProviderAsync(
                LlmUserContext.Anonymous,
                RagStrategy.Fast,
                RequestSource.AutomatedTest,
                TestContext.Current.CancellationToken);

            result.HasProvider.Should().BeTrue();
            result.Decision.ProviderName.Should().Be("Ollama");
            result.Decision.ModelId.Should().Be(
                "qwen2.5:3b",
                "il modello risolto deve arrivare fino alla decisione di routing, altrimenti la "
                + "variabile è letta e poi persa per strada");
            result.Decision.Reason.Should().NotBeNullOrWhiteSpace(
                "la ragione della scelta è ciò che distingue una rotta voluta da un fallback");
        }
        finally
        {
            Environment.SetEnvironmentVariable(AgentDefaults.OllamaChatModelEnvVar, original);
        }
    }

    [Fact]
    public async Task UnTestAutomatizzatoEPinnatoSuOllamaPerDesign()
    {
        // 🔴 Perché A6 della spec («lo stesso test su due provider diversi») NON è raggiungibile
        // oggi: LlmProviderSelector instrada RequestSource.AutomatedTest su Ollama *qualunque* sia
        // la strategia, per non consumare quota OpenRouter gratuita. Un test parametrico su due
        // provider passerebbe quindi due volte su Ollama — il criterio sarebbe falsamente
        // verificato. Questo test fissa il vincolo invece di aggirarlo: se un giorno il pin
        // cadesse, va riletta la spec, non silenziato il test.
        var strategie = new[] { RagStrategy.Fast, RagStrategy.Expert };
        var selector = CreateSelector();

        foreach (var strategia in strategie)
        {
            var result = await selector.SelectProviderAsync(
                LlmUserContext.Anonymous,
                strategia,
                RequestSource.AutomatedTest,
                TestContext.Current.CancellationToken);

            result.Decision.ProviderName.Should().Be(
                "Ollama",
                $"la strategia {strategia} non cambia la rotta di un AutomatedTest");
        }
    }

    // ─── helper ──────────────────────────────────────────────────────────────────────────────────

    private static LlmProviderSelector CreateSelector()
    {
        var ollama = new Mock<ILlmClient>();
        ollama.Setup(c => c.ProviderName).Returns("Ollama");

        var circuitBreaker = new Mock<ICircuitBreakerRegistry>();
        circuitBreaker.Setup(r => r.AllowsRequests(It.IsAny<string>())).Returns(true);

        // Nessun modello attivo in DB: è lo stato misurato dell'ambiente locale, dove le righe
        // Ollama di SystemConfiguration.AiModelConfigurations sono IsActive=false. È per questo che
        // il fallback hardcoded — e quindi la variabile d'ambiente — è il percorso effettivo.
        var modelConfig = new Mock<IAiModelConfigurationRepository>();
        modelConfig
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AiModelConfiguration>());

        return new LlmProviderSelector(
            new[] { ollama.Object },
            new Mock<ILlmRoutingStrategy>().Object,
            circuitBreaker.Object,
            Options.Create(new AiProviderSettings()),
            modelConfig.Object,
            new LoggerFactory().CreateLogger<LlmProviderSelector>());
    }

    private static List<(string File, string Value)> ComposeChatModelDefaults()
    {
        var infra = Path.Combine(FindRepositoryRoot(), "infra");
        var result = new List<(string, string)>();

        foreach (var name in new[] { "docker-compose.yml", "compose.dev.yml" })
        {
            var path = Path.Combine(infra, name);
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var line in File.ReadLines(path))
            {
                const string marker = "${OLLAMA_CHAT_MODEL:-";
                var start = line.IndexOf(marker, StringComparison.Ordinal);
                if (start < 0)
                {
                    continue;
                }

                var valueStart = start + marker.Length;
                var end = line.IndexOf('}', valueStart);
                if (end > valueStart)
                {
                    result.Add((name, line[valueStart..end]));
                }
            }
        }

        return result;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("i test devono girare dentro il repository");
        return dir!.FullName;
    }
}
