using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
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
/// #4660 punto 3 — quale documento vettoriale rappresenta un gioco in
/// <c>GetIndexingInfoByGameIdAsync</c> quando ce n'è più di uno.
/// </summary>
/// <remarks>
/// <para>
/// La regola, decisa il 2026-10-28: una riga non <c>completed</c> (<c>IndexedAt</c> nullo: pending, processing,
/// failed) precede OGNI <c>completed</c>, qualunque sia la data, perché l'endpoint è il polling di chi ha appena
/// caricato un PDF: se un secondo PDF fallisse e la risposta fosse «indicizzato» (il PDF vecchio), il fallimento
/// non si vedrebbe mai. Se il gioco ha una base di conoscenza utilizzabile lo dice un'altra query
/// (<c>GetKnowledgeBaseStatusQueryHandler</c>). Limiti non decisi, quindi non fissati da test: un primo
/// tentativo fallito continua a coprire un PDF caricato e completato dopo; un re-index tiene il suo vecchio
/// <c>IndexedAt</c>; un PDF che fallisce prima che esista il suo documento vettoriale non si vede.
/// </para>
/// <para>
/// Fra più righe non <c>completed</c> l'ordine era indefinito (pari merito su <c>DateTime.MaxValue</c>): decide
/// la data di upload del PDF, la più recente prima, poi l'id del documento. Ogni caso gira con le righe inserite
/// nei due ordini, perché con un pari merito il risultato dipende da quale arriva per prima. Gli id sono fissi e
/// quello alto va sempre alla riga che NON deve vincere: se lo spareggio per data di upload sparisse, deciderebbe
/// l'id e la prova diventerebbe rossa, non fortunata.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4660")]
public sealed class VectorDocumentRepositoryIndexingOrderTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>L'id che, in un pari merito deciso dall'id, perde (decrescente).</summary>
    private static readonly Guid LowId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>L'id che, in un pari merito deciso dall'id, vince (decrescente).</summary>
    private static readonly Guid HighId = Guid.Parse("ffffffff-ffff-ffff-ffff-fffffffffffe");

    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly VectorDocumentRepository _repository;

    public VectorDocumentRepositoryIndexingOrderTests()
    {
        _repository = new VectorDocumentRepository(_db, new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Un PDF e il suo documento vettoriale, nello stato dato, caricato <paramref name="uploadedHoursAgo"/> ore fa.</summary>
    private void Seed(
        Guid? sharedGameId,
        Guid? privateGameId,
        string indexingStatus,
        double uploadedHoursAgo,
        double? indexedHoursAgo = null,
        string? error = null,
        Guid? vectorId = null,
        int chunkCount = 0,
        MeepleAiDbContext? db = null,
        DateTime? uploadedAt = null)
    {
        var target = db ?? _db;
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            PrivateGameId = privateGameId,
            FileName = $"{indexingStatus}.pdf",
            FilePath = $"/tmp/{indexingStatus}.pdf",
            FileSizeBytes = 100,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = uploadedAt ?? Now.AddHours(-uploadedHoursAgo),
            ProcessingState = "Ready",
        };
        target.PdfDocuments.Add(pdf);
        target.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorId ?? Guid.NewGuid(),
            GameId = sharedGameId,
            SharedGameId = sharedGameId,
            PrivateGameId = privateGameId,
            PdfDocumentId = pdf.Id,
            IndexingStatus = indexingStatus,
            IndexedAt = indexedHoursAgo is null ? null : Now.AddHours(-indexedHoursAgo.Value),
            ChunkCount = chunkCount,
            IndexingError = error,
        });
    }

    private async Task SaveAsync() => await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

    // ────────────────────────────────────────────────────────────────────────────
    // Gioco condiviso: VectorDocument.GameId
    // ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Shared_AmongNonCompletedRows_TheNewestUploadWins(bool newestInsertedFirst)
    {
        var gameId = Guid.NewGuid();
        void Newest() => Seed(gameId, null, "processing", uploadedHoursAgo: 1, vectorId: LowId);
        void Oldest() => Seed(gameId, null, "failed", uploadedHoursAgo: 5, error: "old failure", vectorId: HighId);
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
        await SaveAsync();

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(
            VectorDocumentIndexingStatus.Processing,
            "the PDF uploaded last is the one the uploader is waiting for, whatever the insertion order or the id");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Shared_ANewerFailedRow_WinsOverAnOlderCompletedOne(bool failedInsertedFirst)
    {
        var gameId = Guid.NewGuid();
        void Failed() => Seed(gameId, null, "failed", uploadedHoursAgo: 1, error: "second PDF failed");
        void Completed() => Seed(gameId, null, "completed", uploadedHoursAgo: 48, indexedHoursAgo: 47);
        if (failedInsertedFirst)
        {
            Failed();
            Completed();
        }
        else
        {
            Completed();
            Failed();
        }
        await SaveAsync();

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(
            VectorDocumentIndexingStatus.Failed,
            "a second PDF that fails must show up in the poll of whoever uploaded it: answering 'indexed' with "
            + "the old PDF would hide the failure for good");
        info.IndexingError.Should().Be("second PDF failed");
    }

    /// <summary>
    /// Due PDF caricati nello stesso istante: l'ultimo spareggio è l'id del documento, quindi la risposta non
    /// dipende dall'ordine in cui il database restituisce le righe. Ogni ordine di inserimento gira su un
    /// database suo, con gli stessi id, e le due risposte devono coincidere.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenTwoUploadsShareTheSameInstant_TheAnswerDoesNotDependOnInsertionOrder(bool privateGame)
    {
        var gameId = Guid.NewGuid();
        var sameInstant = Now.AddHours(-2);
        var statuses = new List<VectorDocumentIndexingStatus>();

        foreach (var processingFirst in new[] { true, false })
        {
            using var db = TestDbContextFactory.CreateInMemoryDbContext();
            var repository = new VectorDocumentRepository(db, new Mock<IDomainEventCollector>().Object);
            void Processing() => Seed(
                privateGame ? null : gameId, privateGame ? gameId : null, "processing", 0, vectorId: LowId, db: db, uploadedAt: sameInstant);
            void Failed() => Seed(
                privateGame ? null : gameId, privateGame ? gameId : null, "failed", 0, error: "boom", vectorId: HighId, db: db, uploadedAt: sameInstant);
            if (processingFirst)
            {
                Processing();
                Failed();
            }
            else
            {
                Failed();
                Processing();
            }
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            var info = await repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);
            statuses.Add(info!.Status);
        }

        statuses.Distinct().Should().ContainSingle("the same two rows must give the same answer whichever was inserted first");
    }

    [Fact]
    public async Task Shared_AmongCompletedRows_TheMostRecentlyIndexedWins()
    {
        var gameId = Guid.NewGuid();
        Seed(gameId, null, "completed", uploadedHoursAgo: 48, indexedHoursAgo: 47, chunkCount: 10);
        Seed(gameId, null, "completed", uploadedHoursAgo: 10, indexedHoursAgo: 9, chunkCount: 77);
        await SaveAsync();

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Completed);
        info.ChunkCount.Should().Be(77);
    }

    // ────────────────────────────────────────────────────────────────────────────
    // Gioco privato: VectorDocument.GameId nullo, il gioco sta nel PDF (PrivateGameId)
    // ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Private_AmongNonCompletedRows_TheNewestUploadWins(bool newestInsertedFirst)
    {
        var gameId = Guid.NewGuid();
        void Newest() => Seed(null, gameId, "processing", uploadedHoursAgo: 1, vectorId: LowId);
        void Oldest() => Seed(null, gameId, "failed", uploadedHoursAgo: 5, error: "old failure", vectorId: HighId);
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
        await SaveAsync();

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Processing);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Private_ANewerFailedRow_WinsOverAnOlderCompletedOne(bool failedInsertedFirst)
    {
        var gameId = Guid.NewGuid();
        void Failed() => Seed(null, gameId, "failed", uploadedHoursAgo: 1, error: "second PDF failed");
        void Completed() => Seed(null, gameId, "completed", uploadedHoursAgo: 48, indexedHoursAgo: 47);
        if (failedInsertedFirst)
        {
            Failed();
            Completed();
        }
        else
        {
            Completed();
            Failed();
        }
        await SaveAsync();

        var info = await _repository.GetIndexingInfoByGameIdAsync(gameId, TestContext.Current.CancellationToken);

        info!.Status.Should().Be(VectorDocumentIndexingStatus.Failed);
    }
}
