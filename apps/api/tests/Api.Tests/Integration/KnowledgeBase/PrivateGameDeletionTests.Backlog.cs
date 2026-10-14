using System.Net;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Cleanup;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// #4244, consegna C2 — il pregresso. La pulizia della consegna C parte quando un gioco privato
/// viene cancellato: i giochi cancellati <b>prima</b> che esistesse tengono ancora il loro
/// contenuto, e così ciò che la C dichiara di poter lasciare (un PDF tenuto per un altro gioco che
/// poi lo lascia, un caricamento che si completa mentre il gioco viene cancellato). All'avvio una
/// passata invia la stessa pulizia per ogni gioco privato cancellato che ha ancora qualcosa.
/// </summary>
public sealed partial class PrivateGameDeletionTests
{
    /// <summary>
    /// Com'erano tutti i giochi privati cancellati prima della consegna C: la riga segnata come
    /// cancellata, e il contenuto ancora nel database.
    /// </summary>
    [Fact]
    public async Task AGameDeletedBeforeThePurgeExisted_LosesItsContentAtTheNextSweep()
    {
        await MarkDeletedTheOldWayAsync(_gameId);
        var before = await ContentOfAsync(_gameId);
        before.Should().NotBe(Nothing, "il test deve partire da un contenuto che c'è");
        var control = await ContentOfAsync(_otherGameId);

        await SweepAsync();

        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
        (await RowsOfPdfAsync(_pdfId)).Should().Be(new PdfRows(false, 0, 0, 0));
        // Il gioco vivo dello stesso proprietario non è stato toccato.
        (await ContentOfAsync(_otherGameId)).Should().Be(control);
        (await IsDeletedAsync(_otherGameId)).Should().BeFalse();
    }

    /// <summary>
    /// La passata visita solo ciò che serve: non i giochi vivi, e non i giochi cancellati di cui
    /// non resta niente. Altrimenti ogni avvio ripasserebbe tutti i giochi cancellati di sempre.
    /// </summary>
    [Fact]
    public async Task TheSweep_VisitsOnlyDeletedGamesThatStillHaveSomething()
    {
        await MarkDeletedTheOldWayAsync(_gameId);
        var deletedLongAgoWithNothingLeft = await SeedGameDeletedBeforeAsync();

        var toVisit = await GamesStillToCleanAsync();

        toVisit.Should().Equal(_gameId);
        toVisit.Should().NotContain(deletedLongAgoWithNothingLeft);

        await SweepAsync();

        (await GamesStillToCleanAsync()).Should().BeEmpty("dopo la passata non resta niente da visitare");
    }

    /// <summary>Ciò che può restare di un gioco privato cancellato, un tipo alla volta.</summary>
    public enum Leftover
    {
        Thread,
        Pdf,
        Passages,
        VectorDocument,
        Embeddings,
        OpenUpload,
        Link,
    }

    /// <summary>
    /// Basta un solo tipo di residuo perché il gioco vada visitato: ognuno è una condizione a sé
    /// della ricerca, e una condizione tolta lascerebbe quel residuo nel database per sempre.
    /// </summary>
    [Theory]
    [InlineData(Leftover.Thread)]
    [InlineData(Leftover.Pdf)]
    [InlineData(Leftover.Passages)]
    [InlineData(Leftover.VectorDocument)]
    [InlineData(Leftover.Embeddings)]
    [InlineData(Leftover.OpenUpload)]
    [InlineData(Leftover.Link)]
    public async Task ADeletedGameWithASingleKindOfLeftover_IsVisitedAndCleaned(Leftover leftover)
    {
        var gone = await SeedGameDeletedBeforeAsync();
        (await GamesStillToCleanAsync()).Should().BeEmpty("un gioco cancellato senza residui non va visitato");
        await LeaveBehindAsync(gone, leftover);
        var control = await ContentOfAsync(_otherGameId);

        (await GamesStillToCleanAsync()).Should().Equal(gone);

        await SweepAsync();

        (await GamesStillToCleanAsync()).Should().BeEmpty();
        (await ContentOfAsync(gone)).Should().Be(Nothing);
        (await OpenUploadsOfAsync(gone)).Should().Be(0);
        (await LiveLinksFromAsync(gone)).Should().Be(0);
        // Né il gioco vivo né il suo PDF, a cui nel caso del collegamento il gioco cancellato punta.
        (await ContentOfAsync(_otherGameId)).Should().Be(control);
        (await PdfExistsAsync(_otherPdfId)).Should().BeTrue();
    }

