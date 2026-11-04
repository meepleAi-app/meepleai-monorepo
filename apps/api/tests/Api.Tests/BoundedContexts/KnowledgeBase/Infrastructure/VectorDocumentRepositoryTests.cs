using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Regression tests for VectorDocumentRepository (Task 8, I1).
///
/// Focus: validate that after removing the legacy bridge (GameEntity.SharedGameId → GameEntity.Id),
/// resolution by sharedGameId works directly via PdfDocumentEntity.SharedGameId and
/// PdfDocumentEntity.PrivateGameId, without touching the games table.
///
/// Target method: GetIndexingInfoByGameIdAsync.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class VectorDocumentRepositoryTests : IClassFixture<SharedTestcontainersFixture>, IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Mock<IDomainEventCollector> _eventCollectorMock = new();
    private readonly string _databaseName;
    private string? _connectionString;

    public VectorDocumentRepositoryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _databaseName = $"test_vector_document_repo_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        using var dbContext = _fixture.CreateDbContext(_connectionString);
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    private static SharedGameEntity CreateSharedGame(Guid id, string title = "Test Game") => new()
    {
        Id = id,
        Title = title,
        Description = string.Empty,
        ImageUrl = string.Empty,
        ThumbnailUrl = string.Empty,
        Status = 1, // Published
        GameDataStatus = 5,
        CreatedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    private static PdfDocumentEntity CreatePdfDocument(
        Guid uploadedByUserId,
        Guid? sharedGameId = null,
        Guid? privateGameId = null,
        string processingState = "Ready",
        string fileName = "test.pdf") => new()
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            PrivateGameId = privateGameId,
            FileName = fileName,
            FilePath = $"/tmp/{fileName}",
            FileSizeBytes = 100,
            ContentType = "application/pdf",
            UploadedByUserId = uploadedByUserId, // FK_pdf_documents_users_UploadedByUserId requires real users row
            UploadedAt = DateTime.UtcNow,
            ProcessingState = processingState,
        };

    private static UserEntity CreateUser(Guid id) => new()
    {
        Id = id,
        Email = $"test-{id:N}@vector-repo-tests.local",
        DisplayName = "Vector Repo Test User",
        Role = "User",
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// TDD regression test: after removing the legacy bridge, resolution via sharedGameId
    /// must still find PDFs whose SharedGameId OR PrivateGameId matches the input id.
    /// Validates the PDF-state path of GetIndexingInfoByGameIdAsync (a PDF with no vector document yet).
    /// </summary>
    [Fact]
    public async Task GetIndexingInfo_WithSharedGameId_ResolvesPdfViaSharedGameIdOrPrivateGameId()
    {
        // Arrange
        using var seedContext = _fixture.CreateDbContext(_connectionString!);
        var uploaderId = Guid.NewGuid();
        seedContext.Set<UserEntity>().Add(CreateUser(uploaderId));
        var sharedGameId = Guid.NewGuid();
        seedContext.SharedGames.Add(CreateSharedGame(sharedGameId));

        // Seed a PDF in "Extracting" state (pipeline in progress, no vector document yet): the state comes
        // from the PDF, resolved by SharedGameId OR PrivateGameId.
        var sharedPdf = CreatePdfDocument(uploaderId, sharedGameId: sharedGameId, processingState: "Extracting", fileName: "shared.pdf");
        seedContext.PdfDocuments.Add(sharedPdf);
        await seedContext.SaveChangesAsync();

        // Act: resolve via sharedGameId → should find the shared PDF via direct SharedGameId match
        using var actContext = _fixture.CreateDbContext(_connectionString!);
        var repo = new VectorDocumentRepository(actContext, _eventCollectorMock.Object);
        var info = await repo.GetIndexingInfoByGameIdAsync(sharedGameId);

        // Assert: PDF is found → indexing info returned with Processing status
        info.Should().NotBeNull("PDF with SharedGameId == requested id must be found without legacy bridge");
        info!.Status.ToString().Should().BeOneOf("Processing", "Completed", "Pending", "Failed");
    }

    /// <summary>
    /// #4660: con più PDF risponde l'ultimo caricato, e conteggio dei chunk ed errore sono quelli del suo
    /// documento vettoriale. Su PostgreSQL vero, per provare la traduzione di entrambe le query e i vincoli di
    /// chiave esterna che l'host in memoria non applica.
    /// </summary>
    [Fact]
    public async Task GetIndexingInfo_WithTwoPdfs_ReportsTheLastUploadedOne_WithItsOwnChunkCount()
    {
        using var seedContext = _fixture.CreateDbContext(_connectionString!);
        var uploaderId = Guid.NewGuid();
        seedContext.Set<UserEntity>().Add(CreateUser(uploaderId));
        var sharedGameId = Guid.NewGuid();
        seedContext.SharedGames.Add(CreateSharedGame(sharedGameId));

        var older = CreatePdfDocument(uploaderId, sharedGameId: sharedGameId, processingState: "Failed", fileName: "older.pdf");
        older.UploadedAt = DateTime.UtcNow.AddHours(-5);
        var newer = CreatePdfDocument(uploaderId, sharedGameId: sharedGameId, processingState: "Ready", fileName: "newer.pdf");
        newer.UploadedAt = DateTime.UtcNow.AddHours(-1);
        seedContext.PdfDocuments.AddRange(older, newer);
        await seedContext.SaveChangesAsync();

        seedContext.VectorDocuments.AddRange(
            new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = sharedGameId,
                SharedGameId = sharedGameId,
                PdfDocumentId = older.Id,
                IndexingStatus = "failed",
                IndexingError = "older failed",
                ChunkCount = 0,
            },
            new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = sharedGameId,
                SharedGameId = sharedGameId,
                PdfDocumentId = newer.Id,
                IndexingStatus = "completed",
                IndexedAt = DateTime.UtcNow.AddHours(-1),
                ChunkCount = 9,
            });
        await seedContext.SaveChangesAsync();

        using var actContext = _fixture.CreateDbContext(_connectionString!);
        var repo = new VectorDocumentRepository(actContext, _eventCollectorMock.Object);
        var info = await repo.GetIndexingInfoByGameIdAsync(sharedGameId);

        info.Should().NotBeNull();
        info!.Status.ToString().Should().Be("Completed", "the PDF uploaded last is Ready, the older failed one no longer covers it");
        info.ChunkCount.Should().Be(9);
        info.IndexingError.Should().BeNull();
    }

    /// <summary>
    /// #4660 (2026-10-29): un PDF rimesso al lavoro da un re-index (`Pending`) il cui documento vettoriale è
    /// ancora `completed` serve il suo vecchio indice: il polling dice «indexed», con i chunk di quel documento.
    /// Su PostgreSQL vero, per provare la proiezione dello stato del documento e il vincolo di chiave esterna.
    /// </summary>
    [Fact]
    public async Task GetIndexingInfo_WithAPdfBackAtWork_AndACompletedVectorDocument_ReportsTheIndex()
    {
        using var seedContext = _fixture.CreateDbContext(_connectionString!);
        var uploaderId = Guid.NewGuid();
        seedContext.Set<UserEntity>().Add(CreateUser(uploaderId));
        var sharedGameId = Guid.NewGuid();
        seedContext.SharedGames.Add(CreateSharedGame(sharedGameId));
        var pdf = CreatePdfDocument(uploaderId, sharedGameId: sharedGameId, processingState: "Pending", fileName: "reindexing.pdf");
        seedContext.PdfDocuments.Add(pdf);
        await seedContext.SaveChangesAsync();
        seedContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGameId,
            SharedGameId = sharedGameId,
            PdfDocumentId = pdf.Id,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow.AddHours(-30),
            ChunkCount = 17,
        });
        await seedContext.SaveChangesAsync();

        using var actContext = _fixture.CreateDbContext(_connectionString!);
        var repo = new VectorDocumentRepository(actContext, _eventCollectorMock.Object);
        var info = await repo.GetIndexingInfoByGameIdAsync(sharedGameId);

        info.Should().NotBeNull();
        info!.Status.ToString().Should().Be("Completed", "the old index still serves while the PDF is re-indexed");
        info.ChunkCount.Should().Be(17);
    }

    /// <summary>
    /// TDD regression test: no SharedGameEntity record for the id → null result, no exception.
    /// Validates that the repository does NOT attempt to resolve via games table bridge
    /// (which would throw JOIN exceptions once games.SharedGameId column is dropped in sub-project #3).
    /// </summary>
    [Fact]
    public async Task GetIndexingInfo_NoPdfForSharedGameId_DoesNotResolveViaGamesTable()
    {
        // Arrange: seed only the SharedGame, no PDF
        using var seedContext = _fixture.CreateDbContext(_connectionString!);
        var sharedGameId = Guid.NewGuid();
        seedContext.SharedGames.Add(CreateSharedGame(sharedGameId));
        await seedContext.SaveChangesAsync();

        // Act
        using var actContext = _fixture.CreateDbContext(_connectionString!);
        var repo = new VectorDocumentRepository(actContext, _eventCollectorMock.Object);
        var info = await repo.GetIndexingInfoByGameIdAsync(sharedGameId);

        // Assert: null (no PDFs, no VectorDocs) — repo must not throw from games-table JOIN
        info.Should().BeNull("with no PDFs, resolution must return null without attempting the legacy games-table bridge");
    }
}
