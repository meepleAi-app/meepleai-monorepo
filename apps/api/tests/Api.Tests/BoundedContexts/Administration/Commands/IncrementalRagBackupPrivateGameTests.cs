using Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Commands;

/// <summary>
/// Issue #4244. A ogni documento indicizzato il backup incrementale copia passaggi ed embedding
/// nello snapshot RAG condiviso. È la copia del catalogo: l'importazione sa ripristinare solo
/// giochi condivisi. Finché il PDF di un gioco privato non si poteva indicizzare l'evento non
/// scattava mai; ora scatta, e il testo del manuale privato di un utente non deve finire lì.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4244")]
public sealed class IncrementalRagBackupPrivateGameTests : IDisposable
{
    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly Mock<IRagExportService> _export = new();
    private readonly IncrementalRagBackupCommandHandler _sut;

    public IncrementalRagBackupPrivateGameTests()
    {
        _export
            .Setup(e => e.ReadManifestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RagExportManifest?)null);

        _sut = new IncrementalRagBackupCommandHandler(
            _db, _export.Object, NullLogger<IncrementalRagBackupCommandHandler>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task TheDocumentOfAPrivateGame_IsNotCopiedIntoTheSharedSnapshot()
    {
        var pdfId = await SeedIndexedPdfAsync(sharedGame: null, privateGameId: Guid.NewGuid());

        var result = await _sut.Handle(new IncrementalRagBackupCommand(pdfId), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue("non copiare un documento privato non è un guasto del backup");
        _export.Verify(
            e => e.ExportDocumentBundleAsync(
                It.IsAny<string>(), It.IsAny<VectorDocumentEntity>(), It.IsAny<PdfDocumentEntity>(), It.IsAny<string>(),
                It.IsAny<List<TextChunkEntity>>(), It.IsAny<List<PgVectorEmbeddingEntity>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _export.Verify(
            e => e.WriteManifestAsync(It.IsAny<string>(), It.IsAny<RagExportManifest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Il controllo: il documento di un gioco del catalogo viene copiato come prima. Prova anche
    /// che il banco arriva davvero all'esportazione, cioè che il test qui sopra non passa perché il
    /// gestore si è fermato prima per un altro motivo.
    /// </summary>
    [Fact]
    public async Task TheDocumentOfASharedGame_IsCopied()
    {
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Gioco del catalogo",
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };
        _db.SharedGames.Add(game);
        var pdfId = await SeedIndexedPdfAsync(sharedGame: game, privateGameId: null);

        var result = await _sut.Handle(new IncrementalRagBackupCommand(pdfId), TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue(result.Error);
        _export.Verify(
            e => e.ExportDocumentBundleAsync(
                "latest", It.IsAny<VectorDocumentEntity>(), It.Is<PdfDocumentEntity>(p => p.Id == pdfId), "Gioco del catalogo",
                It.IsAny<List<TextChunkEntity>>(), It.IsAny<List<PgVectorEmbeddingEntity>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private async Task<Guid> SeedIndexedPdfAsync(SharedGameEntity? sharedGame, Guid? privateGameId)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready",
            SharedGameId = sharedGame?.Id,
            PrivateGameId = privateGameId,
        };
        _db.PdfDocuments.Add(pdf);
        _db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGame?.Id,
            SharedGameId = sharedGame?.Id,
            PrivateGameId = privateGameId,
            PdfDocumentId = pdf.Id,
            ChunkCount = 1,
            TotalCharacters = 30,
            IndexingStatus = "completed",
            EmbeddingModel = "test-model",
            EmbeddingDimensions = 768,
        });
        _db.TextChunks.Add(new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGame?.Id,
            PrivateGameId = privateGameId,
            PdfDocumentId = pdf.Id,
            Content = "Each player draws five cards.",
            ChunkIndex = 0,
            PageNumber = 1,
            CharacterCount = 29,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return pdf.Id;
    }
}
