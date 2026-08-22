using Api.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.EmbeddingProviders;

/// <summary>
/// The e5 <c>query:</c> prefix is behind a runtime switch (#3737).
/// </summary>
/// <remarks>
/// <para>
/// The prefix is correct per the e5 model card and yet, measured on the gate corpus, it moved the
/// semantic count from 10/11 to 8/11 — the previous 10/11 partly depended on the wrong encoding,
/// which the lexical arm compensated. Getting back from that cost a revert (#3747) plus a redeploy.
/// </para>
/// <para>
/// So the switch exists to make the next attempt cheap rather than to hedge the fix: with it, a red
/// gate is a config flip. It is DB-backed via <see cref="IConfigurationService"/> (5-minute cache),
/// and <b>off</b> when the row is absent — deploying this code changes nothing until someone turns
/// it on deliberately.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3737")]
public sealed class EmbeddingServiceQueryPrefixGateTests
{
    private const string GateKey = "Embedding:E5QueryPrefixEnabled";

    private static readonly EmbeddingProviderResult Success =
        EmbeddingProviderResult.CreateSuccess(new List<float[]> { new[] { 0.1f, 0.2f } }, "test-model");

    [Fact]
    public async Task QueryPurpose_WithTheGateOff_ReachesTheProviderAsPassage()
    {
        var (service, seen) = CreateService(gate: false);

        await service.GenerateEmbeddingAsync("how do I set up Catan?", EmbeddingPurpose.Query);

        seen.Value.Should().Be(EmbeddingPurpose.Passage,
            "con l'interruttore spento il servizio deve comportarsi come prima di #3737");
    }

    [Fact]
    public async Task QueryPurpose_WithNoConfigurationRow_ReachesTheProviderAsPassage()
    {
        // Riga assente = spento. Un default che si accendesse da solo renderebbe il rollout
        // implicito: il deploy cambierebbe il retrieval senza che nessuno lo abbia deciso.
        var (service, seen) = CreateService(gate: null);

        await service.GenerateEmbeddingAsync("how do I set up Catan?", EmbeddingPurpose.Query);

        seen.Value.Should().Be(EmbeddingPurpose.Passage);
    }

    [Fact]
    public async Task QueryPurpose_WithTheGateOn_ReachesTheProviderAsQuery()
    {
        var (service, seen) = CreateService(gate: true);

        await service.GenerateEmbeddingAsync("how do I set up Catan?", EmbeddingPurpose.Query);

        seen.Value.Should().Be(EmbeddingPurpose.Query);
    }

    [Fact]
    public async Task PassagePurpose_IsNeverAffectedByTheGate()
    {
        // L'ingestione non passa dall'interruttore: un chunk codificato `query:` costerebbe un
        // re-bake completo, che è il danno peggiore possibile su questo percorso.
        var (service, seen) = CreateService(gate: true);

        await service.GenerateEmbeddingAsync("Catan is a board game about settling an island.", EmbeddingPurpose.Passage);

        seen.Value.Should().Be(EmbeddingPurpose.Passage);
    }

    [Fact]
    public async Task PassagePurpose_DoesNotEvenReadTheConfiguration()
    {
        var config = new Mock<IConfigurationService>();
        var (service, _) = CreateService(gate: true, configuration: config);

        await service.GenerateEmbeddingAsync("Catan is a board game about settling an island.", EmbeddingPurpose.Passage);

        config.Verify(
            c => c.GetValueAsync<bool?>(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<string?>()),
            Times.Never,
            "l'ingestione gira in batch: una lettura per chunk sarebbe un costo pagato per una decisione che non la riguarda");
    }

    [Fact]
    public async Task TheLanguageOverload_HonoursTheGateToo()
    {
        // Due handler usano la variante con `language`. Un interruttore che copra un solo overload
        // lascerebbe metà delle query sul lato sbagliato senza che si veda dal call site.
        var (service, seen) = CreateService(gate: false);

        await service.GenerateEmbeddingAsync("Come si prepara Catan?", "it", EmbeddingPurpose.Query);

        seen.Value.Should().Be(EmbeddingPurpose.Passage);
    }

    // --- helpers --------------------------------------------------------------

    private sealed class Captured
    {
        public EmbeddingPurpose? Value { get; set; }
    }

    private static (EmbeddingService Service, Captured Seen) CreateService(
        bool? gate,
        Mock<IConfigurationService>? configuration = null)
    {
        var seen = new Captured();

        var primary = new Mock<IEmbeddingProvider>();
        primary.SetupGet(p => p.ProviderName).Returns("Primary");
        primary.SetupGet(p => p.ModelName).Returns("test-model");
        primary.SetupGet(p => p.Dimensions).Returns(768);
        primary
            .Setup(p => p.GenerateBatchEmbeddingsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<EmbeddingPurpose>(),
                It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<string> _, string _, EmbeddingPurpose purpose, CancellationToken _) => seen.Value = purpose)
            .ReturnsAsync(Success);

        var factory = new Mock<IEmbeddingProviderFactory>();
        factory.Setup(f => f.GetPrimaryProvider()).Returns(primary.Object);
        factory.Setup(f => f.GetFallbackProvider()).Returns((IEmbeddingProvider?)null);

        var config = configuration ?? new Mock<IConfigurationService>();
        config
            .Setup(c => c.GetValueAsync<bool?>(GateKey, It.IsAny<bool?>(), It.IsAny<string?>()))
            .ReturnsAsync(gate);

        var embeddingConfig = new EmbeddingConfiguration
        {
            Provider = EmbeddingProviderType.OllamaNomic,
            EnableFallback = true,
            BatchSize = 10
        };

        var service = new EmbeddingService(
            factory.Object,
            Options.Create(embeddingConfig),
            NullLogger<EmbeddingService>.Instance,
            config.Object);

        return (service, seen);
    }
}
