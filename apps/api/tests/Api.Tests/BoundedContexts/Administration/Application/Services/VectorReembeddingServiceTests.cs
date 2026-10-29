using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Services;

/// <summary>
/// #4660 — <c>VectorReembeddingService</c> sostituiva gli embedding di un documento cancellandoli PRIMA di
/// generare i nuovi (un <c>DELETE</c> in autocommit, poi gli embedding a lotti in rete, poi l'inserimento): un guasto
/// del servizio di embedding a metà lasciava il documento <c>completed</c> senza un solo embedding. Ora genera tutti
/// i nuovi embedding per primi e poi li scambia con i vecchi in un'unica operazione atomica
/// (<c>ReplaceByVectorDocumentIdAsync</c>): finché non è pronto il nuovo insieme, il vecchio resta intatto.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4660")]
public sealed class VectorReembeddingServiceTests : IDisposable
{
    private const string OldModel = "old-model";
    private const string NewModel = "new-model";

    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly Mock<IEmbeddingService> _embeddings = new();
    private readonly Mock<IVectorStoreAdapter> _store = new();
    private readonly Mock<IBatchJobRepository> _jobs = new();
    private readonly RecordingLogger<VectorReembeddingService> _log = new();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _pdfId = Guid.NewGuid();
    private readonly Guid _vectorDocumentId = Guid.NewGuid();
    private List<Embedding>? _replacedWith;

