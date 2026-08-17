using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Regression for the <see cref="IndexPdfCommandHandler"/> failed-reindex NoTracking silent no-op.
///
/// On the re-index path, <c>ValidateAndPreparePdfForIndexingAsync</c> loads the pre-existing
/// <see cref="VectorDocumentEntity"/> via a bare <c>_db.Set&lt;VectorDocumentEntity&gt;().FirstOrDefaultAsync(...)</c>
/// (no <c>.AsTracking()</c>). The DbContext defaults to <c>QueryTrackingBehavior.NoTracking</c>
/// (PERF-06), so the entity is detached — the "reset to processing" write AND the terminal
/// <c>MarkIndexingFailedAsync</c> write ("failed") are both silent no-ops. Net effect: when a
/// re-index fails, the VectorDocument stays <c>completed</c> with stale embeddings while the PDF is
/// Failed — divergent tables.
///
/// <para>The existing <c>IndexPdfIntegrationTests</c> misses this because it builds its DbContext via
/// <c>IntegrationServiceCollectionBuilder.CreateBase(conn)</c> WITHOUT <c>useNoTrackingDefault: true</c>,
/// leaving EF Core's track-by-default behavior in place (which masks the bug). This suite explicitly
/// opts into the production NoTracking default.</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class IndexPdfVectorDocNoTrackingPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public IndexPdfVectorDocNoTrackingPersistenceIntegrationTests(SharedTestcontainersFixture fixture)
        => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"indexpdf_vecdoc_notracking_{Guid.NewGuid():N}";
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // CRUX: useNoTrackingDefault: true reproduces the production QueryTrackingBehavior.NoTracking
        // default. Without it the test DbContext would track-by-default and mask the bug.
        var services = IntegrationServiceCollectionBuilder.CreateBase(conn, useNoTrackingDefault: true);

        // Real chunking stack so ChunkAndEmbedTextAsync produces genuine chunks before the
        // (deliberately failing) embedding step drives execution to MarkIndexingFailedAsync.
        services.AddSingleton<ITextChunkingService, TextChunkingService>();
        services.AddScoped<ChunkingStrategySelector>();
        services.AddScoped<IAdvancedChunkingService, AdvancedChunkingService>();

        _serviceProvider = services.BuildServiceProvider();

        var db = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* ignore cleanup errors */ }
        }
    }

    private static IOptions<IndexingSettings> IndexingSettings() =>
        Options.Create(new IndexingSettings { EmbeddingBatchSize = 100 });

    [Fact(DisplayName = "IndexPdfCommandHandler marks the existing VectorDocument failed under NoTracking when reindex embedding fails")]
    public async Task Handle_FailedReindexUnderNoTracking_MarksExistingVectorDocumentFailed()
    {
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Arrange — seed user, game, a Ready PDF with extracted text, and an EXISTING VectorDocument
        // in the "completed" state (the re-index scenario). Seed via its own scope so the entities
        // are not left tracked in the context the handler will later query (matches production
        // isolation and avoids EF "already tracked" harness artifacts).
        await using (var seedScope = _serviceProvider!.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            seedDb.Users.Add(new UserEntity
            {
                Id = userId,
                Email = $"indexpdf-notrk-{userId:N}@meepleai.test",
                DisplayName = "IndexPdf NoTracking Test",
                Role = "Editor",
                CreatedAt = DateTime.UtcNow,
            });
            seedDb.SharedGames.Add(new SharedGameEntity
            {
                Id = gameId,
                Title = "IndexPdf NoTracking Test Game",
                CreatedAt = DateTime.UtcNow,
            });
            seedDb.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = pdfId,
                SharedGameId = gameId,
                UploadedByUserId = userId,
                FileName = "reindex.pdf",
                FilePath = $"/test/{pdfId:N}.pdf",
                FileSizeBytes = 1024,
                ContentType = "application/pdf",
                UploadedAt = DateTime.UtcNow,
                PageCount = 3,
                ProcessingState = "Ready",
                ExtractedText = "Setup the board.\n\nDeal cards to each player.\n\nScore points at the end.",
            });
            seedDb.Set<VectorDocumentEntity>().Add(new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                GameId = gameId,
                SharedGameId = gameId,
                ChunkCount = 5,
                TotalCharacters = 100,
                IndexedAt = DateTime.UtcNow,
                IndexingStatus = "completed",
                IndexingError = null,
                EmbeddingModel = "test-embedding-model",
                EmbeddingDimensions = 768,
            });
            await seedDb.SaveChangesAsync(TestCancellationToken);
        }

        // Act — run the handler with a FAILING embedding service so execution reaches
        // MarkIndexingFailedAsync on the existing VectorDocument. Handler built against a fresh
        // NoTracking-default context resolved from the act scope.
        var embeddingMock = new Mock<IEmbeddingService>();
        embeddingMock.Setup(e => e.GetEmbeddingDimensions()).Returns(768);
        embeddingMock.Setup(e => e.GetModelName()).Returns("test-embedding-model");
        embeddingMock
            .Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = false,
                Embeddings = new List<float[]>(),
                ErrorMessage = "simulated embedding failure",
            });

        IndexingResultDto result;
        using (var actScope = _serviceProvider!.CreateScope())
        {
            var actDb = actScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var handler = new IndexPdfCommandHandler(
                actDb,
                actScope.ServiceProvider.GetRequiredService<IAdvancedChunkingService>(),
                embeddingMock.Object,
                NullLogger<IndexPdfCommandHandler>.Instance,
                IndexingSettings(),
                Mock.Of<ISemanticResponseCache>(),
                Mock.Of<IPdfIndexingPipeline>());

            result = await handler.Handle(new IndexPdfCommand(pdfId.ToString()), TestCancellationToken);
        }

        // Sanity: the handler reported the embedding failure.
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.EmbeddingFailed);

        // Assert — fresh NoTracking read: the existing VectorDocument must have flipped to "failed".
        using (var verifyScope = _serviceProvider!.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var vectorDoc = await verifyDb.Set<VectorDocumentEntity>()
                .AsNoTracking()
                .FirstAsync(v => v.PdfDocumentId == pdfId, TestCancellationToken);

            vectorDoc.IndexingStatus.Should().Be(
                "failed",
                "IndexPdfCommandHandler must load the existing VectorDocument .AsTracking() so a failed "
                + "re-index actually marks it failed — under the NoTracking default the mutation is a silent "
                + "no-op, leaving the VectorDocument 'completed' with stale embeddings while the PDF failed.");
            vectorDoc.IndexingError.Should().NotBeNullOrEmpty(
                "the embedding failure message must be recorded on the VectorDocument");
        }
    }
}
