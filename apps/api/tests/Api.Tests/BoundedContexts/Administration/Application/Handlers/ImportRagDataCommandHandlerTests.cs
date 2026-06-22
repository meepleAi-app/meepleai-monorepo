using System.Text;
using Api.BoundedContexts.Administration.Application.Commands.ImportRagData;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
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
/// Defense-in-depth PII / info-leak guards for ImportRagDataCommandHandler.
/// Mirrors the pattern enforced by
/// <c>BulkImportUsersCommandHandlerTests.Handle_WhenPerLineImportFails_ShouldNotIncludeEmailInError</c>
/// and the PR #1532 hardening of BulkImportUsersCommandHandler — the returned
/// <c>ImportRagDataResult.Errors</c> payload is exposed to admin API callers and
/// propagated to log shipping pipelines (Loki/Datadog/etc.), so it must never carry
/// raw <c>ex.Message</c> content. JsonException messages quote the offending JSON
/// fragment (path leak); EF Core / storage exception messages routinely include
/// SQL fragments, parameter values, and storage-backend internals.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ImportRagDataCommandHandlerTests
{
    private static MeepleAiDbContext CreateInMemoryDb(string testName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ImportRagDataTests_{testName}_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    /// <summary>
    /// Guard for the catch block around <c>JsonSerializer.Deserialize&lt;RagExportManifest&gt;</c>.
    /// System.Text.Json's <c>JsonException.Message</c> reliably contains the JSON path,
    /// line number, and byte position of the failure (e.g. <c>"Path: $.totalDocuments |
    /// LineNumber: 0 | BytePositionInLine: 47"</c>). These reveal the structure of the
    /// uploaded manifest to API callers and to log shipping pipelines. The safe format
    /// (per PR #1532) surfaces only the exception type name.
    /// </summary>
    [Fact]
    public async Task Handle_WhenManifestDeserializationFails_ShouldNotIncludeRawExceptionMessageInError()
    {
        // Arrange — type-mismatch JSON: totalDocuments is declared as int in
        // RagExportManifest but the JSON provides a string. System.Text.Json
        // throws a JsonException whose Message includes "Path: $.totalDocuments
        // | LineNumber: ... | BytePositionInLine: ..." — patterns that would
        // leak through ImportRagDataResult.Errors if the handler embeds
        // ex.Message into the returned payload.
        const string invalidManifestJson =
            "{\"exportVersion\":\"1.0\",\"totalDocuments\":\"not-an-int\"}";
        var manifestBytes = Encoding.UTF8.GetBytes(invalidManifestJson);

        var mockStorage = new Mock<IRagBackupStorageService>();
        mockStorage
            .Setup(s => s.ReadFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifestBytes);

        await using var db = CreateInMemoryDb(nameof(Handle_WhenManifestDeserializationFails_ShouldNotIncludeRawExceptionMessageInError));
        var handler = new ImportRagDataCommandHandler(
            db,
            mockStorage.Object,
            Mock.Of<ILogger<ImportRagDataCommandHandler>>());

        var command = new ImportRagDataCommand { SnapshotPath = "test/snapshot" };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Errors.Should().HaveCount(1,
            because: "deserialize failure produces exactly one terminal error");

        // The leaky pattern (pre-fix): JsonException.Message includes "Path: $...",
        // "LineNumber: N", "BytePositionInLine: N" — none of which should reach API
        // callers. Post-fix, the error contains only the exception type name.
        result.Errors[0].Should().NotContain("Path:",
            because: "JsonException.Message includes 'Path: $.<property>' which leaks " +
                     "the manifest schema/structure to API callers and log pipelines");
        result.Errors[0].Should().NotContain("LineNumber:",
            because: "JsonException.Message includes 'LineNumber: N' which leaks " +
                     "internal file layout — full diagnostic detail belongs in ILogger output");
        result.Errors[0].Should().NotContain("BytePositionInLine:",
            because: "JsonException.Message includes 'BytePositionInLine: N' which leaks " +
                     "internal file layout — full diagnostic detail belongs in ILogger output");
    }

    /// <summary>
    /// Guard for the per-document catch block. A storage / EF Core exception thrown
    /// while importing a document must not leak its <c>ex.Message</c> (which can carry
    /// SQL fragments, parameter values, document IDs, or storage-backend internals).
    /// </summary>
    [Fact]
    public async Task Handle_WhenStorageReadDuringImportThrows_ShouldNotIncludeRawExceptionMessageInError()
    {
        // Arrange — valid manifest with one document entry whose game slug matches a
        // pre-seeded SharedGame. The per-document try block then calls ReadFileAsync
        // for metadata.json, which we mock to throw with a SECRET-MARKER message.
        const string secretInError = "SECRET-MARKER-storage-backend-internal";

        var gameId = Guid.NewGuid();
        await using var db = CreateInMemoryDb(nameof(Handle_WhenStorageReadDuringImportThrows_ShouldNotIncludeRawExceptionMessageInError));
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            // RagBackupPathHelper.Slugify strips non-alphanumeric chars (including dashes)
            // before re-hyphenating whitespace, so "testgame" round-trips to itself.
            Title = "testgame",
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        const string snapshotPath = "rag-exports/2026-05-25";
        var documentId = Guid.NewGuid();
        var entryPath = $"{snapshotPath}/documents/{documentId}";

        var validManifestJson = $$"""
            {
              "exportVersion": "1.0",
              "exportedAt": "2026-05-25T00:00:00Z",
              "totalDocuments": 1,
              "totalChunks": 0,
              "totalEmbeddings": 0,
              "embeddingModel": "test-model",
              "documents": [{
                "pdfDocumentId": "{{documentId}}",
                "gameSlug": "testgame",
                "gameName": "Test Game",
                "path": "{{entryPath}}",
                "chunks": 0,
                "embeddings": 0,
                "language": "en"
              }]
            }
            """;
        var manifestBytes = Encoding.UTF8.GetBytes(validManifestJson);
        var manifestPath = $"{snapshotPath}/manifest.json";
        var metadataPath = $"{entryPath}/metadata.json";

        var mockStorage = new Mock<IRagBackupStorageService>();
        mockStorage
            .Setup(s => s.ReadFileAsync(manifestPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifestBytes);
        mockStorage
            .Setup(s => s.ReadFileAsync(metadataPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(secretInError));

        var handler = new ImportRagDataCommandHandler(
            db,
            mockStorage.Object,
            Mock.Of<ILogger<ImportRagDataCommandHandler>>());

        var command = new ImportRagDataCommand { SnapshotPath = snapshotPath };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Errors.Should().HaveCount(1,
            because: "the single document entry failed; exactly one per-document error is expected");

        result.Errors[0].Should().NotContain("SECRET-MARKER",
            because: "per-document catch must not embed ex.Message — storage/EF errors " +
                     "routinely carry SQL fragments, parameter values, and backend internals. " +
                     "Full diagnostic detail remains in ILogger output (LogWarning).");
    }
}
