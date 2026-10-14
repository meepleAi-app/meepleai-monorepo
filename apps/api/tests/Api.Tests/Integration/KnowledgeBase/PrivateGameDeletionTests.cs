using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// La fixture di <see cref="PrivateGameDeletionTests"/>. Gli host di integrazione montano una
/// cache ibrida che richiama sempre la fabbrica; qui ce n'è una che ricorda davvero, perché
/// l'elenco «I miei documenti» in produzione sta in cache, e un test che non ha cache non può
/// dire se la cancellazione la invalida.
/// </summary>
/// <remarks>
/// Non è la cache vera. Quella di produzione (<c>HybridCacheService</c>) tiene le etichette in
/// Redis, che in questi host è finto. Questi test provano che la cancellazione chiede di togliere
/// le etichette giuste a una cache che rispetta il contratto di <c>IHybridCacheService</c>; che
/// la cache vera poi le tolga è un fatto suo, fuori da qui.
/// </remarks>
public sealed class PrivateGameDeletionHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "pg_deletion")
{
    internal RememberingHybridCache HybridCache { get; } = new();

    /// <summary>L'interruttore che fa fallire la cancellazione dei documenti: una pulizia che si ferma a metà.</summary>
    internal DocumentDeletionFailure DocumentDeletion { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHybridCacheService>();
            services.AddSingleton<IHybridCacheService>(HybridCache);
            services.AddSingleton(DocumentDeletion);
            services.AddTransient<IPipelineBehavior<DeleteKbDocumentCommand, MediatR.Unit>, FailDocumentDeletionWhenArmed>();
        }));
}

/// <summary>Vive quanto la classe di test: ogni test lo riporta a spento.</summary>
internal sealed class DocumentDeletionFailure
{
    private volatile bool _armed;
    private int _stillToLetThrough;
    private Guid? _onlyFor;

    /// <summary>Acceso, ogni cancellazione di documento fallisce. Spegnerlo azzera anche il conto e il bersaglio.</summary>
    public bool Armed
    {
        get => _armed;
        set
        {
            Interlocked.Exchange(ref _stillToLetThrough, 0);
            _onlyFor = null;
            _armed = value;
        }
    }

    /// <summary>Fa fallire solo la cancellazione di quel documento.</summary>
    public void ArmFor(Guid pdfId)
    {
        Interlocked.Exchange(ref _stillToLetThrough, 0);
        _onlyFor = pdfId;
        _armed = true;
    }

    /// <summary>Lascia passare le prime <paramref name="documents"/> cancellazioni, poi le fa fallire.</summary>
    public void ArmAfter(int documents)
    {
        Interlocked.Exchange(ref _stillToLetThrough, documents);
        _armed = true;
    }

    /// <summary>Dice se questa cancellazione deve fallire; se no, consuma uno dei passaggi concessi.</summary>
    public bool ShouldFail(Guid pdfId) =>
        _armed && (_onlyFor is { } target ? target == pdfId : Interlocked.Decrement(ref _stillToLetThrough) < 0);
}

/// <summary>
/// Sta dentro la transazione di <c>[AtomicAudit]</c>, al posto del gestore: quando l'interruttore
/// è acceso la cancellazione del documento fallisce come fallirebbe per un errore del database.
/// </summary>
internal sealed class FailDocumentDeletionWhenArmed(DocumentDeletionFailure failure)
    : IPipelineBehavior<DeleteKbDocumentCommand, MediatR.Unit>
{
    public Task<MediatR.Unit> Handle(
        DeleteKbDocumentCommand request, RequestHandlerDelegate<MediatR.Unit> next, CancellationToken cancellationToken) =>
        failure.ShouldFail(request.Id)
            ? throw new InvalidOperationException("La cancellazione del documento è fatta fallire dal test.")
            : next(cancellationToken);
}

/// <summary>Una cache ibrida in memoria che ricorda i valori e li toglie per etichetta.</summary>
internal sealed class RememberingHybridCache : IHybridCacheService
{
    private readonly ConcurrentDictionary<string, (object Value, string[] Tags)> _entries =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, int> _removalsByTag = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, Func<Task>> _whenRemoving = new(StringComparer.Ordinal);

    /// <summary>Quante volte è stato chiesto di togliere un'etichetta.</summary>
    public int RemovalsOf(string tag) => _removalsByTag.GetValueOrDefault(tag);

    /// <summary>
    /// Che cosa fare ogni volta che viene chiesto di togliere un'etichetta, prima di toglierla:
    /// serve a guardare il database nell'istante dell'invalidazione.
    /// </summary>
    public void WhenRemoving(string tag, Func<Task> probe) => _whenRemoving[tag] = probe;

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class
    {
        if (_entries.TryGetValue(cacheKey, out var hit))
        {
            return (T)hit.Value;
        }

        var value = await factory(ct);
        _entries[cacheKey] = (value, tags ?? []);
        return value;
    }

