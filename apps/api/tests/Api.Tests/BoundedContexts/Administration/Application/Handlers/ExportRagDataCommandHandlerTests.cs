using Api.BoundedContexts.Administration.Application.Commands.ExportRagData;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Defense-in-depth PII / info-leak guard for the per-document catch in
/// <c>ExportRagDataCommandHandler.Handle</c>. Mirrors the pattern enforced by
/// the BulkImportUsersCommandHandler / MigrateStorage / ImportRagData test suites
/// (PRs #1532, #1543) — the returned <c>ExportRagDataResult.Errors</c> payload is
/// exposed to admin API callers and propagated to log shipping pipelines, so it
/// must never carry raw <c>ex.Message</c> content. Storage backends (the dual-
/// write S3 + local in <c>IRagExportService</c>) throw exceptions whose Message
/// routinely includes bucket names, credential fragments, request IDs, and
/// internal paths.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ExportRagDataCommandHandlerTests
{
    private static MeepleAiDbContext CreateInMemoryDb(string testName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ExportRagDataTests_{testName}_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    [Fact]
    public async Task Handle_WhenExportServiceThrows_ShouldNotIncludeRawExceptionMessageInError()
    {
        // Arrange — populate a single completed VectorDocument so the per-document
        // foreach iterates exactly one entry; mock IRagExportService.ExportDocumentBundleAsync
        // to throw with a SECRET-MARKER message, which (pre-fix) would leak into
        // ExportRagDataResult.Errors[0] via `errors.Add($"...{ex.Message}")`.
        const string secretInError = "SECRET-MARKER-bucket=internal-prod-credentials";

        var gameId = Guid.NewGuid();
        var pdfDocId = Guid.NewGuid();
        var vectorDocId = Guid.NewGuid();

        await using var db = CreateInMemoryDb(nameof(Handle_WhenExportServiceThrows_ShouldNotIncludeRawExceptionMessageInError));

        var game = new SharedGameEntity
        {
            Id = gameId,
            Title = "testgame",
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };
        var pdfDoc = new PdfDocumentEntity
        {
            Id = pdfDocId,
            FileName = "rules.pdf",
            FilePath = "test/rules.pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            Language = "en",
            SharedGameId = gameId,
        };
        var vectorDoc = new VectorDocumentEntity
        {
            Id = vectorDocId,
            GameId = gameId,
            PdfDocumentId = pdfDocId,
            IndexingStatus = "completed",
            EmbeddingModel = "test-model",
            EmbeddingDimensions = 768,
            ChunkCount = 1,
            IndexedAt = DateTime.UtcNow,
            Game = game,
            PdfDocument = pdfDoc,
        };
        var chunk = new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            PdfDocumentId = pdfDocId,
            Content = "chunk content",
            ChunkIndex = 0,
            CharacterCount = 13,
            CreatedAt = DateTime.UtcNow,
        };

        db.SharedGames.Add(game);
        db.PdfDocuments.Add(pdfDoc);
        db.VectorDocuments.Add(vectorDoc);
        db.TextChunks.Add(chunk);
        await db.SaveChangesAsync();

        var mockExportService = new Mock<IRagExportService>();
        mockExportService
            .Setup(s => s.ExportDocumentBundleAsync(
                It.IsAny<string>(),
                It.IsAny<VectorDocumentEntity>(),
                It.IsAny<PdfDocumentEntity>(),
                It.IsAny<string>(),
                It.IsAny<List<TextChunkEntity>>(),
                It.IsAny<List<PgVectorEmbeddingEntity>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(secretInError));

        var handler = new ExportRagDataCommandHandler(
            db,
            mockExportService.Object,
            Mock.Of<ILogger<ExportRagDataCommandHandler>>());

        var command = new ExportRagDataCommand { DryRun = false };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Errors.Should().HaveCount(1,
            because: "the single completed vector document triggers exactly one per-document error");

        result.Errors[0].Should().NotContain("SECRET-MARKER",
            because: "per-document catch must not embed ex.Message — storage/EF exceptions " +
                     "routinely carry bucket names, credential fragments, SQL parameters, " +
                     "and backend internals. Full diagnostic detail is preserved server-side " +
                     "by the existing LogWarning(ex, ...) call.");
    }
}
