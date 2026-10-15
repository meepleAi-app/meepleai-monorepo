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

    /// <summary>
    /// #4384: ogni riga di pgvector_embeddings importata punta al text_chunks importato con lo stesso
    /// ChunkIndex. Senza il collegamento il braccio vettoriale non ne legge heading, regioni e offset
    /// (LEFT JOIN su source_chunk_id), e la valutazione di KbQuality conta come mancati i passaggi
    /// trovati solo da quel braccio.
    /// </summary>
    [Fact]
    public async Task Handle_LinksEveryImportedEmbeddingToTheChunkAtTheSameIndex()
    {
        // Arrange
        await using var db = CreateInMemoryDb(nameof(Handle_LinksEveryImportedEmbeddingToTheChunkAtTheSameIndex));
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "testgame",
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        const string snapshotPath = "rag-exports/2026-10-15";
        var documentId = Guid.NewGuid();
        var entryPath = $"{snapshotPath}/documents/{documentId}";
        var manifestJson = $$"""
            {
              "exportVersion": "1.0", "exportedAt": "2026-10-15T00:00:00Z",
              "totalDocuments": 1, "totalChunks": 3, "totalEmbeddings": 3, "embeddingModel": "test-model",
              "documents": [{ "pdfDocumentId": "{{documentId}}", "gameSlug": "testgame", "gameName": "Test Game",
                              "path": "{{entryPath}}", "chunks": 3, "embeddings": 3, "language": "en" }]
            }
            """;
        var metadataJson = $$"""
            {
              "exportVersion": "1.0", "exportedAt": "2026-10-15T00:00:00Z",
              "embeddingModel": "test-model", "embeddingDimensions": 4,
              "document": { "pdfDocumentId": "{{documentId}}", "gameId": null, "gameSlug": "testgame",
                            "gameName": "Test Game", "fileName": "rules.pdf", "language": "en",
                            "languageConfidence": null, "documentCategory": null, "versionLabel": null,
                            "licenseType": null, "pageCount": 2, "characterCount": 30, "contentHash": null,
                            "fileSizeBytes": null },
              "stats": { "totalChunks": 3, "totalEmbeddings": 3, "chunkSizeAvg": 10 }
            }
            """;
        // Gli embedding arrivano in un ordine diverso dai passaggi: il collegamento va per indice, non
        // per posizione.
        var chunks = await RagParquetSerializer.SerializeChunksAsync(
        [
            new(0, 1, "Setup the board.", 16),
            new(1, 1, "Each player draws.", 18),
            new(2, 2, "The game ends.", 14),
        ]);
        var embeddings = await RagParquetSerializer.SerializeEmbeddingsAsync(
        [
            new(2, 2, "The game ends.", [0.3f, 0.1f, 0.2f, 0.4f], "test-model"),
            new(0, 1, "Setup the board.", [0.1f, 0.2f, 0.3f, 0.4f], "test-model"),
            new(1, 1, "Each player draws.", [0.2f, 0.3f, 0.4f, 0.1f], "test-model"),
        ]);

        var storage = new Mock<IRagBackupStorageService>();
        storage.Setup(s => s.ReadFileAsync($"{snapshotPath}/manifest.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(manifestJson));
        storage.Setup(s => s.ReadFileAsync($"{entryPath}/metadata.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(metadataJson));
        storage.Setup(s => s.ReadFileAsync($"{entryPath}/chunks.parquet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);
        storage.Setup(s => s.ReadFileAsync($"{entryPath}/embeddings.parquet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);

        var handler = new ImportRagDataCommandHandler(db, storage.Object, Mock.Of<ILogger<ImportRagDataCommandHandler>>());

        // Act
        var result = await handler.Handle(new ImportRagDataCommand { SnapshotPath = snapshotPath }, CancellationToken.None);

        // Assert
        result.Imported.Should().Be(1, because: string.Join("; ", result.Errors.Concat(result.Warnings)));
        var chunkIdByIndex = await db.TextChunks.ToDictionaryAsync(tc => tc.ChunkIndex, tc => tc.Id);
        var rows = await db.PgVectorEmbeddings.ToListAsync();

        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(row => row.SourceChunkId == chunkIdByIndex[row.ChunkIndex],
            "ogni riga deve puntare al passaggio con lo stesso ChunkIndex");
    }

    /// <summary>
    /// #4384, dalla revisione: due passaggi sullo stesso indice sono ambigui. L'embedding di quell'indice
    /// non si collega a nessuno dei due, e l'import lo dice nei warning invece di sceglierne uno in silenzio.
    /// </summary>
    [Fact]
    public async Task Handle_TwoChunksOnOneIndex_LeavesThatEmbeddingUnlinked_AndWarns()
    {
        // Arrange
        await using var db = CreateInMemoryDb(nameof(Handle_TwoChunksOnOneIndex_LeavesThatEmbeddingUnlinked_AndWarns));
        db.SharedGames.Add(new SharedGameEntity { Id = Guid.NewGuid(), Title = "testgame", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        const string snapshotPath = "rag-exports/2026-10-15b";
        var documentId = Guid.NewGuid();
        var entryPath = $"{snapshotPath}/documents/{documentId}";
        var storage = new Mock<IRagBackupStorageService>();
        storage.Setup(s => s.ReadFileAsync($"{snapshotPath}/manifest.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes($$"""
                { "exportVersion": "1.0", "exportedAt": "2026-10-15T00:00:00Z", "totalDocuments": 1, "totalChunks": 3,
                  "totalEmbeddings": 2, "embeddingModel": "test-model",
                  "documents": [{ "pdfDocumentId": "{{documentId}}", "gameSlug": "testgame", "gameName": "Test Game",
                                  "path": "{{entryPath}}", "chunks": 3, "embeddings": 2, "language": "en" }] }
                """));
        storage.Setup(s => s.ReadFileAsync($"{entryPath}/metadata.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes($$"""
                { "exportVersion": "1.0", "exportedAt": "2026-10-15T00:00:00Z", "embeddingModel": "test-model",
                  "embeddingDimensions": 4,
                  "document": { "pdfDocumentId": "{{documentId}}", "gameId": null, "gameSlug": "testgame",
                                "gameName": "Test Game", "fileName": "rules.pdf", "language": "en",
                                "languageConfidence": null, "documentCategory": null, "versionLabel": null,
                                "licenseType": null, "pageCount": 1, "characterCount": 30, "contentHash": null,
                                "fileSizeBytes": null },
                  "stats": { "totalChunks": 3, "totalEmbeddings": 2, "chunkSizeAvg": 10 } }
                """));
        storage.Setup(s => s.ReadFileAsync($"{entryPath}/chunks.parquet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(await RagParquetSerializer.SerializeChunksAsync(
            [
                new(0, 1, "Setup the board.", 16),
                new(1, 1, "Each player draws.", 18),
                new(1, 1, "Each player discards.", 21),
            ]));
        storage.Setup(s => s.ReadFileAsync($"{entryPath}/embeddings.parquet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(await RagParquetSerializer.SerializeEmbeddingsAsync(
            [
                new(0, 1, "Setup the board.", [0.1f, 0.2f, 0.3f, 0.4f], "test-model"),
                new(1, 1, "Each player draws.", [0.2f, 0.3f, 0.4f, 0.1f], "test-model"),
            ]));

        var handler = new ImportRagDataCommandHandler(db, storage.Object, Mock.Of<ILogger<ImportRagDataCommandHandler>>());

        // Act
        var result = await handler.Handle(new ImportRagDataCommand { SnapshotPath = snapshotPath }, CancellationToken.None);

        // Assert
        result.Imported.Should().Be(1);
        var rows = await db.PgVectorEmbeddings.ToDictionaryAsync(e => e.ChunkIndex);
        rows[0].SourceChunkId.Should().NotBeNull("l'indice 0 ha un solo passaggio");
        rows[1].SourceChunkId.Should().BeNull("l'indice 1 ha due passaggi: nessuno dei due è quello giusto per certo");
        result.Warnings.Should().Contain(w => w.Contains("more than once"));
        result.Warnings.Should().Contain(w => w.Contains("without a link"));
    }
}
