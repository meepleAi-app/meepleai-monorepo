using System.Text;
using Api.BoundedContexts.Administration.Application.Commands.ImportRagData;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
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
            Mock.Of<IEmbeddingService>(),
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
            Mock.Of<IEmbeddingService>(),
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

        var handler = new ImportRagDataCommandHandler(db, storage.Object, Mock.Of<IEmbeddingService>(), Mock.Of<ILogger<ImportRagDataCommandHandler>>());

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

        var handler = new ImportRagDataCommandHandler(db, storage.Object, Mock.Of<IEmbeddingService>(), Mock.Of<ILogger<ImportRagDataCommandHandler>>());

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

    // ── #4725: ReEmbed=true ───────────────────────────────────────────────────────────────────────────────
    // L'import con ReEmbed lasciava un PDF "Ready" con un documento vettoriale "pending" e nessun embedding,
    // e niente lo riprendeva. Ora gli embedding si calcolano dal testo dei passaggi appena letti, prima del
    // salvataggio: o il documento entra completo, o non entra.

    private const string ReEmbedSnapshot = "rag-exports/2026-10-29";

    private sealed record ReEmbedImport(
        ImportRagDataResult Result,
        MeepleAiDbContext Db,
        Mock<IRagBackupStorageService> Storage,
        string EntryPath);

    private static async Task<ReEmbedImport> ImportWithReEmbedAsync(
        string testName,
        IEmbeddingService embeddingService,
        (int Index, int Page, string Text)[] chunks,
        string language = "en",
        int documentCount = 1)
    {
        var db = CreateInMemoryDb(testName);
        db.SharedGames.Add(new SharedGameEntity { Id = Guid.NewGuid(), Title = "testgame", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var documentIds = Enumerable.Range(0, documentCount).Select(_ => Guid.NewGuid()).ToList();
        string EntryPathOf(Guid id) => $"{ReEmbedSnapshot}/documents/{id}";
        var entries = string.Join(",", documentIds.Select(id => $$"""
            { "pdfDocumentId": "{{id}}", "gameSlug": "testgame", "gameName": "Test Game",
              "path": "{{EntryPathOf(id)}}", "chunks": {{chunks.Length}}, "embeddings": {{chunks.Length}}, "language": "{{language}}" }
            """));
        var storage = new Mock<IRagBackupStorageService>();
        storage.Setup(s => s.ReadFileAsync($"{ReEmbedSnapshot}/manifest.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes($$"""
                { "exportVersion": "1.0", "exportedAt": "2026-10-29T00:00:00Z", "totalDocuments": {{documentCount}},
                  "totalChunks": {{chunks.Length * documentCount}}, "totalEmbeddings": {{chunks.Length * documentCount}},
                  "embeddingModel": "old-model", "documents": [{{entries}}] }
                """));
        var chunksFile = await RagParquetSerializer.SerializeChunksAsync(
            chunks.Select(c => new RagExportChunkLine(c.Index, c.Page, c.Text, c.Text.Length)).ToList());
        foreach (var documentId in documentIds)
        {
            var documentPath = EntryPathOf(documentId); // a local function cannot appear inside a Moq expression
            storage.Setup(s => s.ReadFileAsync($"{documentPath}/metadata.json", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Encoding.UTF8.GetBytes($$"""
                    { "exportVersion": "1.0", "exportedAt": "2026-10-29T00:00:00Z", "embeddingModel": "old-model",
                      "embeddingDimensions": 1024,
                      "document": { "pdfDocumentId": "{{documentId}}", "gameId": null, "gameSlug": "testgame",
                                    "gameName": "Test Game", "fileName": "rules.pdf", "language": "{{language}}",
                                    "languageConfidence": null, "documentCategory": null, "versionLabel": null,
                                    "licenseType": null, "pageCount": 2, "characterCount": 30, "contentHash": null,
                                    "fileSizeBytes": null },
                      "stats": { "totalChunks": {{chunks.Length}}, "totalEmbeddings": {{chunks.Length}}, "chunkSizeAvg": 10 } }
                    """));
            storage.Setup(s => s.ReadFileAsync($"{documentPath}/chunks.parquet", It.IsAny<CancellationToken>()))
                .ReturnsAsync(chunksFile);
        }
        var entryPath = EntryPathOf(documentIds[0]);

        var handler = new ImportRagDataCommandHandler(db, storage.Object, embeddingService, Mock.Of<ILogger<ImportRagDataCommandHandler>>());
        var result = await handler.Handle(
            new ImportRagDataCommand { SnapshotPath = ReEmbedSnapshot, ReEmbed = true }, CancellationToken.None);

        return new ReEmbedImport(result, db, storage, entryPath);
    }

    /// <summary>Un servizio che risponde con un vettore per testo: la prima componente è la lunghezza del testo.</summary>
    private static Mock<IEmbeddingService> WorkingEmbeddingService(string model = "new-model", int dimensions = 4)
    {
        var service = new Mock<IEmbeddingService>();
        service.Setup(e => e.GetModelName()).Returns(model);
        service.Setup(e => e.GetEmbeddingDimensions()).Returns(dimensions);
        service.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken _) =>
                EmbeddingResult.CreateSuccess(texts.Select(t => new[] { (float)t.Length, 0.5f, 0.5f, 0.5f }).ToList()));
        return service;
    }

    [Fact]
    public async Task Handle_ReEmbed_EmbedsTheImportedChunks_AndLeavesTheDocumentCompleted()
    {
        // Arrange: quindici passaggi, più dei dieci per chiamata, così il test attraversa anche il confine fra due lotti.
        var chunks = Enumerable.Range(0, 15)
            .Select(i => (Index: i, Page: 1 + (i / 5), Text: $"Passage {i}{new string('x', i)}"))
            .ToArray();
        var service = WorkingEmbeddingService();

        // Act
        var import = await ImportWithReEmbedAsync(nameof(Handle_ReEmbed_EmbedsTheImportedChunks_AndLeavesTheDocumentCompleted), service.Object, chunks);
        await using var db = import.Db;

        // Assert
        import.Result.Imported.Should().Be(1, because: string.Join("; ", import.Result.Errors.Concat(import.Result.Warnings)));
        import.Result.ReEmbedded.Should().Be(1);

        var vectorDocument = await db.VectorDocuments.SingleAsync();
        vectorDocument.IndexingStatus.Should().Be("completed", "nessun documento resta in attesa di qualcuno che lo riprenda");
        vectorDocument.EmbeddingModel.Should().Be("new-model", "il documento descrive gli embedding che ha, non quelli del backup");
        vectorDocument.EmbeddingDimensions.Should().Be(4);
        vectorDocument.ChunkCount.Should().Be(chunks.Length);
        (await db.PdfDocuments.SingleAsync()).ProcessingState.Should().Be("Ready");

        var chunkIdByIndex = await db.TextChunks.ToDictionaryAsync(tc => tc.ChunkIndex, tc => tc.Id);
        var rows = await db.PgVectorEmbeddings.OrderBy(e => e.ChunkIndex).ToListAsync();
        rows.Should().HaveCount(chunks.Length);
        rows.Should().OnlyContain(r => r.VectorDocumentId == vectorDocument.Id && r.Model == "new-model" && r.Lang == "en");
        rows.Should().OnlyContain(r => r.SourceChunkId == chunkIdByIndex[r.ChunkIndex],
            "ogni embedding punta al passaggio da cui è stato calcolato");
        // Il provider in memoria non conserva la colonna del vettore, e la lettura è senza tracking: il vettore
        // si legge dalle istanze che il contesto ha appena salvato.
        var saved = db.ChangeTracker.Entries<PgVectorEmbeddingEntity>().Select(e => e.Entity).OrderBy(e => e.ChunkIndex).ToList();
        saved.Should().HaveCount(chunks.Length);
        for (var i = 0; i < chunks.Length; i++)
        {
            rows[i].TextContent.Should().Be(chunks[i].Text);
            rows[i].PageNumber.Should().Be(chunks[i].Page);
            saved[i].Vector.ToArray()[0].Should().Be(chunks[i].Text.Length, "il vettore è quello del testo di quel passaggio, in ordine");
        }

        service.Verify(
            e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        import.Storage.Verify(
            s => s.ReadFileAsync($"{import.EntryPath}/embeddings.parquet", It.IsAny<CancellationToken>()),
            Times.Never);
        import.Result.Warnings.Should().NotContain(w => w.Contains("re-embedding required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_ReEmbed_TagsTheEmbeddingsWithTheDocumentLanguage()
    {
        var import = await ImportWithReEmbedAsync(
            nameof(Handle_ReEmbed_TagsTheEmbeddingsWithTheDocumentLanguage),
            WorkingEmbeddingService().Object,
            [(0, 1, "Prepara il tabellone.")],
            language: "it");
        await using var db = import.Db;

        import.Result.Imported.Should().Be(1);
        (await db.PgVectorEmbeddings.SingleAsync()).Lang.Should().Be("it");
    }

    [Fact]
    public async Task Handle_ReEmbed_TwoChunksOnOneIndex_EachEmbeddingPointsAtItsOwnChunk()
    {
        // L'ambiguità dell'indice riguarda gli embedding importati, che arrivano con il solo indice. Qui il
        // vettore nasce dal testo di un passaggio preciso: il collegamento è certo, e il warning sarebbe falso.
        var import = await ImportWithReEmbedAsync(
            nameof(Handle_ReEmbed_TwoChunksOnOneIndex_EachEmbeddingPointsAtItsOwnChunk),
            WorkingEmbeddingService().Object,
            [(0, 1, "Setup the board."), (1, 1, "Each player draws."), (1, 1, "Each player discards.")]);
        await using var db = import.Db;

        import.Result.Imported.Should().Be(1);
        var chunkIdByContent = await db.TextChunks.ToDictionaryAsync(tc => tc.Content, tc => tc.Id);
        var rows = await db.PgVectorEmbeddings.ToListAsync();
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(r => r.SourceChunkId == chunkIdByContent[r.TextContent]);
        import.Result.Warnings.Should().NotContain(w => w.Contains("without a link", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Handle_ReEmbed_WhenTheEmbeddingServiceFails_ImportsNothingOfThatDocument()
    {
        // Il guasto del servizio non deve lasciare un PDF "Ready" senza embedding: il documento non entra.
        const string serviceDetail = "embedding service down at 10.0.0.7:8001";
        var service = WorkingEmbeddingService();
        service.SetupSequence(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(Enumerable.Range(0, 10).Select(_ => new[] { 0.1f, 0.2f, 0.3f, 0.4f }).ToList()))
            .ReturnsAsync(EmbeddingResult.CreateFailure(serviceDetail));
        var chunks = Enumerable.Range(0, 15).Select(i => (Index: i, Page: 1, Text: $"Passage {i}")).ToArray();

        var import = await ImportWithReEmbedAsync(nameof(Handle_ReEmbed_WhenTheEmbeddingServiceFails_ImportsNothingOfThatDocument), service.Object, chunks);
        await using var db = import.Db;

        import.Result.Imported.Should().Be(0);
        import.Result.Failed.Should().Be(1);
        import.Result.ReEmbedded.Should().Be(0);
        (await db.PdfDocuments.CountAsync()).Should().Be(0);
        (await db.VectorDocuments.CountAsync()).Should().Be(0);
        (await db.TextChunks.CountAsync()).Should().Be(0);
        (await db.PgVectorEmbeddings.CountAsync()).Should().Be(0);
        import.Result.Errors.Should().ContainSingle().Which.Should().NotContain("10.0.0.7",
            "l'errore restituito all'admin porta il tipo dell'eccezione, il dettaglio sta nel log");
    }

    // Meno vettori dei testi lascerebbe un passaggio senza embedding; più vettori ne sposterebbero uno sul passaggio
    // sbagliato senza che nulla si accorga: il secondo caso non fallisce da solo, va controllato.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Handle_ReEmbed_WhenTheServiceReturnsTheWrongNumberOfVectors_ImportsNothingOfThatDocument(int vectorsReturned)
    {
        var service = WorkingEmbeddingService();
        service.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(
                Enumerable.Range(0, vectorsReturned).Select(_ => new[] { 0.1f, 0.2f, 0.3f, 0.4f }).ToList()));

        var import = await ImportWithReEmbedAsync(
            nameof(Handle_ReEmbed_WhenTheServiceReturnsTheWrongNumberOfVectors_ImportsNothingOfThatDocument) + vectorsReturned,
            service.Object,
            [(0, 1, "One."), (1, 1, "Two.")]);
        await using var db = import.Db;

        import.Result.Failed.Should().Be(1);
        (await db.PdfDocuments.CountAsync()).Should().Be(0);
        (await db.PgVectorEmbeddings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReEmbed_AResultThatSaysFailureIsAFailureEvenIfItCarriesVectors()
    {
        // Il conteggio dei vettori non basta: un servizio che dichiara il fallimento e allega comunque i vettori
        // (parziali, o di una risposta di ripiego) non deve far entrare il documento.
        var service = WorkingEmbeddingService();
        service.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "degraded",
                Embeddings = [[0.1f, 0.2f, 0.3f, 0.4f], [0.1f, 0.2f, 0.3f, 0.4f]],
            });

        var import = await ImportWithReEmbedAsync(
            nameof(Handle_ReEmbed_AResultThatSaysFailureIsAFailureEvenIfItCarriesVectors),
            service.Object,
            [(0, 1, "One."), (1, 1, "Two.")]);
        await using var db = import.Db;

        import.Result.Failed.Should().Be(1);
        (await db.PdfDocuments.CountAsync()).Should().Be(0);
        (await db.PgVectorEmbeddings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_ReEmbed_AFailedDocumentDoesNotLeakIntoTheNextOne()
    {
        // Il primo documento ha già PDF, documento vettoriale e passaggi nel tracker quando il servizio fallisce.
        // Il secondo si salva con lo stesso contesto: non deve portarsi dietro quello che il primo ha lasciato.
        var service = WorkingEmbeddingService();
        service.SetupSequence(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("embedding service down"))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(
                Enumerable.Range(0, 3).Select(_ => new[] { 0.1f, 0.2f, 0.3f, 0.4f }).ToList()));

        var import = await ImportWithReEmbedAsync(
            nameof(Handle_ReEmbed_AFailedDocumentDoesNotLeakIntoTheNextOne),
            service.Object,
            [(0, 1, "One."), (1, 1, "Two."), (2, 1, "Three.")],
            documentCount: 2);
        await using var db = import.Db;

        import.Result.Imported.Should().Be(1);
        import.Result.Failed.Should().Be(1);
        import.Result.ReEmbedded.Should().Be(1);
        (await db.PdfDocuments.CountAsync()).Should().Be(1);
        (await db.VectorDocuments.CountAsync()).Should().Be(1);
        (await db.TextChunks.CountAsync()).Should().Be(3, "i passaggi del documento fallito non sono stati salvati con l'altro");
        (await db.PgVectorEmbeddings.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithoutReEmbed_NeverCallsTheEmbeddingService()
    {
        // Il ramo che importa gli embedding salvati non cambia: niente chiamata al servizio.
        var service = WorkingEmbeddingService();
        var db = CreateInMemoryDb(nameof(Handle_WithoutReEmbed_NeverCallsTheEmbeddingService));
        await using var _ = db;
        db.SharedGames.Add(new SharedGameEntity { Id = Guid.NewGuid(), Title = "testgame", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var documentId = Guid.NewGuid();
        var entryPath = $"{ReEmbedSnapshot}/documents/{documentId}";
        var storage = new Mock<IRagBackupStorageService>();
        storage.Setup(s => s.ReadFileAsync($"{ReEmbedSnapshot}/manifest.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes($$"""
                { "exportVersion": "1.0", "exportedAt": "2026-10-29T00:00:00Z", "totalDocuments": 1, "totalChunks": 1,
                  "totalEmbeddings": 1, "embeddingModel": "old-model",
                  "documents": [{ "pdfDocumentId": "{{documentId}}", "gameSlug": "testgame", "gameName": "Test Game",
                                  "path": "{{entryPath}}", "chunks": 1, "embeddings": 1, "language": "en" }] }
                """));
        storage.Setup(s => s.ReadFileAsync($"{entryPath}/metadata.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes($$"""
                { "exportVersion": "1.0", "exportedAt": "2026-10-29T00:00:00Z", "embeddingModel": "old-model",
                  "embeddingDimensions": 4,
                  "document": { "pdfDocumentId": "{{documentId}}", "gameId": null, "gameSlug": "testgame",
                                "gameName": "Test Game", "fileName": "rules.pdf", "language": "en",
                                "languageConfidence": null, "documentCategory": null, "versionLabel": null,
                                "licenseType": null, "pageCount": 1, "characterCount": 5, "contentHash": null,
                                "fileSizeBytes": null },
                  "stats": { "totalChunks": 1, "totalEmbeddings": 1, "chunkSizeAvg": 5 } }
                """));
        storage.Setup(s => s.ReadFileAsync($"{entryPath}/chunks.parquet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(await RagParquetSerializer.SerializeChunksAsync([new(0, 1, "Hello", 5)]));
        storage.Setup(s => s.ReadFileAsync($"{entryPath}/embeddings.parquet", It.IsAny<CancellationToken>()))
            .ReturnsAsync(await RagParquetSerializer.SerializeEmbeddingsAsync([new(0, 1, "Hello", [0.1f, 0.2f, 0.3f, 0.4f], "old-model")]));

        var handler = new ImportRagDataCommandHandler(db, storage.Object, service.Object, Mock.Of<ILogger<ImportRagDataCommandHandler>>());

        var result = await handler.Handle(new ImportRagDataCommand { SnapshotPath = ReEmbedSnapshot }, CancellationToken.None);

        result.Imported.Should().Be(1);
        result.ReEmbedded.Should().Be(0);
        (await db.VectorDocuments.SingleAsync()).EmbeddingModel.Should().Be("old-model");
        service.Verify(
            e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
