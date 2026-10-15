using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Observability;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Un embedding della domanda fallito a monte è un braccio vettoriale non eseguito per ogni gioco
/// cercato, e va contato come tale (#3786, #4168 5e-c-2).
/// </summary>
/// <remarks>
/// Prima della 5e-c-2 lo contava <c>HybridSearchService</c>, a cui il vettore fallito arrivava gioco
/// per gioco. Ora il passo per gioco ripiega sul solo lessicale senza mai interrogare il braccio
/// vettoriale, quindi il conteggio sta qui: altrimenti un servizio di embedding giù diventerebbe
/// una risposta interamente lessicale che nessun allarme vede.
/// </remarks>
[Collection("VectorArmMetrics")]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class MultiGameHybridSearchServiceVectorArmMetricTests
{
    private readonly PerGameSearchStub _perGame = new();

    [Fact]
    public async Task WhenTheEmbeddingFailedUpstream_EachHybridGameRecordsAFailedVectorArm()
    {
        using var probe = new VectorArmOutcomeProbe();
        var gameIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();

        await CreateSut(embeddingSucceeds: false).SearchAsync("come si prepara Catan?", gameIds, 10, PerGameSearchStub.Seed);

        probe.Count(MeepleAiMetrics.RagArmOutcome.Failed).Should().Be(3, "un braccio vettoriale non eseguito per gioco");
    }

    [Fact]
    public async Task WhenTheEmbeddingFailedUpstream_ASemanticSearchRecordsAFailedVectorArmPerGame()
    {
        using var probe = new VectorArmOutcomeProbe();
        var gameIds = Enumerable.Range(0, 2).Select(_ => Guid.NewGuid()).ToArray();

        await CreateSut(embeddingSucceeds: false)
            .SearchAsync("come si prepara Catan?", gameIds, 10, PerGameSearchStub.Seed, mode: SearchMode.Semantic);

        probe.Count(MeepleAiMetrics.RagArmOutcome.Failed).Should().Be(2);
    }

    [Fact]
    public async Task AKeywordSearch_RecordsNoVectorOutcome()
    {
        // Il modo lessicale non ha un braccio vettoriale: nessun esito da contare, riuscito o no.
        using var probe = new VectorArmOutcomeProbe();

        await CreateSut(embeddingSucceeds: false)
            .SearchAsync("catan setup", new[] { Guid.NewGuid() }, 10, PerGameSearchStub.Seed, mode: SearchMode.Keyword);

        probe.Total.Should().Be(0);
    }

    [Fact]
    public async Task WhenTheEmbeddingSucceeds_TheServiceRecordsNothingOfItsOwn()
    {
        // Con il vettore in mano l'esito lo conta il recupero per gioco (SearchQueryHandler), qui
        // simulato: contarlo anche qui lo raddoppierebbe.
        using var probe = new VectorArmOutcomeProbe();

        await CreateSut(embeddingSucceeds: true).SearchAsync("come si prepara Catan?", new[] { Guid.NewGuid() }, 10, PerGameSearchStub.Seed);

        probe.Total.Should().Be(0);
    }

    private MultiGameHybridSearchService CreateSut(bool embeddingSucceeds)
    {
        var embeddings = new Mock<IEmbeddingService>();
        embeddings
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingSucceeds
                ? EmbeddingResult.CreateSuccess(new List<float[]> { new[] { 0.11f, 0.22f, 0.33f } })
                : EmbeddingResult.CreateFailure("embedding service unreachable"));

        return new MultiGameHybridSearchService(
            _perGame.ScopeFactory(),
            embeddings.Object,
            NullLogger<MultiGameHybridSearchService>.Instance);
    }
}