    /// <summary>Lascia sul gioco cancellato un solo tipo di residuo.</summary>
    private async Task LeaveBehindAsync(Guid gameId, Leftover leftover)
    {
        switch (leftover)
        {
            case Leftover.OpenUpload:
                await OpenChunkedUploadAsync(gameId);
                return;
            case Leftover.Link:
                await LinkGameToPdfAsync(gameId, _otherPdfId);
                return;
            case Leftover.Passages:
            case Leftover.VectorDocument:
            case Leftover.Embeddings:
                // Passaggi, documento vettoriale ed embedding che nominano il gioco sotto un PDF
                // che non lo nomina; poi si toglie tutto tranne il tipo che interessa.
                await SeedContentOutsideThePdfsOfTheGameAsync(gameId);
                break;
        }

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        switch (leftover)
        {
            case Leftover.Thread:
                db.ChatThreads.Add(new Api.Infrastructure.Entities.ChatThreadEntity
                {
                    UserId = _userId,
                    PrivateGameId = gameId,
                    Title = "Una chat rimasta",
                });
                await db.SaveChangesAsync(Ct);
                break;
            case Leftover.Pdf:
                // Senza passaggi: resta la sola riga del PDF.
                await PrivateGameKbSeed.IndexPdfAsync(db, _userId, gameId, "Pending", Ct);
                break;
            case Leftover.Passages:
                await db.PgVectorEmbeddings.Where(e => e.GameId == gameId).ExecuteDeleteAsync(Ct);
                await db.VectorDocuments.Where(v => v.PrivateGameId == gameId).ExecuteDeleteAsync(Ct);
                break;
            case Leftover.VectorDocument:
                await db.PgVectorEmbeddings.Where(e => e.GameId == gameId).ExecuteDeleteAsync(Ct);
                await db.TextChunks.Where(c => c.PrivateGameId == gameId).ExecuteDeleteAsync(Ct);
                break;
            case Leftover.Embeddings:
                await db.TextChunks.Where(c => c.PrivateGameId == gameId).ExecuteDeleteAsync(Ct);
                await db.VectorDocuments.Where(v => v.PrivateGameId == gameId).ExecuteDeleteAsync(Ct);
                break;
        }

        // Il seme ha lasciato davvero un tipo solo: senza questo, il test non direbbe quale condizione prova.
        var left = await ContentOfAsync(gameId);
        var expected = leftover switch
        {
            Leftover.Thread => Nothing with { Threads = 1 },
            Leftover.Pdf => Nothing with { Pdfs = 1 },
            Leftover.Passages => Nothing with { Passages = 1 },
            Leftover.VectorDocument => Nothing with { VectorDocuments = 1 },
            Leftover.Embeddings => Nothing with { Embeddings = 2 },
            _ => Nothing,
        };
        left.Should().Be(expected);
    }

    /// <summary>
    /// Ciò che la consegna C dichiara di poter lasciare. Il PDF del gioco cancellato resta perché
    /// l'altro gioco lo collega; poi quel collegamento viene tolto, senza cancellare l'altro
    /// gioco. Nessuna pulizia ripassa da sola: lo raccoglie la passata.
    /// </summary>
    [Fact]
    public async Task APdfKeptForAnotherGameThatLaterLetItGo_IsCollectedByTheSweep()
    {
        var kept = await SeedPdfUsedByBothGamesAsync();
        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RowsOfPdfAsync(kept)).Exists.Should().BeTrue("finché l'altro gioco lo collega, resta");

        await DropLinksFromAsync(_otherGameId);
        var control = await ContentOfAsync(_otherGameId);

        await SweepAsync();