    public Task RemoveAsync(string cacheKey, CancellationToken ct = default)
    {
        _entries.TryRemove(cacheKey, out _);
        return Task.CompletedTask;
    }

    public async Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default)
    {
        _removalsByTag.AddOrUpdate(tag, 1, (_, count) => count + 1);
        if (_whenRemoving.TryGetValue(tag, out var probe))
        {
            await probe();
        }

        var removed = 0;
        foreach (var (key, entry) in _entries)
        {
            if (entry.Tags.Contains(tag, StringComparer.Ordinal) && _entries.TryRemove(key, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    public async Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default)
    {
        var removed = 0;
        foreach (var tag in tags)
        {
            removed += await RemoveByTagAsync(tag, ct);
        }

        return removed;
    }

    public Task<int> RemoveByTagAcrossReplicasAsync(string tag, CancellationToken ct = default) =>
        RemoveByTagAsync(tag, ct);

    public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default) =>
        Task.FromResult(new HybridCacheStats());

    public void Clear()
    {
        _entries.Clear();
        _removalsByTag.Clear();
        _whenRemoving.Clear();
    }
}

/// <summary>
/// Issue #4244 (consegna C): cancellato un gioco privato, del suo contenuto non resta niente —
/// le chat, i passaggi, il documento vettoriale, gli embedding, il PDF.
/// </summary>
/// <remarks>
/// <para>
/// La cancellazione del gioco è logica (<c>is_deleted</c>), quindi le chiavi esterne con
/// <c>ON DELETE CASCADE</c> verso <c>private_games</c> non scattano mai: senza una pulizia
/// esplicita il contenuto resterebbe nel database, irraggiungibile e per sempre.
/// </para>
/// <para>
/// Su Postgres, via HTTP. Ogni test ha un secondo gioco privato dello stesso proprietario che
/// fa da controllo: la pulizia non deve portarsi via nient'altro.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4244")]
public sealed partial class PrivateGameDeletionTests
    : IClassFixture<PrivateGameDeletionHostFixture>, IAsyncLifetime
{
    /// <summary>Quante righe di un gioco privato ci sono, tabella per tabella.</summary>
    private sealed record Content(int Threads, int Pdfs, int VectorDocuments, int Passages, int Embeddings);

    /// <summary>Che cosa c'è di un PDF: la riga e il suo indice.</summary>
    private sealed record PdfRows(bool Exists, int VectorDocuments, int Passages, int Embeddings);

    private static readonly Content Nothing = new(0, 0, 0, 0, 0);

    private readonly PrivateGameDeletionHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _gameId;
    private Guid _pdfId;
    private Guid _otherGameId;
    private Guid _otherPdfId;

    public PrivateGameDeletionTests(PrivateGameDeletionHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        // La cache e l'interruttore vivono quanto la classe: ogni test parte da vuota e da spento.
        _hostFixture.HybridCache.Clear();
        _hostFixture.DocumentDeletion.Armed = false;
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);

        (_gameId, _pdfId) = await SeedPrivateGameAsync(db, "Il mio prototipo",
            ("At setup each player draws five cards.", QueryVectorEmbeddings.Axis(1f, 0f)),
            ("The game ends when the deck runs out.", QueryVectorEmbeddings.Axis(0f, 1f)));
        (_otherGameId, _otherPdfId) = await SeedPrivateGameAsync(db, "Un altro prototipo",
            ("In the other prototype each player draws seven tiles.", QueryVectorEmbeddings.Axis(1f, 0f)));

        await OpenThreadAsync(_gameId);
        await OpenThreadAsync(_otherGameId);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task DeletingAPrivateGame_LeavesNoneOfItsContent()
    {
        // Il punto di partenza: se il gioco non avesse niente, «non resta niente» non proverebbe nulla.
        (await ContentOfAsync(_gameId)).Should().Be(
            new Content(Threads: 1, Pdfs: 1, VectorDocuments: 1, Passages: 2, Embeddings: 2));

        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
    }

    /// <summary>
    /// Non tutto pende da un PDF del gioco. Gli embedding non hanno una chiave esterna verso il
    /// documento vettoriale: una cancellazione finita a metà li lascia soli, legati al gioco dal
    /// solo <c>game_id</c>. E i passaggi nominano il gioco per conto loro, anche se il PDF non lo fa.
    /// </summary>
    [Fact]
    public async Task ContentThatHangsFromNoPdfOfTheGame_IsRemovedToo()
    {
        var detachedPdfId = await SeedContentOutsideThePdfsOfTheGameAsync(_gameId);
        (await ContentOfAsync(_gameId)).Should().Be(
            new Content(Threads: 1, Pdfs: 1, VectorDocuments: 2, Passages: 3, Embeddings: 4));

        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
        // Il PDF che non nomina il gioco è ancora lì: quelle righe non sono andate via con lui.
        (await PdfExistsAsync(detachedPdfId)).Should().BeTrue();
    }

    [Fact]
    public async Task DeletingAPrivateGame_LeavesTheOtherGamesOfTheOwnerAlone()
    {
        var before = await ContentOfAsync(_otherGameId);
        before.Should().Be(new Content(Threads: 1, Pdfs: 1, VectorDocuments: 1, Passages: 1, Embeddings: 1));

        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_otherGameId)).Should().Be(before);
    }

    [Fact]
    public async Task AnotherUser_CannotDeleteThePrivateGame_AndNothingIsRemoved()
    {
        var before = await ContentOfAsync(_gameId);
        string strangerToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (_, strangerToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        }

        var response = await DeleteGameAsync(_gameId, strangerToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_gameId)).Should().Be(before);
        (await IsDeletedAsync(_gameId)).Should().BeFalse();
    }

    // ─── Un PDF che un altro gioco usa ancora ───────────────────

    /// <summary>
    /// La deduplicazione non ricarica due volte lo stesso file: al secondo gioco dà un
    /// collegamento verso il PDF del primo (<c>CompleteChunkedUploadCommandHandler</c>). Per chi lo
    /// ha caricato quel regolamento è anche del secondo gioco: cancellare il primo non glielo
    /// deve togliere. È la regola di #2949 (un PDF collegato da più giochi si scollega, non si
    /// distrugge), che qui vale anche quando a lasciarlo è il gioco che lo aveva caricato.
    /// </summary>
    [Fact]
    public async Task APdfAnotherGameStillUses_IsNotDestroyed()
    {
        var usedByBoth = await SeedPdfUsedByBothGamesAsync();
        var before = await RowsOfPdfAsync(usedByBoth);
        before.Should().Be(new PdfRows(Exists: true, VectorDocuments: 1, Passages: 1, Embeddings: 1));

        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await RowsOfPdfAsync(usedByBoth)).Should().Be(before);
        // Quello che era solo del gioco se n'è andato comunque.
        (await RowsOfPdfAsync(_pdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
        (await ContentOfAsync(_gameId)).Threads.Should().Be(0);
    }

    /// <summary>
    /// Non resta per sempre: quando anche l'ultimo gioco che lo usa viene cancellato, il PDF se
    /// ne va con lui, e del primo gioco non resta più niente.
    /// </summary>
    [Fact]
    public async Task APdfAnotherGameStillUses_GoesWhenTheLastGameUsingItIsDeleted()
    {
        var usedByBoth = await SeedPdfUsedByBothGamesAsync();
        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RowsOfPdfAsync(usedByBoth)).Exists.Should().BeTrue();

        var response = await DeleteGameAsync(_otherGameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await RowsOfPdfAsync(usedByBoth)).Should().Be(new PdfRows(false, 0, 0, 0));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
        (await ContentOfAsync(_otherGameId)).Should().Be(Nothing);
    }

    /// <summary>
    /// I collegamenti di un gioco cancellato spariscono con lui: qui il gioco cancellato usava un
    /// PDF dell'altro gioco. Che spariscano lo dice il conteggio dei collegamenti vivi; che un
    /// collegamento da un gioco cancellato non trattenga un PDF lo prova, da solo,
    /// <see cref="ALinkLeftByAGameDeletedBefore_DoesNotKeepThePdfAlive"/>.
    /// </summary>
    [Fact]
    public async Task TheLinksOfTheDeletedGame_GoWithIt_AndKeepNothingAlive()
    {
        await LinkGameToPdfAsync(_gameId, _otherPdfId);
        (await LiveLinksFromAsync(_gameId)).Should().Be(1);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await LiveLinksFromAsync(_gameId)).Should().Be(0);
        // Il PDF dell'altro gioco è ancora suo…
        (await RowsOfPdfAsync(_otherPdfId)).Exists.Should().BeTrue();

        (await DeleteGameAsync(_otherGameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // …e se ne va con lui.
        (await RowsOfPdfAsync(_otherPdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
    }

    // ─── Chi tiene in vita un PDF ────────────────────────────────

    /// <summary>
    /// <c>POST /library/entity-links</c> lascia creare a chiunque un collegamento fra due id
    /// qualunque. Se bastasse un collegamento vivo, un altro utente che conosce l'id del PDF lo
    /// terrebbe nel database per sempre, dopo che chi l'ha caricato ha cancellato il gioco.
    /// </summary>
    [Fact]
    public async Task ALinkOfAnotherUser_DoesNotKeepThePdfAlive()
    {
        var (strangerId, strangersGameId) = await SeedStrangerWithAGameAsync();
        await LinkGameToPdfAsync(strangersGameId, _pdfId, ownerId: strangerId);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsOfPdfAsync(_pdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
    }

    /// <summary>Un collegamento che parte da un id che non è di nessun gioco non è un uso.</summary>
    [Fact]
    public async Task ALinkFromNoGameAtAll_DoesNotKeepThePdfAlive()
    {
        await LinkGameToPdfAsync(Guid.NewGuid(), _pdfId);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsOfPdfAsync(_pdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
    }

    /// <summary>
    /// Nemmeno se il collegamento è di chi ha caricato il PDF ma parte dal gioco di un altro: quel
    /// gioco non è suo, e nessuna sua cancellazione libererebbe più il PDF.
    /// </summary>
    [Fact]
    public async Task ALinkOfTheUploaderFromSomeoneElsesGame_DoesNotKeepThePdfAlive()
    {
        var (_, strangersGameId) = await SeedStrangerWithAGameAsync();
        await LinkGameToPdfAsync(strangersGameId, _pdfId);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsOfPdfAsync(_pdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
    }

    /// <summary>
    /// La deduplicazione collega anche a un gioco del catalogo: chi ha caricato lo stesso file per
    /// un gioco del catalogo lo usa ancora lì.
    /// </summary>
    [Fact]
    public async Task ALinkFromACatalogGameOfTheUploader_KeepsThePdf()
    {
        var catalogGameId = await SeedCatalogGameAsync(deleted: false);
        await LinkGameToPdfAsync(catalogGameId, _pdfId);
        var before = await RowsOfPdfAsync(_pdfId);
        before.Should().Be(new PdfRows(Exists: true, VectorDocuments: 1, Passages: 2, Embeddings: 2));

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsOfPdfAsync(_pdfId)).Should().Be(before);
        (await ContentOfAsync(_gameId)).Threads.Should().Be(0);
    }

    /// <summary>
    /// Per un gioco del catalogo la deduplicazione cerca il file fra i PDF di tutti gli utenti
    /// (<c>PdfDocumentRepository.FindByContentHashAsync</c>): se un altro carica lo stesso file per
    /// un gioco del catalogo, nasce un suo collegamento verso questo PDF privato. Non lo tiene in
    /// vita: chi l'ha caricato ha cancellato il gioco, e il PDF è suo.
    /// </summary>
    [Fact]
    public async Task ALinkOfAnotherUserFromACatalogGame_DoesNotKeepThePdfAlive()
    {
        var (strangerId, _) = await SeedStrangerWithAGameAsync();
        var catalogGameId = await SeedCatalogGameAsync(deleted: false);
        await LinkGameToPdfAsync(catalogGameId, _pdfId, ownerId: strangerId);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsOfPdfAsync(_pdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
    }

    /// <summary>Un gioco del catalogo cancellato non usa più niente, come un gioco privato cancellato.</summary>
    [Fact]
    public async Task ALinkFromACatalogGameThatIsGone_DoesNotKeepThePdfAlive()
    {
        var catalogGameId = await SeedCatalogGameAsync(deleted: true);
        await LinkGameToPdfAsync(catalogGameId, _pdfId);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsOfPdfAsync(_pdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
    }

    /// <summary>
    /// Ricaricare lo stesso file sullo stesso gioco gli dà un collegamento verso il proprio PDF
    /// (la deduplicazione non distingue). Quel collegamento non è «un altro gioco che lo usa»: il
    /// gioco che si sta cancellando non tiene in vita niente.
    /// </summary>
    [Fact]
    public async Task APdfTheGameLinksToItself_GoesWithTheGame()
    {
        await LinkGameToPdfAsync(_gameId, _pdfId);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsOfPdfAsync(_pdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
    }

    /// <summary>
    /// Attraverso un collegamento si arriva solo ai propri PDF. Qui il gioco privato di un altro
    /// utente, cancellato prima che questa pulizia esistesse, ha lasciato il suo PDF: collegarlo a
    /// un proprio gioco e cancellare il gioco non lo distrugge. Quel PDF non è di chi cancella.
    /// </summary>
    [Fact]
    public async Task ALinkToTheLeftoverPdfOfAnotherUser_DoesNotDestroyIt()
    {
        var leftover = await SeedLeftoverPdfOfAStrangerAsync();
        await LinkGameToPdfAsync(_gameId, leftover);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsOfPdfAsync(leftover)).Should().Be(
            new PdfRows(Exists: true, VectorDocuments: 1, Passages: 1, Embeddings: 1));
    }

    // ─── Una pulizia che si ferma a metà ─────────────────────────

    /// <summary>
    /// La pulizia non è in una transazione sola: se si ferma, il gioco resta e chi cancella riprova.
    /// Riprovare deve arrivare allo stesso stato. Qui l'altro gioco è l'ultimo a usare un PDF che
    /// il primo, già cancellato, aveva caricato: lo si raggiunge solo dai collegamenti, quindi i
    /// collegamenti devono essere ancora lì al secondo tentativo.
    /// </summary>
    [Fact]
    public async Task APurgeThatStopsHalfway_CanBeRepeated()
    {
        var usedByBoth = await SeedPdfUsedByBothGamesAsync();
        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RowsOfPdfAsync(usedByBoth)).Exists.Should().BeTrue();

        _hostFixture.DocumentDeletion.Armed = true;
        var failed = await DeleteGameAsync(_otherGameId, _token);

        failed.IsSuccessStatusCode.Should().BeFalse();
        (await IsDeletedAsync(_otherGameId)).Should().BeFalse();
        // Il gioco è ancora lì, e usa ancora quel PDF.
        (await LiveLinksFromAsync(_otherGameId)).Should().Be(1);
        (await RowsOfPdfAsync(usedByBoth)).Exists.Should().BeTrue();

        _hostFixture.DocumentDeletion.Armed = false;
        var retried = await DeleteGameAsync(_otherGameId, _token);

        retried.StatusCode.Should().Be(HttpStatusCode.NoContent, await retried.Content.ReadAsStringAsync(Ct));
        (await RowsOfPdfAsync(usedByBoth)).Should().Be(new PdfRows(false, 0, 0, 0));
        (await RowsOfPdfAsync(_otherPdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
        (await ContentOfAsync(_otherGameId)).Should().Be(Nothing);
        (await LiveLinksFromAsync(_otherGameId)).Should().Be(0);
    }

    /// <summary>
    /// E a metà per davvero: un documento se ne va, il successivo fallisce. Nel test qui sopra la
    /// pulizia si ferma al primo documento, quando non ha ancora distrutto niente. Qui l'altro
    /// gioco ha tre documenti — il suo, e due rimasti dal gioco già cancellato, raggiungibili solo
    /// dai collegamenti: qualunque sia l'ordine, dopo il fallimento ne resta almeno uno di questi.
    /// </summary>
    [Fact]
    public async Task APurgeThatDestroysOneDocumentAndFailsOnTheNext_CanBeRepeated()
    {
        var firstLeftover = await SeedPdfUsedByBothGamesAsync();
        var secondLeftover = await SeedPdfUsedByBothGamesAsync();
        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Guid[] documents = [_otherPdfId, firstLeftover, secondLeftover];

        _hostFixture.DocumentDeletion.ArmAfter(1);
        var failed = await DeleteGameAsync(_otherGameId, _token);

        failed.IsSuccessStatusCode.Should().BeFalse();
        (await IsDeletedAsync(_otherGameId)).Should().BeFalse();
        // Uno è andato, due no: la pulizia si è fermata a metà, e i collegamenti sono ancora lì.
        (await StillThereAsync(documents)).Should().Be(2);
        (await LiveLinksFromAsync(_otherGameId)).Should().Be(2);

        _hostFixture.DocumentDeletion.Armed = false;
        var retried = await DeleteGameAsync(_otherGameId, _token);

        retried.StatusCode.Should().Be(HttpStatusCode.NoContent, await retried.Content.ReadAsStringAsync(Ct));
        (await StillThereAsync(documents)).Should().Be(0);
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
        (await ContentOfAsync(_otherGameId)).Should().Be(Nothing);
        (await LiveLinksFromAsync(_otherGameId)).Should().Be(0);
    }

    /// <summary>Quanti di questi documenti hanno ancora la loro riga.</summary>
    private async Task<int> StillThereAsync(Guid[] pdfIds)
    {
        var count = 0;
        foreach (var pdfId in pdfIds)
        {
            if (await PdfExistsAsync(pdfId))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Un gioco privato cancellato prima di questa pulizia ha lasciato i suoi collegamenti dov'erano.
    /// Non contano: un collegamento da un gioco che non c'è più non tiene in vita un PDF.
    /// </summary>
    [Fact]
    public async Task ALinkLeftByAGameDeletedBefore_DoesNotKeepThePdfAlive()
    {
        var goneGameId = await SeedGameDeletedBeforeAsync();
        await LinkGameToPdfAsync(goneGameId, _otherPdfId);

        (await DeleteGameAsync(_otherGameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RowsOfPdfAsync(_otherPdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
    }

    /// <summary>
    /// La cancellazione del documento svuota le sue cache da dentro la propria transazione: una
    /// lettura che cade prima del commit le riempie di nuovo. La pulizia le svuota un'altra volta
    /// a commit avvenuto. La corsa non si riproduce in un test; si vede che cosa trova nel
    /// database un'altra connessione nell'istante di ciascuna invalidazione: alla prima il
    /// documento c'è ancora, alla seconda non c'è più.
    /// </summary>
    [Fact]
    public async Task TheCachesOfADeletedDocument_AreClearedAgainOnceItIsCommitted()
    {
        var documentStillThere = new List<bool>();
        _hostFixture.HybridCache.WhenRemoving(
            $"kbDoc:{_pdfId:N}", async () => documentStillThere.Add(await PdfExistsAsync(_pdfId)));

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        documentStillThere.Should().Equal(true, false);
        // Le due etichette del documento sono solo sue. `user:{id}` no: lo tolgono anche altri
        // gestori (l'attività dell'utente), quindi qui non si conta; che l'elenco si svuoti lo dice
        // TheOwnersDocumentList_NoLongerShowsTheDocumentsOfTheDeletedGame.
        _hostFixture.HybridCache.RemovalsOf($"kbChunks:{_pdfId:N}").Should().Be(2);
        // Il PDF dell'altro gioco non c'entra.
        _hostFixture.HybridCache.RemovalsOf($"kbDoc:{_otherPdfId:N}").Should().Be(0);
    }

    /// <summary>
    /// Un caricamento a pezzi lasciato a metà porta l'id del gioco. Completato dopo la
    /// cancellazione creerebbe un PDF su un gioco che non c'è più.
    /// </summary>
    [Fact]
    public async Task AnUploadStillOpenOnTheGame_IsDropped()
    {
        await OpenChunkedUploadAsync(_gameId);
        await OpenChunkedUploadAsync(_otherGameId);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await OpenUploadsOfAsync(_gameId)).Should().Be(0);
        (await OpenUploadsOfAsync(_otherGameId)).Should().Be(1);
    }

    /// <summary>
    /// Il documento e i suoi passaggi stanno in cache anche loro (<c>kbDoc:</c>, <c>kbChunks:</c>),
    /// i passaggi per mezz'ora, col testo dentro. Chi li aveva aperti non deve poterli rileggere.
    /// </summary>
    [Fact]
    public async Task ADocumentReadBefore_CannotBeReadAfterTheGameIsDeleted()
    {
        (await GetAsync($"/api/v1/kb-docs/{_pdfId}")).Should().Be(HttpStatusCode.OK);
        (await GetAsync($"/api/v1/kb-docs/{_pdfId}/chunks")).Should().Be(HttpStatusCode.OK);

        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetAsync($"/api/v1/kb-docs/{_pdfId}")).Should().Be(HttpStatusCode.NotFound);
        (await GetAsync($"/api/v1/kb-docs/{_pdfId}/chunks")).Should().Be(HttpStatusCode.NotFound);
    }


    /// <summary>
    /// «I miei documenti» (<c>GET /api/v1/kb-docs</c>) tiene l'elenco in cache per cinque minuti.
    /// Chi l'ha guardato prima di cancellare il gioco non deve ritrovarci un documento che non
    /// esiste più. Regge solo perché la fixture monta una cache che ricorda: con quella degli altri
    /// host di integrazione, che richiama sempre la fabbrica, passerebbe per niente.
    /// </summary>
    [Fact]
    public async Task TheOwnersDocumentList_NoLongerShowsTheDocumentsOfTheDeletedGame()
    {
        // La prima lettura mette l'elenco in cache: è il caso che conta.
        (await ListedDocumentsAsync()).Should().BeEquivalentTo(new[] { _pdfId, _otherPdfId });

        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await ListedDocumentsAsync()).Should().BeEquivalentTo(new[] { _otherPdfId });
    }

    /// <summary>
    /// La cancellazione del gioco resta logica: sparisce il contenuto, non la riga del gioco.
    /// Una cancellazione fisica violerebbe <c>CK_UserLibraryEntry_GameSource</c>.
    /// </summary>
    [Fact]
    public async Task TheGameItself_IsStillSoftDeleted()
    {
        var response = await DeleteGameAsync(_gameId, _token);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        (await IsDeletedAsync(_gameId)).Should().BeTrue();
        (await IsDeletedAsync(_otherGameId)).Should().BeFalse();
    }

    /// <summary>
    /// Ripetere la cancellazione non deve fare danni: il gioco non c'è più per il suo proprietario,
    /// e il contenuto degli altri giochi resta dov'è.
    /// </summary>
    [Fact]
    public async Task DeletingTwice_AnswersNotFound_AndRemovesNothingElse()
    {
        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var before = await ContentOfAsync(_otherGameId);

        var again = await DeleteGameAsync(_gameId, _token);

        again.StatusCode.Should().Be(HttpStatusCode.NotFound, await again.Content.ReadAsStringAsync(Ct));
        (await ContentOfAsync(_otherGameId)).Should().Be(before);
    }

    // ─── Supporto ────────────────────────────────────────────────

    private Task<HttpResponseMessage> DeleteGameAsync(Guid gameId, string token) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/api/v1/private-games/{gameId}", token), Ct);

    private async Task<(Guid GameId, Guid PdfId)> SeedPrivateGameAsync(
        MeepleAiDbContext db, string title, params (string Text, float[] Vector)[] passages)
    {
        var gameId = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = gameId,
            OwnerId = _userId,
            Title = title,
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
        });
        await db.SaveChangesAsync(Ct);
        var pdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, gameId, "Ready", Ct, passages);
        return (gameId, pdfId);
    }

    /// <summary>Un altro utente, con un suo gioco privato che esiste.</summary>
    private async Task<(Guid UserId, Guid GameId)> SeedStrangerWithAGameAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (strangerId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        var gameId = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = gameId,
            OwnerId = strangerId,
            Title = "Il prototipo di un altro",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
        });
        await db.SaveChangesAsync(Ct);
        return (strangerId, gameId);
    }

    /// <summary>
    /// Il PDF che il gioco privato di un altro utente ha lasciato: il gioco è cancellato
    /// (logicamente), il PDF e il suo indice sono ancora nel database.
    /// </summary>
    private async Task<Guid> SeedLeftoverPdfOfAStrangerAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (strangerId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        var goneGameId = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = goneGameId,
            OwnerId = strangerId,
            Title = "Un prototipo di un altro, di una volta",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-30),
        });
        await db.SaveChangesAsync(Ct);
        return await PrivateGameKbSeed.IndexPdfAsync(db, strangerId, goneGameId, "Ready", Ct,
            ("A rulebook left behind by somebody else.", QueryVectorEmbeddings.Axis(0f, 1f)));
    }

    /// <summary>Un gioco del catalogo, vivo o cancellato (logicamente).</summary>
    private async Task<Guid> SeedCatalogGameAsync(bool deleted)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Un gioco del catalogo",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = deleted,
        });
        await db.SaveChangesAsync(Ct);
        return gameId;
    }

    /// <summary>Un thread del proprietario sul gioco, con un messaggio dentro.</summary>
    private async Task OpenThreadAsync(Guid gameId)
    {
        var created = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/chat-threads", _token,
            new { gameId, title = "Chat sul prototipo" }), Ct);
        var body = await created.Content.ReadAsStringAsync(Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created, body);
        using var thread = JsonDocument.Parse(body);
        var threadId = thread.RootElement.GetProperty("id").GetGuid();

        var added = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/chat-threads/{threadId}/messages", _token,
            new { content = "Quante carte si pescano?", role = "user" }), Ct);
        added.IsSuccessStatusCode.Should().BeTrue(await added.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>Gli id dei documenti che il proprietario vede in «I miei documenti».</summary>
    private async Task<List<Guid>> ListedDocumentsAsync()
    {
        var response = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, "/api/v1/kb-docs?state=all", _token), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();
    }

    private async Task<Content> ContentOfAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return new Content(
            Threads: await db.ChatThreads.IgnoreQueryFilters().CountAsync(t => t.PrivateGameId == gameId, Ct),
            Pdfs: await db.PdfDocuments.IgnoreQueryFilters().CountAsync(p => p.PrivateGameId == gameId, Ct),
            VectorDocuments: await db.VectorDocuments.IgnoreQueryFilters().CountAsync(v => v.PrivateGameId == gameId, Ct),
            Passages: await db.TextChunks.IgnoreQueryFilters().CountAsync(c => c.PrivateGameId == gameId, Ct),
            Embeddings: await db.PgVectorEmbeddings.IgnoreQueryFilters().CountAsync(e => e.GameId == gameId, Ct));
    }

    /// <summary>
    /// Contenuto del gioco che la cancellazione dei suoi PDF non raggiunge: un PDF che non nomina
    /// il gioco, con passaggi, documento vettoriale ed embedding che invece lo nominano; e un
    /// embedding rimasto senza documento vettoriale.
    /// </summary>
    private async Task<Guid> SeedContentOutsideThePdfsOfTheGameAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var detachedPdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, gameId, "Ready", Ct,
            ("A passage whose PDF does not name the game.", QueryVectorEmbeddings.Axis(0f, 1f)));
        await db.PdfDocuments
            .Where(p => p.Id == detachedPdfId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PrivateGameId, (Guid?)null), Ct);

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);
        await using var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pgvector_embeddings
                (id, vector_document_id, game_id, text_content, vector, model,
                 chunk_index, page_number, created_at, lang, is_translation, role_tags)
            VALUES
                (@id, @vdId, @gameId, 'An embedding left without its vector document.', @vec, 'test-model',
                 0, 1, NOW(), 'en', false, 0)
            """;
        command.Parameters.AddWithValue("@id", Guid.NewGuid());
        command.Parameters.AddWithValue("@vdId", Guid.NewGuid());
        command.Parameters.AddWithValue("@gameId", gameId);
        command.Parameters.AddWithValue("@vec", new Pgvector.Vector(QueryVectorEmbeddings.Axis(1f, 0f)));
        await command.ExecuteNonQueryAsync(Ct);

        return detachedPdfId;
    }

    /// <summary>
    /// Un secondo PDF del gioco che cancello, a cui l'altro gioco è collegato: il caso che la
    /// deduplicazione crea quando lo stesso file viene caricato su due giochi.
    /// </summary>
    private async Task<Guid> SeedPdfUsedByBothGamesAsync()
    {
        Guid pdfId;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            pdfId = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, _gameId, "Ready", Ct,
                ("A rulebook both prototypes share.", QueryVectorEmbeddings.Axis(0f, 1f)));
        }

        await LinkGameToPdfAsync(_otherGameId, pdfId);
        return pdfId;
    }

    /// <summary>Un gioco privato dello stesso proprietario, già cancellato (logicamente).</summary>
    private async Task<Guid> SeedGameDeletedBeforeAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = Guid.NewGuid();
        db.PrivateGames.Add(new PrivateGameEntity
        {
            Id = gameId,
            OwnerId = _userId,
            Title = "Un prototipo di una volta",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow.AddDays(-30),
        });
        await db.SaveChangesAsync(Ct);
        return gameId;
    }

    /// <summary>Un collegamento dal gioco al PDF, di chi ha caricato il PDF se non si dice altro.</summary>
    private async Task LinkGameToPdfAsync(Guid gameId, Guid pdfId, Guid? ownerId = null)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.EntityLinks.Add(EntityLink.Create(
            MeepleEntityType.Game, gameId,
            MeepleEntityType.KbCard, pdfId,
            EntityLinkType.RelatedTo, EntityLinkScope.User, ownerId ?? _userId));
        await db.SaveChangesAsync(Ct);
    }

    private async Task<int> LiveLinksFromAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.EntityLinks.IgnoreQueryFilters().CountAsync(
            l => l.SourceEntityType == MeepleEntityType.Game && l.SourceEntityId == gameId && !l.IsDeleted, Ct);
    }

    private async Task OpenChunkedUploadAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.ChunkedUploadSessions.Add(new ChunkedUploadSessionEntity
        {
            PrivateGameId = gameId,
            UserId = _userId,
            FileName = "regolamento.pdf",
            TotalFileSize = 20_000_000,
            TotalChunks = 4,
            ReceivedChunks = 2,
            TempDirectory = $"/tmp/upload-{Guid.NewGuid():N}",
            Status = "uploading",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<int> OpenUploadsOfAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.ChunkedUploadSessions.IgnoreQueryFilters().CountAsync(u => u.PrivateGameId == gameId, Ct);
    }

    private async Task<PdfRows> RowsOfPdfAsync(Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var vectorDocumentIds = await db.VectorDocuments.IgnoreQueryFilters()
            .Where(v => v.PdfDocumentId == pdfId).Select(v => v.Id).ToListAsync(Ct);
        return new PdfRows(
            Exists: await db.PdfDocuments.IgnoreQueryFilters().AnyAsync(p => p.Id == pdfId, Ct),
            VectorDocuments: vectorDocumentIds.Count,
            Passages: await db.TextChunks.IgnoreQueryFilters().CountAsync(c => c.PdfDocumentId == pdfId, Ct),
            Embeddings: await db.PgVectorEmbeddings.IgnoreQueryFilters()
                .CountAsync(e => vectorDocumentIds.Contains(e.VectorDocumentId), Ct));
    }

    private async Task<HttpStatusCode> GetAsync(string path)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, path, _token), Ct);
        return response.StatusCode;
    }

    private async Task<bool> PdfExistsAsync(Guid pdfId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.PdfDocuments.IgnoreQueryFilters().AnyAsync(p => p.Id == pdfId, Ct);
    }

    private async Task<bool> IsDeletedAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.PrivateGames.IgnoreQueryFilters()
            .Where(g => g.Id == gameId)
            .Select(g => g.IsDeleted)
            .SingleAsync(Ct);
    }
}
