using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// La riga di diagnostica <c>[RAG-TUNE]</c> è osservabile quando serve e gratuita quando no (#3737).
/// </summary>
/// <remarks>
/// Esiste perché fra il 2026-08-17 e il 2026-08-18 tre configurazioni della fusione sono state
/// provate contro il gate (10/11 → 8/11 → 5/11) senza mai poter vedere i segnali su cui la fusione
/// decideva: dall'esterno si osserva solo il top-3 finale. Questi test pinnano le due proprietà che
/// rendono la strumentazione accettabile in produzione — emette quando la categoria è a Debug, e
/// <b>non serializza nulla</b> quando non lo è.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class MultiGameHybridSearchServiceTuningLogTests
{
    [Fact]
    public async Task WhenDebugIsDisabled_NoTuningLineIsEmitted()
    {
        // La proprietà che conta in produzione: la guardia IsEnabled precede ogni allocazione.
        var (sut, logger) = CreateSut(debugEnabled: false);

        await sut.SearchAsync("how do I set up Catan?", new[] { Guid.NewGuid() }, limit: 5);

        LogCallsAt(logger, LogLevel.Debug).Should().Be(0);
    }

    [Fact]
    public async Task WhenDebugIsEnabled_ExactlyOneTuningLinePerQuery()
    {
        var (sut, logger) = CreateSut(debugEnabled: true);

        await sut.SearchAsync("how do I set up Catan?", new[] { Guid.NewGuid() }, limit: 5);

        LogCallsAt(logger, LogLevel.Debug).Should().Be(1,
            "il consumatore estrae una riga per query: due righe romperebbero l'associazione query -> aggregato");
    }

    [Fact]
    public async Task TheTuningPayloadCarriesTheLanguageOfEachCandidate()
    {
        // #3740: senza questo campo il banco offline non può raggruppare i candidati per lingua, ed
        // è la domanda a cui il ciclo precedente non ha saputo rispondere — «il vettoriale, con il
        // prefisso corretto, attrae il contenuto italiano di qualunque gioco?». Il dato esisteva in
        // colonna e si perdeva due volte: non era nella SELECT dell'adapter e non era nel dump.
        var (sut, logger) = CreateSut(debugEnabled: true, candidateLanguage: "it");

        await sut.SearchAsync("come si prepara il tabellone di Catan?", new[] { Guid.NewGuid() }, limit: 5);

        TuningPayload(logger).Should().Contain("\"l\":\"it\"");
    }

    [Fact]
    public void TheLogPrefixIsStable()
    {
        // Il gate filtra su questa costante (grep -F). Cambiarla rompe lo script di estrazione,
        // quindi il valore è pinnato qui e non solo nel workflow.
        MultiGameHybridSearchService.TuningLogPrefix.Should().Be("[RAG-TUNE]");
    }

    /// <summary>The single [RAG-TUNE] line emitted at Debug, as the consumer reads it.</summary>
    private static string TuningPayload(Mock<ILogger<MultiGameHybridSearchService>> logger) =>
        logger.Invocations
            .Where(i => string.Equals(i.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal)
                        && i.Arguments.Count > 2
                        && i.Arguments[0] is LogLevel.Debug)
            .Select(i => i.Arguments[2]?.ToString() ?? string.Empty)
            .Single();

    private static int LogCallsAt(Mock<ILogger<MultiGameHybridSearchService>> logger, LogLevel level) =>
        logger.Invocations.Count(i =>
            string.Equals(i.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal)
            && i.Arguments.Count > 0
            && i.Arguments[0] is LogLevel actual
            && actual == level);

    private static (MultiGameHybridSearchService Sut, Mock<ILogger<MultiGameHybridSearchService>> Logger)
        CreateSut(bool debugEnabled, string candidateLanguage = "en")
    {
        var logger = new Mock<ILogger<MultiGameHybridSearchService>>();
        logger.Setup(l => l.IsEnabled(LogLevel.Debug)).Returns(debugEnabled);
        logger.Setup(l => l.IsEnabled(It.Is<LogLevel>(lv => lv != LogLevel.Debug))).Returns(true);

        var hybrid = new Mock<IHybridSearchService>();
        hybrid
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<SearchMode>(), It.IsAny<int>(),
                It.IsAny<List<Guid>?>(), It.IsAny<float>(), It.IsAny<float>(),
                It.IsAny<double>(), It.IsAny<GameBookRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                new()
                {
                    ChunkId = $"{Guid.NewGuid()}_0",
                    Content = "chunk",
                    PdfDocumentId = Guid.NewGuid().ToString(),
                    GameId = Guid.NewGuid(),
                    ChunkIndex = 0,
                    PageNumber = 1,
                    HybridScore = 0.02f,
                    VectorScore = 0.83f,
                    KeywordScore = 0.10f,
                    VectorRank = 1,
                    KeywordRank = 1,
                    MatchedTerms = new List<string>(),
                    Mode = SearchMode.Hybrid,
                    RoleTags = GameBookRole.None,
                    Language = candidateLanguage
                }
            });

        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IHybridSearchService))).Returns(hybrid.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return (new MultiGameHybridSearchService(scopeFactory.Object, logger.Object), logger);
    }
}