        (await RowsOfPdfAsync(kept)).Should().Be(new PdfRows(false, 0, 0, 0));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
        (await ContentOfAsync(_otherGameId)).Should().Be(control);
    }

    /// <summary>La passata non è più severa della pulizia: un PDF ancora in uso resta.</summary>
    [Fact]
    public async Task APdfStillKeptForAnotherGame_SurvivesTheSweep()
    {
        var kept = await SeedPdfUsedByBothGamesAsync();
        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await RowsOfPdfAsync(kept);
        rows.Exists.Should().BeTrue();

        await SweepAsync();

        (await RowsOfPdfAsync(kept)).Should().Be(rows);
        (await LiveLinksFromAsync(_otherGameId)).Should().Be(1);
    }

    /// <summary>
    /// L'altro caso dichiarato: un caricamento che si completa mentre il gioco viene cancellato
    /// lascia un PDF su un gioco che non c'è più.
    /// </summary>
    [Fact]
    public async Task APdfThatLandedOnAGameAlreadyDeleted_IsCollectedByTheSweep()
    {
        (await DeleteGameAsync(_gameId, _token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Guid late;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            late = await PrivateGameKbSeed.IndexPdfAsync(db, _userId, _gameId, "Ready", Ct,
                ("A page uploaded while the game was going away.", QueryVectorEmbeddings.Axis(1f, 0f)));
        }

        (await ContentOfAsync(_gameId)).Should().NotBe(Nothing);

        await SweepAsync();

        (await RowsOfPdfAsync(late)).Should().Be(new PdfRows(false, 0, 0, 0));
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
    }

    /// <summary>
    /// Un gioco che non si riesce a pulire non ferma gli altri, e non ferma l'avvio: la passata
    /// gira fra gli strati di seed, dove un'eccezione salterebbe tutti quelli dopo.
    /// </summary>
    [Fact]
    public async Task WhenOneGameCannotBeCleaned_TheSweepGoesOnWithTheOthers()
    {
        // La passata va dal gioco cancellato da più tempo: quello che fallisce viene visitato per primo.
        await MarkDeletedTheOldWayAsync(_gameId, DateTime.UtcNow.AddDays(-2));
        await MarkDeletedTheOldWayAsync(_otherGameId, DateTime.UtcNow.AddDays(-1));
        _hostFixture.DocumentDeletion.ArmFor(_pdfId);

        var sweep = async () => await SweepAsync();

        await sweep.Should().NotThrowAsync();
        (await GamesStillToCleanAsync()).Should().Equal([_gameId], "il secondo è stato pulito anche se il primo è fallito");
        (await ContentOfAsync(_otherGameId)).Should().Be(Nothing);

        _hostFixture.DocumentDeletion.Armed = false;
        await SweepAsync();

        (await GamesStillToCleanAsync()).Should().BeEmpty("al giro dopo si riprova");
        (await ContentOfAsync(_gameId)).Should().Be(Nothing);
    }

    /// <summary>
    /// Se la passata non riesce nemmeno a leggere il database, l'avvio va avanti: l'orchestratore
    /// non protegge gli strati, e un'eccezione qui salterebbe tutti quelli dopo.
    /// </summary>
    [Fact]
    public async Task ASweepThatCannotReadTheDatabase_DoesNotStopTheStartup()
    {
        var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var context = new SeedContext(SeedProfile.Staging, db, scope.ServiceProvider, NullLogger.Instance, Guid.Empty);
        scope.Dispose();

        var sweep = async () => await new DeletedPrivateGameContentCleanupLayer().SeedAsync(context, Ct);

        await sweep.Should().NotThrowAsync();
    }

    /// <summary>
    /// La passata è uno strato di seed registrato, e gira in ogni profilo che semina: senza
    /// questo, la classe esisterebbe e non verrebbe mai chiamata.
    /// </summary>
    [Fact]
    public void TheSweep_IsARegisteredSeedLayer_ThatRunsInEveryProfile()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var layers = scope.ServiceProvider.GetServices<ISeedLayer>().ToList();

        layers.OfType<DeletedPrivateGameContentCleanupLayer>().Should().ContainSingle();
        foreach (var profile in new[] { SeedProfile.Prod, SeedProfile.Staging, SeedProfile.Dev })
        {
            SeedOrchestrator.FilterLayers(layers, profile)
                .OfType<DeletedPrivateGameContentCleanupLayer>()
                .Should().ContainSingle($"il profilo {profile} deve eseguirla");
        }
    }

    // ─── Aiuti ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Esegue la passata come la esegue l'orchestratore dei seed: lo strato, con il suo contesto.</summary>
    private async Task SweepAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var context = new SeedContext(SeedProfile.Staging, db, scope.ServiceProvider, NullLogger.Instance, Guid.Empty);

        await new DeletedPrivateGameContentCleanupLayer().SeedAsync(context, Ct);
    }

    private async Task<List<Guid>> GamesStillToCleanAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await DeletedPrivateGameContentCleanupLayer.FindDeletedGamesWithContentAsync(db, Ct);
    }

    /// <summary>Segna il gioco come cancellato senza passare dalla rotta: niente pulizia, come prima della consegna C.</summary>
    private async Task MarkDeletedTheOldWayAsync(Guid gameId, DateTime? deletedAt = null)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var when = deletedAt ?? DateTime.UtcNow;
        var rows = await db.PrivateGames
            .IgnoreQueryFilters()
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(g => g.IsDeleted, true).SetProperty(g => g.DeletedAt, when), Ct);
        rows.Should().Be(1);
    }

    /// <summary>Toglie i collegamenti del gioco, come fa <c>DELETE /library/entity-links</c>: il gioco resta.</summary>
    private async Task DropLinksFromAsync(Guid gameId)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var now = DateTime.UtcNow;
        var rows = await db.EntityLinks
            .IgnoreQueryFilters()
            .Where(l => !l.IsDeleted && l.SourceEntityType == MeepleEntityType.Game && l.SourceEntityId == gameId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(l => l.IsDeleted, true).SetProperty(l => l.DeletedAt, now), Ct);
        rows.Should().BeGreaterThan(0);
    }
}
