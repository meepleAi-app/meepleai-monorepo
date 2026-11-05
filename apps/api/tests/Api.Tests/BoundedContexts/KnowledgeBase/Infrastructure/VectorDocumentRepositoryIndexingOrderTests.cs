using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// #4660 punto 3 — quale documento rappresenta un gioco in <c>GetIndexingInfoByGameIdAsync</c> quando ha più
/// PDF: <b>l'ultimo caricato</b>, e il suo stato si legge dal PDF stesso.
/// </summary>
/// <remarks>
/// <para>
/// La regola, decisa il 2026-10-28 (sostituisce la prima, dello stesso giorno, che metteva davanti ogni riga non
/// <c>completed</c>): l'endpoint è il polling di chi ha appena caricato un PDF, e chi ha caricato per ultimo
/// aspetta quel PDF. Lo stato è <c>ProcessingState</c> del PDF (le tappe della pipeline), non l'esito del
/// documento vettoriale: così si vedono anche un PDF che fallisce prima che esista il suo documento vettoriale e
/// un re-index (il PDF non perde il suo posto perché il documento tiene il vecchio <c>IndexedAt</c>). Il numero
/// di chunk e il testo d'errore sono quelli del documento vettoriale di <i>quel</i> PDF.
/// </para>
/// <para>
/// Conseguenze volute: un PDF più vecchio che ha fallito non copre più uno caricato dopo e completato; un PDF più
/// vecchio completato non nasconde un fallimento di quello nuovo. Conseguenze che ne discendono e che il
/// proprietario ha confermato (2026-10-29): una errata o un'espansione possono essere l'ultimo upload. Eccezione
/// stretta, anch'essa confermata: un PDF rimesso al lavoro (un re-index lo riporta a <c>Pending</c>) il cui
/// documento vettoriale è già <c>completed</c> serve ancora il suo vecchio indice, e il polling dice «indexed». Se il gioco ha una
/// base di conoscenza utilizzabile lo dicono <c>GetKbReadinessQueryHandler</c> e
/// <c>GetGameRagReadinessQueryHandler</c> (contano i PDF <c>Ready</c>), non questa query.
/// </para>
/// <para>
/// Pari merito sull'istante di upload: decide l'id del PDF, il più alto vince. Ogni caso gira con le righe
/// inserite nei due ordini, perché in un pari merito il risultato dipende da quale arriva per prima. Quando la
/// prova deve dimostrare che decide la data e non l'id, l'id alto va al PDF che NON deve vincere: se lo spareggio
/// per data sparisse, deciderebbe l'id e la prova diventerebbe rossa, non fortunata.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4660")]
public sealed class VectorDocumentRepositoryIndexingOrderTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>L'id che, in un pari merito deciso dall'id, perde.</summary>
    private static readonly Guid LowId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>L'id che, in un pari merito deciso dall'id, vince.</summary>
    private static readonly Guid HighId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffffe");

    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly VectorDocumentRepository _repository;

    public VectorDocumentRepositoryIndexingOrderTests()
    {
        _repository = new VectorDocumentRepository(_db, new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    // ────────────────────────────────────────────────────────────────────────────
    // Supporto
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>Il documento vettoriale di un PDF: assente (<c>null</c>) finché la pipeline non lo crea.</summary>
    private sealed record Vector(string Status, int ChunkCount = 0, string? Error = null, double? IndexedHoursAgo = null);

    private static void SeedPdf(
        MeepleAiDbContext db,
        Guid gameId,
        bool privateGame,
        string pdfState,
        DateTime uploadedAt,
        Guid? pdfId = null,
        Vector? vector = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = pdfId ?? Guid.NewGuid(),
            SharedGameId = privateGame ? null : gameId,
            PrivateGameId = privateGame ? gameId : null,
            FileName = $"{pdfState}.pdf",
            FilePath = $"/tmp/{pdfState}.pdf",
            FileSizeBytes = 100,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = uploadedAt,
            ProcessingState = pdfState,
        };
        db.PdfDocuments.Add(pdf);

        if (vector is null)
        {
            return;
        }

        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = privateGame ? null : gameId,
            SharedGameId = privateGame ? null : gameId,
            PrivateGameId = privateGame ? gameId : null,
            PdfDocumentId = pdf.Id,
            IndexingStatus = vector.Status,
            IndexedAt = vector.IndexedHoursAgo is null ? null : Now.AddHours(-vector.IndexedHoursAgo.Value),
            ChunkCount = vector.ChunkCount,
            IndexingError = vector.Error,
        });
    }

    /// <summary>Il documento vettoriale che accompagna di norma un PDF nello stato dato.</summary>
    private static Vector VectorFor(string pdfState, int chunkCount = 0, string? error = null) => pdfState switch
    {
        "Ready" => new Vector("completed", chunkCount, null, IndexedHoursAgo: 1),
        "Failed" => new Vector("failed", 0, error ?? "failed"),
        "Pending" => new Vector("pending"),
        _ => new Vector("processing"),
    };

    /// <summary>
    /// Semina due PDF dello stesso gioco, il più recente e il più vecchio, nell'ordine di inserimento dato: l'id
    /// alto va sempre al PDF vecchio, quello che NON deve vincere.
    /// </summary>
    private static async Task<VectorDocumentIndexingInfo?> PollTwoUploadsAsync(
        bool privateGame,
        bool newestInsertedFirst,
        string newestState,
        string oldestState,
        Vector? newestVector,
        Vector? oldestVector)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var repository = new VectorDocumentRepository(db, new Mock<IDomainEventCollector>().Object);
        var gameId = Guid.NewGuid();
        void Newest() => SeedPdf(db, gameId, privateGame, newestState, Now.AddHours(-1), LowId, newestVector);
        void Oldest() => SeedPdf(db, gameId, privateGame, oldestState, Now.AddHours(-48), HighId, oldestVector);
        if (newestInsertedFirst)
        {
            Newest();
            Oldest();
        }
        else
        {
            Oldest();
            Newest();
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return await repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);
    }

    public static TheoryData<string, string, string, bool, bool> LastUploadCases()
    {
        var data = new TheoryData<string, string, string, bool, bool>();
        var cases = new (string Newest, string Oldest, string Expected)[]
        {
            // il polling di chi ha appena caricato: un secondo PDF che fallisce non deve sparire dietro il vecchio
            ("Failed", "Ready", nameof(VectorDocumentIndexingStatus.Failed)),
            // un PDF vecchio che ha fallito non copre più uno caricato dopo e completato
            ("Ready", "Failed", nameof(VectorDocumentIndexingStatus.Completed)),
            ("Embedding", "Failed", nameof(VectorDocumentIndexingStatus.Processing)),
            ("Embedding", "Ready", nameof(VectorDocumentIndexingStatus.Processing)),
            ("Pending", "Ready", nameof(VectorDocumentIndexingStatus.Pending)),
        };
        foreach (var (newest, oldest, expected) in cases)
        {
            foreach (var privateGame in new[] { false, true })
            {
                foreach (var newestInsertedFirst in new[] { true, false })
                {
                    data.Add(newest, oldest, expected, privateGame, newestInsertedFirst);
                }
            }
        }

        return data;
    }

    // ────────────────────────────────────────────────────────────────────────────
    // L'ultimo upload decide
    // ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(LastUploadCases))]
    public async Task TheLastUploadedPdfDecides_WhateverTheEarlierOnesDid(
        string newestState,
        string oldestState,
        string expected,
        bool privateGame,
        bool newestInsertedFirst)
    {
        var info = await PollTwoUploadsAsync(
            privateGame, newestInsertedFirst, newestState, oldestState, VectorFor(newestState), VectorFor(oldestState));

        info!.Status.Should().Be(
            Enum.Parse<VectorDocumentIndexingStatus>(expected),
            "the poll answers for the PDF uploaded last, whatever the insertion order or the ids (the older PDF has the higher id)");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFailedLastUpload_ReportsItsOwnError_NotTheOlderPdfs(bool privateGame)
    {
        var info = await PollTwoUploadsAsync(
            privateGame, newestInsertedFirst: true, "Failed", "Failed",
            new Vector("failed", 0, "newest failed"), new Vector("failed", 0, "oldest failed"));

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Failed);
        info.IndexingError.Should().Be("newest failed");
    }

    /// <summary>
    /// Un PDF indicizzato di nuovo tiene il suo vecchio <c>IndexedAt</c> nel documento vettoriale: con la regola
    /// vecchia poteva perdere contro un documento indicizzato dopo. Ora conta solo la data di upload.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task TheLastUpload_Wins_EvenWhenAnOlderPdfWasIndexedLater_AndReportsItsOwnChunkCount(
        bool privateGame,
        bool newestInsertedFirst)
    {
        var info = await PollTwoUploadsAsync(
            privateGame, newestInsertedFirst, "Ready", "Ready",
            new Vector("completed", ChunkCount: 77, IndexedHoursAgo: 40),
            new Vector("completed", ChunkCount: 10, IndexedHoursAgo: 1));

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Completed);
        info.ChunkCount.Should().Be(77, "the chunk count is the one of the PDF uploaded last, not of the document indexed last");
    }

    /// <summary>
    /// Due PDF caricati nello stesso istante: l'ultimo spareggio è l'id del PDF (il più alto vince), quindi la
    /// risposta non dipende dall'ordine in cui il database restituisce le righe.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task WhenTwoUploadsShareTheSameInstant_TheHigherPdfIdWins_WhateverTheInsertionOrder(
        bool privateGame,
        bool highInsertedFirst)
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var repository = new VectorDocumentRepository(db, new Mock<IDomainEventCollector>().Object);
        var gameId = Guid.NewGuid();
        var sameInstant = Now.AddHours(-2);
        void High() => SeedPdf(db, gameId, privateGame, "Failed", sameInstant, HighId, new Vector("failed", 0, "high"));
        void Low() => SeedPdf(db, gameId, privateGame, "Embedding", sameInstant, LowId, new Vector("processing"));
        if (highInsertedFirst)
        {
            High();
            Low();
        }
        else
        {
            Low();
            High();
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var info = await repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Failed, "the higher id breaks the tie");
        info.IndexingError.Should().Be("high");
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Lo stato si legge dal PDF
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un PDF che fallisce prima che esista il suo documento vettoriale (estrazione, chunking) si vede, anche se
    /// un PDF più vecchio dello stesso gioco è indicizzato. Con la regola vecchia non si vedeva mai.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ALastUploadThatFailedBeforeItsVectorDocumentExisted_IsSeen(bool privateGame)
    {
        var info = await PollTwoUploadsAsync(
            privateGame, newestInsertedFirst: false, "Failed", "Ready",
            newestVector: null, oldestVector: VectorFor("Ready", chunkCount: 10));

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Failed);
        info.IndexingError.Should().BeNull("there is no vector document to take a message from");
        info.ChunkCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Pending", nameof(VectorDocumentIndexingStatus.Pending))]
    [InlineData("Uploading", nameof(VectorDocumentIndexingStatus.Processing))]
    [InlineData("Extracting", nameof(VectorDocumentIndexingStatus.Processing))]
    [InlineData("Chunking", nameof(VectorDocumentIndexingStatus.Processing))]
    [InlineData("Embedding", nameof(VectorDocumentIndexingStatus.Processing))]
    [InlineData("Indexing", nameof(VectorDocumentIndexingStatus.Processing))]
    [InlineData("Ready", nameof(VectorDocumentIndexingStatus.Completed))]
    [InlineData("Failed", nameof(VectorDocumentIndexingStatus.Failed))]
    public async Task APdfWithoutAVectorDocument_IsMappedFromItsProcessingState(
        string pdfState,
        string expected)
    {
        var gameId = Guid.NewGuid();
        SeedPdf(_db, gameId, privateGame: false, pdfState, Now.AddHours(-1));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(Enum.Parse<VectorDocumentIndexingStatus>(expected));
    }

    /// <summary>
    /// Il PDF è la fonte dello stato: se il documento vettoriale dice un'altra cosa (una riga rimasta
    /// <c>processing</c> mentre il PDF è già <c>Failed</c> o <c>Ready</c>), vale il PDF. Numero di chunk e testo
    /// d'errore restano quelli del documento vettoriale. L'unica eccezione è quella dei test sotto: un PDF in
    /// lavorazione con il documento già <c>completed</c>.
    /// </summary>
    [Theory]
    [InlineData("Failed", "processing", nameof(VectorDocumentIndexingStatus.Failed))]
    [InlineData("Ready", "processing", nameof(VectorDocumentIndexingStatus.Completed))]
    [InlineData("Embedding", "processing", nameof(VectorDocumentIndexingStatus.Processing))]
    [InlineData("Embedding", "failed", nameof(VectorDocumentIndexingStatus.Processing))]
    [InlineData("Pending", "processing", nameof(VectorDocumentIndexingStatus.Pending))]
    [InlineData("Pending", "pending", nameof(VectorDocumentIndexingStatus.Pending))]
    public async Task TheStateComesFromThePdf_NotFromItsVectorDocument(
        string pdfState,
        string vectorStatus,
        string expected)
    {
        var gameId = Guid.NewGuid();
        SeedPdf(_db, gameId, privateGame: false, pdfState, Now.AddHours(-1), vector: new Vector(vectorStatus, ChunkCount: 5));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(Enum.Parse<VectorDocumentIndexingStatus>(expected));
        if (expected == nameof(VectorDocumentIndexingStatus.Completed))
        {
            info.ChunkCount.Should().Be(5, "the chunk count is the vector document's");
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Un PDF in lavorazione che serve ancora il suo indice (decisione del 2026-10-29)
    // ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un re-index riporta il PDF a <c>Pending</c> e il documento vettoriale resta <c>completed</c> per tutta la
    /// durata (decisione del 2026-10-25): il vecchio indice serve ancora. Il polling dice «indexed», con i chunk
    /// di quel documento, per ogni tappa in cui il PDF può trovarsi. Lo stesso vale per un PDF rimasto in
    /// <c>Indexing</c> perché ha perso una gara di concorrenza sul salvataggio finale.
    /// </summary>
    [Theory]
    [InlineData("Pending", false)]
    [InlineData("Uploading", false)]
    [InlineData("Extracting", false)]
    [InlineData("Chunking", false)]
    [InlineData("Embedding", false)]
    [InlineData("Indexing", false)]
    [InlineData("Pending", true)]
    [InlineData("Indexing", true)]
    public async Task APdfBackAtWork_WhoseOwnVectorDocumentIsCompleted_StillReportsTheIndex(string pdfState, bool privateGame)
    {
        var gameId = Guid.NewGuid();
        SeedPdf(_db, gameId, privateGame, pdfState, Now.AddHours(-1), vector: new Vector("completed", ChunkCount: 42, IndexedHoursAgo: 30));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Completed, "the old index still serves while the PDF is being worked on again");
        info.ChunkCount.Should().Be(42);
        info.IndexingError.Should().BeNull();
    }

    /// <summary>
    /// Se il re-index fallisce il PDF è <c>Failed</c>, e resta «failed» anche se il documento vettoriale è
    /// ancora <c>completed</c> (non lo si compensa, decisione del 2026-10-25): chi ha caricato deve vedere il
    /// fallimento.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFailedPdf_WhoseVectorDocumentIsStillCompleted_ReportsFailed(bool privateGame)
    {
        var gameId = Guid.NewGuid();
        SeedPdf(_db, gameId, privateGame, "Failed", Now.AddHours(-1), vector: new Vector("completed", ChunkCount: 42, IndexedHoursAgo: 30));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Failed);
    }

    /// <summary>
    /// L'eccezione riguarda il documento vettoriale DEL PDF ultimo: un PDF più vecchio indicizzato non rende
    /// «indexed» uno nuovo che sta ancora lavorando. L'id alto va al PDF vecchio, quello che non deve contare.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task AnOlderPdfsCompletedIndex_DoesNotMakeTheNewerPdfIndexed(bool privateGame, bool newestInsertedFirst)
    {
        var info = await PollTwoUploadsAsync(
            privateGame, newestInsertedFirst, "Embedding", "Ready",
            newestVector: new Vector("processing"),
            oldestVector: new Vector("completed", ChunkCount: 10, IndexedHoursAgo: 40));

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Processing);
        info.ChunkCount.Should().Be(0);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Confini della regola
    // ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task APdfOfAnotherGame_UploadedLater_IsNotMixedIn()
    {
        var gameId = Guid.NewGuid();
        SeedPdf(_db, gameId, privateGame: false, "Ready", Now.AddHours(-10), vector: VectorFor("Ready", chunkCount: 12));
        SeedPdf(_db, Guid.NewGuid(), privateGame: false, "Failed", Now.AddHours(-1), vector: VectorFor("Failed"));
        SeedPdf(_db, Guid.NewGuid(), privateGame: true, "Embedding", Now.AddHours(-1), vector: VectorFor("Embedding"));
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Completed);
        info.ChunkCount.Should().Be(12);
    }

    [Fact]
    public async Task AGameWithNothingIndexedOrUploaded_ReturnsNull()
    {
        var info = await _repository.GetIndexingInfoByGameIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        info.Should().BeNull();
    }

    /// <summary>
    /// Un documento vettoriale del gioco il cui PDF non porta il gioco (righe vecchie, da prima di
    /// <c>SharedGameId</c>/<c>PrivateGameId</c> sul PDF) non ha un PDF del gioco da cui leggere lo stato: si
    /// risponde con quello del documento, come prima. Fra più documenti così, vince il non completato, poi il
    /// più recente.
    /// </summary>
    [Fact]
    public async Task VectorDocumentsWhosePdfDoesNotCarryTheGame_AreStillReported_TheUnfinishedOneFirst()
    {
        var gameId = Guid.NewGuid();
        foreach (var (status, indexedAt, chunks) in new (string, DateTime?, int)[]
                 {
                     ("completed", Now.AddHours(-3), 12),
                     ("processing", null, 0),
                 })
        {
            var pdfId = Guid.NewGuid();
            _db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                FileName = $"{status}.pdf",
                FilePath = $"/tmp/{status}.pdf",
                FileSizeBytes = 100,
                ContentType = "application/pdf",
                UploadedByUserId = Guid.NewGuid(),
                UploadedAt = Now.AddHours(-5),
                ProcessingState = "Ready",
            });
            _db.VectorDocuments.Add(new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                PdfDocumentId = pdfId,
                IndexingStatus = status,
                IndexedAt = indexedAt,
                ChunkCount = chunks,
            });
        }
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Processing);
    }
}
