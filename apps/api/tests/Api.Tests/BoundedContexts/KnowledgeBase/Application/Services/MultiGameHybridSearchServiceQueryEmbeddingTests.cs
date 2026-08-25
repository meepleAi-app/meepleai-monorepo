using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Api.Helpers;
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
            Times.Exactly(2),
            "i tentativi restano quelli fissi a monte (uno piu' il ritentativo): il fallimento non va ripetuto PER GIOCO, che con 12 giochi darebbe 12");
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    public async Task AnInvalidQuery_DoesNotReachTheEmbeddingService(string query)
    {
        // Finche' il vettore nasceva dentro il ciclo per-gioco, era HybridSearchService.SearchAsync
        // a validare per primo: una query invalida non raggiungeva mai il servizio di embedding.
        // Spostando il calcolo a monte, quella protezione va ricreata a monte — altrimenti questa
        // correzione, nata per togliere chiamate, ne aggiungerebbe una dove prima erano zero.
        var sut = CreateSut(embeddingSucceeds: true);

        var result = await sut.SearchAsync(query, new[] { Guid.NewGuid() }, limit: 10);

        result.Should().BeEmpty();
        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AQueryOverTheLengthLimit_DoesNotReachTheEmbeddingService()
    {
        // QueryValidator.MaxQueryLength e' dichiarato «security: prevent DoS». Senza la validazione
        // a monte non sarebbe una chiamata sprecata ma una difesa aggirata: il testo oltre il
        // limite raggiungerebbe un servizio esterno che prima era protetto dal controllo per-gioco.
        var tooLong = new string('x', QueryValidator.MaxQueryLength + 1);
        var sut = CreateSut(embeddingSucceeds: true);

        var result = await sut.SearchAsync(tooLong, new[] { Guid.NewGuid() }, limit: 10);

        result.Should().BeEmpty();
        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ACancelledRequest_StopsInsteadOfFanningOut()
    {
        // Il chiamante SSE passa HttpContext.RequestAborted. Inghiottire la cancellazione qui
        // significherebbe proseguire il fan-out per un client che se n'e' andato: ~160 warning
        // con il testo della query, e poi l'assemblaggio del prompt e la chiamata all'LLM.
        using var cts = new CancellationTokenSource();
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));
        var sut = CreateSut(embeddingSucceeds: true, configureEmbedding: false);
        await cts.CancelAsync();

        var act = async () => await sut.SearchAsync(
            "come si prepara Catan?",
            Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray(),
            limit: 10,
            cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _hybridSearchMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ATransientFailure_IsRetriedOnceUpstream()
    {
        // Prima di questa correzione ogni gioco generava il proprio vettore, quindi un flake
        // singolo degradava UN gioco e lasciava sani gli altri ~159. Con un solo calcolo lo stesso
        // flake li degrada tutti — e in SearchMode.Semantic la ricerca restituisce zero risultati
        // invece di un insieme degradato. Il ritentativo va a monte, dove costa 2 chiamate invece
        // di 160.
        var calls = 0;
        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++calls == 1
                ? EmbeddingResult.CreateFailure("transient timeout")
                : EmbeddingResult.CreateSuccess(new List<float[]> { QueryVector }));

        var seen = new List<QueryEmbedding?>();
        var sut = CreateSut(embeddingSucceeds: true, onSearch: e => seen.Add(e), configureEmbedding: false);

        await sut.SearchAsync("come si prepara Catan?", new[] { Guid.NewGuid(), Guid.NewGuid() }, limit: 10);

        calls.Should().Be(2, "un fallimento transitorio va ritentato una volta, non zero e non per gioco");
        seen.Should().AllSatisfy(e => e!.Succeeded.Should().BeTrue(),
            "il secondo tentativo e' riuscito: i giochi devono ricevere il vettore, non una degradazione");
    }

    [Fact]
    public async Task APersistentFailure_StopsAfterTheRetry_AndDoesNotScaleWithTheGameCount()
    {
        var gameIds = Enumerable.Range(0, 30).Select(_ => Guid.NewGuid()).ToArray();
        var sut = CreateSut(embeddingSucceeds: false);

        await sut.SearchAsync("come si prepara Catan?", gameIds, limit: 10);

        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "il numero di tentativi e' fisso: non deve crescere col numero di giochi accessibili");
    }

    // --- helpers --------------------------------------------------------------

    private MultiGameHybridSearchService CreateSut(
        bool embeddingSucceeds,
        Action<QueryEmbedding?>? onSearch = null,
        bool configureEmbedding = true)
    {
        // configureEmbedding: false quando il test ha gia' impostato il proprio comportamento
        // sull'embedding (ritentativo, cancellazione) — sovrascriverlo qui lo annullerebbe.
        if (configureEmbedding)
        {
            _embeddingMock
                .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<EmbeddingPurpose>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(embeddingSucceeds
                    ? EmbeddingResult.CreateSuccess(new List<float[]> { QueryVector })
                    : EmbeddingResult.CreateFailure("embedding service unreachable"));
        }

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
