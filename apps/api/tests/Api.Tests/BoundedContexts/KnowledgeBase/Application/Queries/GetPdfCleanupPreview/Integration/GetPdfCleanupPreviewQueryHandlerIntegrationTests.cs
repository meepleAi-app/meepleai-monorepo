using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetPdfCleanupPreview;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.GetPdfCleanupPreview.Integration;

/// <summary>
/// Integration tests for <see cref="GetPdfCleanupPreviewQueryHandler"/> against a real
/// PostgreSQL database via Testcontainers. Validates that the EF Core SQL translation
/// of the COUNT(*) clauses on <c>text_chunks</c> and <c>raptor_summaries</c> filtered
/// by <c>PdfDocumentId</c> produces the expected aggregates end-to-end. Issue #1529.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1529")]
public sealed class GetPdfCleanupPreviewQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _dbName = null!;
    private MeepleAiDbContext _dbContext = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public GetPdfCleanupPreviewQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_pdf_cleanup_preview_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);

        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_dbName);
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();
    }

    [Fact(Timeout = 60_000)]
    public async Task Handle_RealPostgres_CountsChunksAndRaptorScopedPerPdf()
    {
        // Arrange ----------------------------------------------------------------
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var targetPdfId = Guid.NewGuid();
        var unrelatedPdfId = Guid.NewGuid();
        const long fileSize = 47_185_920L;

        _dbContext.Users.Add(CreateUser(userId));

        // #3633: le RaptorSummaries seedate più sotto hanno una FK verso shared_games
        // (FK_RaptorSummaries_shared_games_GameId), ma il gioco non veniva mai creato: il
        // SaveChanges falliva con 23503 prima ancora di arrivare alle assert. Va inserito qui,
        // prima delle righe che lo referenziano.
        _dbContext.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = gameId,
            Title = "Cleanup Preview Test Game",
        });

        _dbContext.PdfDocuments.AddRange(
            CreatePdfDocument(targetPdfId, userId, fileSize),
            CreatePdfDocument(unrelatedPdfId, userId, 999L));

        // 5 chunks for the target PDF, 3 for an unrelated PDF
        for (int i = 0; i < 5; i++)
        {
            _dbContext.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = targetPdfId,
                Content = $"target-chunk-{i}",
                ChunkIndex = i,
                CharacterCount = 100
            });
        }
        for (int i = 0; i < 3; i++)
        {
            _dbContext.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = unrelatedPdfId,
                Content = $"unrelated-chunk-{i}",
                ChunkIndex = i,
                CharacterCount = 100
            });
        }

        // 2 RAPTOR summaries for the target PDF, 4 for the unrelated PDF
        for (int i = 0; i < 2; i++)
        {
            _dbContext.RaptorSummaries.Add(new RaptorSummaryEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = targetPdfId,
                GameId = gameId,
                TreeLevel = i,
                ClusterIndex = i,
                SummaryText = $"target-r-{i}",
                SourceChunkCount = 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        for (int i = 0; i < 4; i++)
        {
            _dbContext.RaptorSummaries.Add(new RaptorSummaryEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = unrelatedPdfId,
                GameId = gameId,
                TreeLevel = 0,
                ClusterIndex = i,
                SummaryText = $"unrelated-r-{i}",
                SourceChunkCount = 1,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(Token);

        var sut = new GetPdfCleanupPreviewQueryHandler(_dbContext);

        // Act --------------------------------------------------------------------
        var result = await sut.Handle(new GetPdfCleanupPreviewQuery(targetPdfId), Token);

        // Assert -----------------------------------------------------------------
        result.Should().NotBeNull();
        result!.PdfId.Should().Be(targetPdfId);
        result.PdfFileSizeBytes.Should().Be(fileSize);
        result.ChunkCount.Should().Be(5, "EF Core must translate Where + Count to a scoped SQL COUNT(*) and return only target-PDF chunks");
        result.RaptorSummaryCount.Should().Be(2, "same scoping rule for raptor_summaries");
        result.GraphEdgeCount.Should().Be(0, "constant 0 — no graph store yet");
    }

    [Fact(Timeout = 30_000)]
    public async Task Handle_RealPostgres_UnknownPdfId_ReturnsNull()
    {
        // Arrange — empty DB; ask for a random PDF id
        var sut = new GetPdfCleanupPreviewQueryHandler(_dbContext);

        // Act
        var result = await sut.Handle(new GetPdfCleanupPreviewQuery(Guid.NewGuid()), Token);

        // Assert
        result.Should().BeNull("missing PDF must map to null so the endpoint can return 404 without leaking existence");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static UserEntity CreateUser(Guid userId) => new()
    {
        Id = userId,
        Email = $"pdf-cleanup-{userId:N}@test.local",
        DisplayName = "Pdf Cleanup Preview Test User",
        PasswordHash = "hashed",
        Role = "User",
        Tier = "free",
        CreatedAt = DateTime.UtcNow,
        IsTwoFactorEnabled = false,
    };

    private static PdfDocumentEntity CreatePdfDocument(Guid pdfId, Guid userId, long fileSize) => new()
    {
        Id = pdfId,
        FileName = $"rulebook-{pdfId:N}.pdf",
        FilePath = $"/test/{pdfId:N}.pdf",
        FileSizeBytes = fileSize,
        UploadedByUserId = userId,
        UploadedAt = DateTime.UtcNow,
        ProcessingState = "Ready",
    };
}
