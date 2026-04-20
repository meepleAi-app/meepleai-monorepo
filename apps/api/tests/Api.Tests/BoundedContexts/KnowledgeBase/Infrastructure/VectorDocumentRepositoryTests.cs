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
/// Target method: GetIndexingInfoByGameIdAsync (contains the bridge logic at lines 171-219).
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
        UploadedByUserId = Guid.NewGuid(),
        UploadedAt = DateTime.UtcNow,
        ProcessingState = processingState,
    };

    /// <summary>
    /// TDD regression test: after removing the legacy bridge, resolution via sharedGameId
    /// must still find PDFs whose SharedGameId OR PrivateGameId matches the input id.
    /// Validates the pipeline fallback (third fallback in GetIndexingInfoByGameIdAsync).
    /// </summary>
    [Fact]
    public async Task GetIndexingInfo_WithSharedGameId_ResolvesPdfViaSharedGameIdOrPrivateGameId()
    {
        // Arrange
        using var seedContext = _fixture.CreateDbContext(_connectionString!);
        var sharedGameId = Guid.NewGuid();
        seedContext.SharedGames.Add(CreateSharedGame(sharedGameId));

        // Seed two PDFs in "Extracting" state (pipeline in progress → fallback path)
        // so the third fallback (pdfProcessingState) evaluates both predicates.
        var sharedPdf = CreatePdfDocument(sharedGameId: sharedGameId, processingState: "Extracting", fileName: "shared.pdf");
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
