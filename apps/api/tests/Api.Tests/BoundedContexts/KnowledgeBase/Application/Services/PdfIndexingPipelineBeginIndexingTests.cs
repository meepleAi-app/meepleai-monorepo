using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4609 — il documento vettoriale diventa <c>completed</c> DOPO i salvataggi, non prima.
///
/// <para>Prima, i chiamanti che non creano da soli la riga in <c>processing</c> (l'upload e la pipeline a
/// coda) chiamavano <c>IndexAsync</c> per far esistere il documento vettoriale, da cui dipendono i passaggi e le
/// righe vettoriali. Ma <c>IndexAsync</c> lo scrive già <c>completed</c> e pubblica
/// <c>VectorDocumentIndexedEvent</c>, che alza <c>shared_games.has_knowledge_base</c>: se i salvataggi dopo
/// fallivano, restava un indice «completo» senza righe. <c>BeginIndexingAsync</c> è il primo passo, quello che
/// fa esistere la riga senza dichiarare niente; <c>IndexAsync</c> è l'ultimo.</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class PdfIndexingPipelineBeginIndexingTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly List<VectorDocumentIndexedEvent> _published = new();

    public PdfIndexingPipelineBeginIndexingTests()
    {
        _mediator
            .Setup(m => m.Publish(It.IsAny<VectorDocumentIndexedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<VectorDocumentIndexedEvent, CancellationToken>((e, _) => _published.Add(e))
            .Returns(Task.CompletedTask);
    }

    private PdfIndexingPipeline CreatePipeline(MeepleAiDbContext db) =>
        new(db, _mediator.Object, TimeProvider.System, Mock.Of<ISemanticResponseCache>(), NullLogger<PdfIndexingPipeline>.Instance);

    [Fact]
    public async Task BeginIndexingAsync_WithoutARow_CreatesItProcessing_AndAnnouncesNothing()
    {
        var pdfId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        await CreatePipeline(db).BeginIndexingAsync(pdfId, sharedGameId, sharedGameId, chunkCount: 12, totalCharacters: 3400, language: "en");

        var row = await db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == pdfId);
        row.IndexingStatus.Should().Be("processing", "the row has to exist for the chunks and vector rows, but must not claim an index yet");
        row.SharedGameId.Should().Be(sharedGameId);
        row.GameId.Should().Be(sharedGameId);
        row.ChunkCount.Should().Be(12);
        row.TotalCharacters.Should().Be(3400);
        row.IndexedAt.Should().BeNull("nothing has been indexed yet");
        _published.Should().BeEmpty("the event that raises has_knowledge_base belongs to the completion, after the saves");
    }

    [Fact]
    public async Task BeginIndexingAsync_ForAPrivateGamePdf_KeepsThePrivateGameOutOfTheSharedForeignKey()
    {
        var pdfId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.PdfDocuments.Add(new PdfDocumentEntity { Id = pdfId, PrivateGameId = privateGameId, FileName = "private.pdf", FilePath = "private.pdf", ContentType = "application/pdf" });
        await db.SaveChangesAsync();

        // Un chiamante passa il gioco del catalogo (null per un PDF privato), un altro l'id privato: la riga non cambia.
        await CreatePipeline(db).BeginIndexingAsync(pdfId, gameId: privateGameId, sharedGameId: null, chunkCount: 3, totalCharacters: 100, language: "it");

        var row = await db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == pdfId);
        row.PrivateGameId.Should().Be(privateGameId);
        row.GameId.Should().BeNull("vector_documents.GameId is an FK to shared_games, so a private game goes in PrivateGameId (#4244)");
    }

    [Fact]
    public async Task BeginIndexingAsync_WithACompletedRow_LeavesItCompleted()
    {
        var pdfId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var indexedAt = DateTime.UtcNow.AddHours(-1);
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(), PdfDocumentId = pdfId, ChunkCount = 5, TotalCharacters = 50, IndexingStatus = "completed", IndexedAt = indexedAt,
        });
        await db.SaveChangesAsync();

        await CreatePipeline(db).BeginIndexingAsync(pdfId, gameId: null, sharedGameId: null, chunkCount: 9, totalCharacters: 90, language: "en");

        var row = await db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == pdfId);
        row.IndexingStatus.Should().Be("completed", "re-processing a document that is already indexed must not take its index away while it is rewritten");
        row.IndexedAt.Should().Be(indexedAt);
        _published.Should().BeEmpty();
    }

    [Fact]
    public async Task BeginIndexingAsync_WithAFailedRow_ReturnsItToProcessing_AndClearsTheError()
    {
        var pdfId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(), PdfDocumentId = pdfId, IndexingStatus = "failed", IndexingError = "the previous attempt failed",
        });
        await db.SaveChangesAsync();

        await CreatePipeline(db).BeginIndexingAsync(pdfId, gameId: null, sharedGameId: null, chunkCount: 4, totalCharacters: 40, language: "en");

        var row = await db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == pdfId);
        row.IndexingStatus.Should().Be("processing");
        row.IndexingError.Should().BeNull();
    }

    [Fact]
    public async Task BeginThenIndex_CompletesTheRow_AndAnnouncesItExactlyOnce()
    {
        var pdfId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var pipeline = CreatePipeline(db);

        await pipeline.BeginIndexingAsync(pdfId, sharedGameId, sharedGameId, chunkCount: 12, totalCharacters: 3400, language: "en");
        _published.Should().BeEmpty("between the two steps the saves happen, and a failure there must leave no announced index");
        await pipeline.IndexAsync(pdfId, sharedGameId, sharedGameId, chunkCount: 12, totalCharacters: 3400, language: "en");

        var row = await db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == pdfId);
        row.IndexingStatus.Should().Be("completed");
        row.IndexedAt.Should().NotBeNull();
        _published.Should().ContainSingle().Which.SharedGameId.Should().Be(sharedGameId);
    }

    [Fact]
    public async Task BeginThenIndex_OnACompletedRow_StaysSilent_AsAReindexAlwaysDid()
    {
        var pdfId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(), PdfDocumentId = pdfId, ChunkCount = 5, TotalCharacters = 50, IndexingStatus = "completed", IndexedAt = DateTime.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();
        var pipeline = CreatePipeline(db);

        await pipeline.BeginIndexingAsync(pdfId, gameId: null, sharedGameId: null, chunkCount: 9, totalCharacters: 90, language: "en");
        await pipeline.IndexAsync(pdfId, gameId: null, sharedGameId: null, chunkCount: 9, totalCharacters: 90, language: "en");

        var row = await db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == pdfId);
        row.ChunkCount.Should().Be(9);
        _published.Should().BeEmpty("the document was already announced as indexed");
    }

    /// <summary>
    /// Seguito di #4609: un PDF che fallisce dopo Begin non lascia il suo documento vettoriale in `processing`.
    /// </summary>
    [Fact]
    public async Task MarkIndexingFailedAsync_OnAProcessingRow_MarksItFailedWithTheError()
    {
        var pdfId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var pipeline = CreatePipeline(db);
        await pipeline.BeginIndexingAsync(pdfId, gameId: null, sharedGameId: null, chunkCount: 3, totalCharacters: 30, language: "en");

        await pipeline.MarkIndexingFailedAsync(pdfId, "Saving the vector rows of the PDF failed.");

        var row = await db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == pdfId);
        row.IndexingStatus.Should().Be("failed");
        row.IndexingError.Should().Be("Saving the vector rows of the PDF failed.");
        _published.Should().BeEmpty("a failed index announces nothing");
    }

    [Fact]
    public async Task MarkIndexingFailedAsync_OnACompletedRow_LeavesItCompleted()
    {
        var pdfId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(), PdfDocumentId = pdfId, ChunkCount = 5, TotalCharacters = 50, IndexingStatus = "completed", IndexedAt = DateTime.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();

        await CreatePipeline(db).MarkIndexingFailedAsync(pdfId, "a re-run failed");

        var row = await db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == pdfId);
        row.IndexingStatus.Should().Be("completed", "a re-run of an indexed document that fails keeps the index it had");
        row.IndexingError.Should().BeNull();
    }

    [Fact]
    public async Task MarkIndexingFailedAsync_WithoutARow_DoesNothing()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var act = () => CreatePipeline(db).MarkIndexingFailedAsync(Guid.NewGuid(), "failed before the indexing phase");

        await act.Should().NotThrowAsync("a PDF that failed before Begin has no vector document");
        (await db.VectorDocuments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MarkIndexingFailedAsync_KeepsALongErrorShort()
    {
        var pdfId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var pipeline = CreatePipeline(db);
        await pipeline.BeginIndexingAsync(pdfId, gameId: null, sharedGameId: null, chunkCount: 3, totalCharacters: 30, language: "en");

        await pipeline.MarkIndexingFailedAsync(pdfId, new string('e', 5000));

        var row = await db.VectorDocuments.AsNoTracking().SingleAsync(v => v.PdfDocumentId == pdfId);
        row.IndexingError!.Length.Should().Be(500);
    }

    [Theory]
    [InlineData(0, 10, "en")]
    [InlineData(-1, 10, "en")]
    [InlineData(1, -1, "en")]
    [InlineData(1, 10, " ")]
    public async Task BeginIndexingAsync_WithInvalidArguments_Throws(int chunkCount, int totalCharacters, string language)
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();

        var act = () => CreatePipeline(db).BeginIndexingAsync(Guid.NewGuid(), null, null, chunkCount, totalCharacters, language);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
