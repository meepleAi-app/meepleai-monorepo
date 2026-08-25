using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// L'embedding della query è calcolato una volta sola per un fan-out cross-gioco (#3786).
/// </summary>
/// <remarks>
/// <para>
/// Il vettore di una query non dipende dal gioco, ma nasceva dentro
/// <c>HybridSearchService.ExecuteVectorSearchAsync</c> — cioè dentro il ciclo per-gioco — quindi un
/// <c>ask/global</c> su ~160 giochi accessibili lo ricalcolava ~160 volte. Misurate su staging
/// <b>1546 richieste al servizio di embedding per 11 query</b>, a ~1,4 s l'una, su un percorso che
/// quelle chiamate dominano interamente.
/// </para>
/// <para>
/// Il conteggio delle chiamate è l'asserzione che conta, e non il tempo: una misura di durata su
/// un mock non dice nulla, mentre il numero di chiamate è esattamente la grandezza che l'issue
/// riporta e che la correzione riduce.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3786")]
public sealed class MultiGameHybridSearchServiceQueryEmbeddingTests
{
    private readonly Mock<IHybridSearchService> _hybridSearchMock = new();
    private readonly Mock<IEmbeddingService> _embeddingMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();

    private static readonly float[] QueryVector = { 0.11f, 0.22f, 0.33f };

    [Fact]
    public async Task ManyGames_GenerateTheQueryEmbeddingExactlyOnce()
    {
        var gameIds = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToArray();
        var sut = CreateSut(embeddingSucceeds: true);

        await sut.SearchAsync("come si prepara Catan?", gameIds, limit: 10);

        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "il vettore della query non dipende dal gioco: 40 giochi devono costare una chiamata, non 40");
    }

    [Fact]
    public async Task ManyGames_ReceiveTheSameEmbeddingInstance()
    {
        var gameIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var seen = new List<QueryEmbedding?>();
        var sut = CreateSut(embeddingSucceeds: true, onSearch: e => seen.Add(e));

        await sut.SearchAsync("come si prepara Catan?", gameIds, limit: 10);

        seen.Should().HaveCount(5);
        seen.Should().AllSatisfy(e => e!.Succeeded.Should().BeTrue());
        seen.Distinct().Should().HaveCount(1, "un unico calcolo deve produrre un unico oggetto condiviso da tutti i giochi");
        seen[0]!.Vector.Should().Equal(QueryVector);
    }

    [Fact]
    public async Task WhenTheEmbeddingFails_TheFailureIsDeclaredOnce_AndNotRetriedPerGame()
    {
        // La distinzione fra «non fornito» e «fornito ma fallito» esiste per questo: senza, un
        // servizio di embedding irraggiungibile avrebbe prodotto un tentativo per gioco — cioè
        // le ~160 chiamate che questa correzione toglie, riproposte nel caso peggiore.
        var gameIds = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid()).ToArray();
        var seen = new List<QueryEmbedding?>();
        var sut = CreateSut(embeddingSucceeds: false, onSearch: e => seen.Add(e));

        var result = await sut.SearchAsync("come si prepara Catan?", gameIds, limit: 10);

        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "il tentativo fallito non va ripetuto per gioco");
        seen.Should().HaveCount(12);
        seen.Should().AllSatisfy(e => e!.Succeeded.Should().BeFalse(),
            "ogni gioco deve poter registrare la degradazione, che e' il segnale introdotto da #3793");
        result.Should().NotBeNull("la ricerca prosegue solo-lessicale: il fallimento degrada, non aborta");
    }

    [Fact]
    public async Task TheQueryCarriesTheE5QueryPurpose()
    {
        // #3737: spostare la generazione a monte non deve perdere il purpose. Un embedding di
        // query codificato `passage:` porterebbe il manuale atteso dal rango 1 al rango 10 sul
        // corpus reale — e qui il difetto sarebbe globale invece che per gioco.
        var sut = CreateSut(embeddingSucceeds: true);

        await sut.SearchAsync("come si prepara Catan?", new[] { Guid.NewGuid() }, limit: 10);

        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync("come si prepara Catan?", EmbeddingPurpose.Query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task KeywordMode_DoesNotGenerateAnEmbeddingAtAll()
    {
        // Il modo Keyword non usa il braccio vettoriale: una chiamata HTTP spesa per un vettore
        // che nessuno legge sarebbe un costo introdotto da questa correzione, non tolto.
        var gameIds = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var seen = new List<QueryEmbedding?>();
        var sut = CreateSut(embeddingSucceeds: true, onSearch: e => seen.Add(e));

        await sut.SearchAsync("catan setup", gameIds, limit: 10, mode: SearchMode.Keyword);

        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Never);
        seen.Should().AllSatisfy(e => e.Should().BeNull(),
            "«non fornito» e' lo stato giusto: il percorso per-gioco non deve ne' usarlo ne' considerarlo fallito");
    }

    [Fact]
    public async Task NoAccessibleGames_DoesNotGenerateAnEmbedding()
    {
        var sut = CreateSut(embeddingSucceeds: true);

        var result = await sut.SearchAsync("catan setup", Array.Empty<Guid>(), limit: 10);

        result.Should().BeEmpty();
        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "l'uscita anticipata su zero giochi precede il calcolo, altrimenti la correzione aggiungerebbe una chiamata a una richiesta che non cerca nulla");
    }

    // --- helpers --------------------------------------------------------------

    private MultiGameHybridSearchService CreateSut(
        bool embeddingSucceeds,
        Action<QueryEmbedding?>? onSearch = null)
    {
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingSucceeds
                ? EmbeddingResult.CreateSuccess(new List<float[]> { QueryVector })
                : EmbeddingResult.CreateFailure("embedding service unreachable"));

        _hybridSearchMock
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<double>(),
                It.IsAny<GameBookRole>(),
                It.IsAny<QueryEmbedding?>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, Guid _, SearchMode _, int _, List<Guid>? _, float _, float _, double _,
                       GameBookRole _, QueryEmbedding? precomputed, CancellationToken _) => onSearch?.Invoke(precomputed))
            .ReturnsAsync(new List<HybridSearchResult>());

        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IHybridSearchService))).Returns(_hybridSearchMock.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new MultiGameHybridSearchService(
            _scopeFactoryMock.Object,
            _embeddingMock.Object,
            NullLogger<MultiGameHybridSearchService>.Instance);
    }
}