    public VectorReembeddingServiceTests()
    {
        _embeddings.Setup(e => e.GetModelName()).Returns(NewModel);
        _embeddings.Setup(e => e.GetEmbeddingDimensions()).Returns(4);
        _store.Setup(s => s.EnsureCollectionExistsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _store.Setup(s => s.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, List<Embedding> e, CancellationToken _) => _replacedWith = e)
            .Returns(Task.CompletedTask);
        _jobs.Setup(r => r.UpdateAsync(It.IsAny<BatchJob>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    public void Dispose() => _db.Dispose();

    private VectorReembeddingService CreateSut() =>
        new(_db, _embeddings.Object, _store.Object, _jobs.Object, _log);

    private static BatchJob NewJob() => BatchJob.Create(JobType.VectorReembedding, "{}", Guid.NewGuid());

    private List<Guid> Seed(int chunks, string status = "completed", string? error = null)
    {
        _db.Set<VectorDocumentEntity>().Add(new VectorDocumentEntity
        {
            Id = _vectorDocumentId,
            GameId = _gameId,
            PdfDocumentId = _pdfId,
            ChunkCount = chunks,
            IndexingStatus = status,
            IndexingError = error,
            IndexedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EmbeddingModel = OldModel,
            EmbeddingDimensions = 2,
        });
        var ids = new List<Guid>();
        for (var i = 0; i < chunks; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            _db.TextChunks.Add(new TextChunkEntity
            {
                Id = id,
                GameId = _gameId,
                PdfDocumentId = _pdfId,
                Content = $"chunk {i}",
                ChunkIndex = i,
                PageNumber = i + 1,
                CharacterCount = 7,
            });
        }

        _db.SaveChanges();
        return ids;
    }

    private void EmbeddingsSucceed() =>
        _embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) =>
                EmbeddingResult.CreateSuccess(texts.Select(_ => new[] { 0.1f, 0.2f, 0.3f, 0.4f }).ToList()));

    private async Task<VectorDocumentEntity> ReloadDocAsync() =>
        await _db.Set<VectorDocumentEntity>().AsNoTracking().SingleAsync(v => v.Id == _vectorDocumentId, TestContext.Current.CancellationToken);

    private void VerifyOldEmbeddingsUntouched()
    {
        _store.Verify(s => s.DeleteByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "the old embeddings must not be deleted outside the atomic swap");
        _store.Verify(s => s.IndexBatchAsync(It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnSuccess_SwapsAllTheNewEmbeddingsInOneAtomicReplace_AndUpdatesTheDocument()
    {
        // tre lotti da dieci: l'insieme si scambia una volta sola, non per lotto. Il documento parte da failed, con un
        // errore, per vedere che alla fine è completed e senza errore.
        var chunkIds = Seed(chunks: 25, status: "failed", error: "old failure");
        EmbeddingsSucceed();

        await CreateSut().ExecuteAsync(NewJob(), TestContext.Current.CancellationToken);

        _store.Verify(s => s.ReplaceByVectorDocumentIdAsync(_vectorDocumentId, It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()), Times.Once);
        VerifyOldEmbeddingsUntouched();
        _replacedWith.Should().NotBeNull();
        _replacedWith!.Should().HaveCount(25);
        _replacedWith.Select(e => e.ChunkIndex).Should().Equal(Enumerable.Range(0, 25), "in chunk order");
        _replacedWith.Select(e => e.SourceChunkId).Should().Equal(chunkIds.Cast<Guid?>(), "each embedding points at its chunk");
        _replacedWith.Should().OnlyContain(e => e.VectorDocumentId == _vectorDocumentId && e.Model == NewModel);

        var doc = await ReloadDocAsync();
        doc.EmbeddingModel.Should().Be(NewModel);
        doc.EmbeddingDimensions.Should().Be(4);
        doc.IndexingStatus.Should().Be("completed");
        doc.IndexingError.Should().BeNull();
        doc.IndexedAt.Should().BeAfter(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task WhenTheEmbeddingServiceFails_NothingIsDeleted_AndTheDocumentIsLeftAsItWas()
    {
        Seed(chunks: 25);
        // il secondo lotto fallisce: il primo è già stato generato
        _embeddings.SetupSequence(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(Enumerable.Range(0, 10).Select(_ => new[] { 0.1f }).ToList()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("embedding service down at 10.0.0.7:8001"));

        await CreateSut().ExecuteAsync(NewJob(), TestContext.Current.CancellationToken);

        _store.Verify(s => s.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyOldEmbeddingsUntouched();
        var doc = await ReloadDocAsync();
        doc.EmbeddingModel.Should().Be(OldModel, "the document still describes the embeddings that are still there");
        doc.EmbeddingDimensions.Should().Be(2);
        _log.Entries.Should().Contain(l => l.StartsWith("Error:", StringComparison.Ordinal) && l.Contains("10.0.0.7", StringComparison.Ordinal),
            "the failure is logged");
    }

    [Fact]
    public async Task WhenABatchComesBackWithTheWrongCount_NothingIsDeleted()
    {
        Seed(chunks: 5);
        _embeddings.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess([new[] { 0.1f }]));

        await CreateSut().ExecuteAsync(NewJob(), TestContext.Current.CancellationToken);

        _store.Verify(s => s.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyOldEmbeddingsUntouched();
        (await ReloadDocAsync()).EmbeddingModel.Should().Be(OldModel);
    }

    [Fact]
    public async Task WhenTheSwapFails_TheDocumentStillDescribesTheOldEmbeddings()
    {
        Seed(chunks: 3);
        EmbeddingsSucceed();
        _store.Setup(s => s.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("copy failed"));

        await CreateSut().ExecuteAsync(NewJob(), TestContext.Current.CancellationToken);

        VerifyOldEmbeddingsUntouched();
        var doc = await ReloadDocAsync();
        doc.EmbeddingModel.Should().Be(OldModel, "the swap rolled back: the old embeddings are still there, and so is their description");
        doc.IndexedAt.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task WhenTheDocumentHasNoTextChunks_NothingIsTouched()
    {
        Seed(chunks: 0);
        EmbeddingsSucceed();

        await CreateSut().ExecuteAsync(NewJob(), TestContext.Current.CancellationToken);

        _store.Verify(s => s.ReplaceByVectorDocumentIdAsync(It.IsAny<Guid>(), It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyOldEmbeddingsUntouched();
        (await ReloadDocAsync()).EmbeddingModel.Should().Be(OldModel);
    }
}
