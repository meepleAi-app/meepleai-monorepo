using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Observability;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Il braccio vettoriale del recupero per gioco dichiara il proprio esito, e in modo ibrido un suo
/// guasto degrada a solo lessicale invece di far cadere la ricerca (#3786, #4168 5e-c-2).
/// </summary>
/// <remarks>
/// <para>
/// Finché la ricerca fra giochi passava da <c>HybridSearchService</c>, era lì che il braccio
/// vettoriale veniva catturato e contato: su staging 428 ricerche per gioco su 1759 avevano perso il
/// braccio vettoriale in silenzio, e la metrica esiste per vederlo. Con la 5e-c-2 il passo per gioco
/// passa da <see cref="SearchQueryHandler"/>: senza queste due proprietà un guasto del braccio
/// vettoriale toglierebbe l'intero gioco dal pool, lessicale compreso, e l'allarme
/// <c>RagVectorArmDegraded</c> non lo vedrebbe più.
/// </para>
/// <para>
/// Nella collection <c>VectorArmMetrics</c> perché il contatore è di processo.
/// </para>
/// </remarks>
[Collection("VectorArmMetrics")]
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SearchQueryHandlerVectorArmTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestGameId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string TestQuery = "come si prepara la partita?";

    [Fact]
    public async Task Hybrid_WhenTheVectorArmThrows_AnswersFromTheKeywordArm()
    {
        var pdfId = Guid.NewGuid();
        var handler = CreateHandler(ThrowingRepository(), KeywordReturning(pdfId, chunkIndex: 4));

        var result = await handler.Handle(Query("hybrid"), TestCancellationToken);

        var dto = result.Should().ContainSingle("il braccio lessicale resta: il guasto del vettoriale degrada, non toglie il gioco").Subject;
        dto.PdfDocumentId.Should().Be(pdfId.ToString());
        dto.ChunkIndex.Should().Be(4);
        dto.VectorScore.Should().BeNull();
    }

    [Fact]
    public async Task Hybrid_WhenTheVectorArmThrows_RecordsFailed_NotEmpty()
    {
        using var probe = new VectorArmOutcomeProbe();
        var handler = CreateHandler(ThrowingRepository(), KeywordReturning(Guid.NewGuid(), chunkIndex: 0));

        await handler.Handle(Query("hybrid"), TestCancellationToken);

        probe.Count(MeepleAiMetrics.RagArmOutcome.Failed).Should().Be(1);
        probe.Count(MeepleAiMetrics.RagArmOutcome.Empty).Should().Be(0,
            "una ricerca fallita non è una ricerca senza risultati: confonderle è il difetto #3786");
    }

    [Fact]
    public async Task AVectorSearchWithResults_IsRecordedAsHit()
    {
        using var probe = new VectorArmOutcomeProbe();
        var handler = CreateHandler(RepositoryReturningOnePassage(), KeywordReturningNothing());

        await handler.Handle(Query("hybrid"), TestCancellationToken);

        probe.Count(MeepleAiMetrics.RagArmOutcome.Hit).Should().Be(1);
        probe.Total.Should().Be(1, "un esito per ricerca: è il denominatore della copertura");
    }

    [Fact]
    public async Task AnEmptyVectorSearch_IsRecordedAsEmpty()
    {
        // Esito legittimo: un gioco senza contenuto indicizzato, o senza chunk sopra la soglia.
        using var probe = new VectorArmOutcomeProbe();
        var handler = CreateHandler(EmptyRepository(), KeywordReturningNothing());

        await handler.Handle(Query("vector"), TestCancellationToken);

        probe.Count(MeepleAiMetrics.RagArmOutcome.Empty).Should().Be(1);
        probe.Total.Should().Be(1);
    }

    [Fact]
    public async Task VectorOnly_WhenTheSearchThrows_RecordsFailed_AndStillFails()
    {
        // Solo vettoriale non c'è un altro braccio su cui ripiegare: la ricerca fallisce come prima,
        // ma il guasto resta contato.
        using var probe = new VectorArmOutcomeProbe();
        var handler = CreateHandler(ThrowingRepository(), KeywordReturningNothing());

        var act = () => handler.Handle(Query("vector"), TestCancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        probe.Count(MeepleAiMetrics.RagArmOutcome.Failed).Should().Be(1);
    }

    [Fact]
    public async Task KeywordOnly_RecordsNoVectorOutcome()
    {
        // Il modo solo lessicale non ha un braccio vettoriale: contarlo gonfierebbe il denominatore.
        using var probe = new VectorArmOutcomeProbe();
        var handler = CreateHandler(ThrowingRepository(), KeywordReturning(Guid.NewGuid(), chunkIndex: 0));

        await handler.Handle(Query("keyword"), TestCancellationToken);

        probe.Total.Should().Be(0);
    }

    [Fact]
    public async Task Hybrid_WhenTheCallerCancels_TheCancellationIsNotSwallowed()
    {
        // Il ripiego vale per un guasto, non per un client che se n'è andato: proseguire col
        // lessicale per una richiesta annullata sarebbe lavoro per nessuno.
        using var cts = new CancellationTokenSource();
        var repository = new Mock<IEmbeddingRepository>();
        repository
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await cts.CancelAsync();
                throw new OperationCanceledException(cts.Token);
            });
        var handler = CreateHandler(repository, KeywordReturning(Guid.NewGuid(), chunkIndex: 0));

        var act = () => handler.Handle(Query("hybrid"), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- helpers --------------------------------------------------------------

    private static SearchQuery Query(string mode) => new(
        GameId: TestGameId,
        Query: TestQuery,
        Access: SearchAccess.AlreadyChecked,
        TopK: 5,
        MinScore: 0.0,
        SearchMode: mode,
        QueryVector: new[] { 0.1f, 0.2f, 0.3f });

    private static Mock<IEmbeddingRepository> ThrowingRepository()
    {
        var mock = new Mock<IEmbeddingRepository>();
        mock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("A second operation was started on this context instance"));
        return mock;
    }

    private static Mock<IEmbeddingRepository> EmptyRepository()
    {
        var mock = new Mock<IEmbeddingRepository>();
        mock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding>());
        return mock;
    }

    private static Mock<IEmbeddingRepository> RepositoryReturningOnePassage()
    {
        var embedding = new Embedding(
            id: Guid.NewGuid(), vectorDocumentId: Guid.NewGuid(), textContent: "Preparazione: ogni giocatore pesca due carte.",
            vector: new Vector(new float[] { 0.1f, 0.2f, 0.3f }), model: "test-model",
            chunkIndex: 1, pageNumber: 1, language: "it", pdfDocumentId: Guid.NewGuid());
        var mock = new Mock<IEmbeddingRepository>();
        mock
            .Setup(r => r.SearchByVectorWithScoresAsync(
                It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ScoredEmbedding>)new List<ScoredEmbedding> { new(embedding, 0.8) });
        return mock;
    }

    private static Mock<IKeywordSearchService> KeywordReturning(Guid pdfId, int chunkIndex) =>
        KeywordMock(new KeywordSearchResult
        {
            ChunkId = $"{pdfId}_{chunkIndex}",
            Content = "Preparazione della partita.",
            PdfDocumentId = pdfId.ToString(),
            GameId = TestGameId,
            ChunkIndex = chunkIndex,
            PageNumber = 2,
            RelevanceScore = 0.6f,
        });

    private static Mock<IKeywordSearchService> KeywordReturningNothing() => KeywordMock();

    private static Mock<IKeywordSearchService> KeywordMock(params KeywordSearchResult[] results)
    {
        var mock = new Mock<IKeywordSearchService>();
        mock
            .Setup(k => k.SearchAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>(),
                It.IsAny<List<string>?>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results.ToList());
        return mock;
    }

    private static SearchQueryHandler CreateHandler(
        Mock<IEmbeddingRepository> repository,
        Mock<IKeywordSearchService> keyword) =>
        new(
            repository.Object,
            new VectorSearchDomainService(),
            new RrfFusionDomainService(),
            new Mock<IEmbeddingService>(MockBehavior.Strict).Object,
            keyword.Object,
            new Mock<IRagAccessService>().Object,
            new Mock<ILogger<SearchQueryHandler>>().Object);
}
