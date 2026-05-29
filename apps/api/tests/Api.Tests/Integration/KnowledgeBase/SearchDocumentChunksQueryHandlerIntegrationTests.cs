using Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchDocumentChunks;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for <see cref="SearchDocumentChunksByVectorQueryHandler"/>.
///
/// Happy-path: real pgvector Testcontainer with seeded embeddings and a mocked
/// <see cref="IEmbeddingService"/> that returns a known query vector.
/// "Not indexed" path: real Postgres DB, no pgvector rows — validates the resolver guard.
///
/// Issue #1653: F3-FU-4 — per-document scored similarity-search.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1653")]
public sealed class SearchDocumentChunksQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    // Mock embedding service — returns a unit vector aligned with our seeded embeddings.
    private readonly Mock<IEmbeddingService> _embeddingServiceMock = new();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SearchDocumentChunksQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_search_chunks_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // Register adapters needed by the handler.
        services.AddScoped<IVectorStoreAdapter, PgVectorStoreAdapter>();
        services.AddScoped<IEmbeddingRepository, EmbeddingRepository>();

        // Replace the stub IEmbeddingService with our configured mock.
        services.RemoveAll<IEmbeddingService>();
        services.AddScoped<IEmbeddingService>(_ => _embeddingServiceMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* best-effort */ }
        }
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a SharedGame + PdfDocument + VectorDocument in the relational DB,
    /// then inserts 2 embedding rows directly into <c>pgvector_embeddings</c> via
    /// <see cref="PgVectorStoreAdapter.IndexBatchAsync"/> so the happy-path test can
    /// assert score-ordered results.
    ///
    /// The <c>pgvector_embeddings</c> table is created by the EF Initial migration with a
    /// <c>vector(768)</c> column — we must use 768-dimensional test vectors.
    /// We do NOT call <see cref="IVectorStoreAdapter.EnsureCollectionExistsAsync"/> because
    /// the EF migration already created the table and that method would fail trying to add a
    /// GIN index on a <c>search_vector</c> column that only exists when the table is created
    /// from scratch by the adapter.
    /// </summary>
    private async Task<Guid> SeedIndexedDocAsync()
    {
        const int Dims = 768; // must match vector(768) column in Initial migration

        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"search-chunks-{userId:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });

        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Search Chunks Test Game",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(sharedGame);

        var pdfId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "search-chunks-test.pdf",
            FilePath = "/tmp/search-chunks-test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id
        });

        // VectorDocument: GameId is required by the pgvector search index.
        var vectorDocId = Guid.NewGuid();
        _dbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocId,
            GameId = sharedGame.Id,
            PdfDocumentId = pdfId,
            ChunkCount = 2,
            TotalCharacters = 100,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = Dims
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Build 768-dim vectors where only the first component differs.
        // emb1 query-aligned: [1, 0, 0, ... ] → cosine similarity with query ≈ 1.0
        // emb2 orthogonal:    [0, 1, 0, ... ] → cosine similarity with query ≈ 0.0
        var queryVector = MakeVector(Dims, firstComponent: 1f);
        var embVector1  = MakeVector(Dims, firstComponent: 1f);   // identical → sim ~1
        var embVector2  = MakeVector(Dims, secondComponent: 1f);  // orthogonal → sim ~0

        // Configure mock to return the query vector when asked.
        _embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { queryVector }));

        // Insert embeddings directly via raw SQL.
        // NOTE: Do NOT call EnsureCollectionExistsAsync (table already exists from EF migrations)
        // and do NOT use IndexBatchAsync (it does LEFT JOIN games which was dropped in migration 1345).
        await InsertEmbeddingRawAsync(vectorDocId, sharedGame.Id, 0, 1, "Predator activation rules explained in detail", embVector1);
        await InsertEmbeddingRawAsync(vectorDocId, sharedGame.Id, 1, 2, "Another chunk about different game mechanics", embVector2);

        return pdfId;
    }

    /// <summary>Inserts a single row into pgvector_embeddings via raw parameterised SQL.</summary>
    private async Task InsertEmbeddingRawAsync(
        Guid vectorDocumentId, Guid gameId,
        int chunkIndex, int pageNumber,
        string textContent, float[] vector)
    {
        var conn = _dbContext!.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(TestCancellationToken);

        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pgvector_embeddings
                (id, vector_document_id, game_id, text_content, vector, model,
                 chunk_index, page_number, created_at, lang, is_translation, role_tags)
            VALUES
                (@id, @vdId, @gameId, @text, @vec, 'test-model',
                 @chunkIndex, @pageNumber, NOW(), 'en', false, 0)
            """;

        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@vdId", vectorDocumentId);
        cmd.Parameters.AddWithValue("@gameId", gameId);
        cmd.Parameters.AddWithValue("@text", textContent);
        cmd.Parameters.AddWithValue("@vec", new Pgvector.Vector(vector));
        cmd.Parameters.AddWithValue("@chunkIndex", chunkIndex);
        cmd.Parameters.AddWithValue("@pageNumber", pageNumber);

        await cmd.ExecuteNonQueryAsync(TestCancellationToken);
    }

    /// <summary>Creates a unit vector of <paramref name="dims"/> dimensions with 1.0 at the specified position.</summary>
    private static float[] MakeVector(int dims, float firstComponent = 0f, float secondComponent = 0f)
    {
        var v = new float[dims];
        if (firstComponent != 0f) v[0] = firstComponent;
        if (secondComponent != 0f) v[1] = secondComponent;
        return v;
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Happy-path: a document with 2 indexed embeddings is searched;
    /// results come back ordered by score descending.
    /// Happy-path type: REAL integration (pgvector Testcontainer + mocked IEmbeddingService).
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task Handle_ReturnsScoredHits_OrderedByScoreDesc()
    {
        var pdfId = await SeedIndexedDocAsync();

        var result = await _mediator!.Send(
            new SearchDocumentChunksByVectorQuery(pdfId, "predator activation", TopK: 5, MinScore: 0.0),
            TestCancellationToken);

        result.ErrorMessage.Should().BeNull();
        result.Results.Should().NotBeEmpty();
        result.Results.Select(r => r.Score).Should().BeInDescendingOrder();
    }

    /// <summary>
    /// When PdfDocumentId does not correspond to any VectorDocument, the handler returns
    /// an empty result with a non-null ErrorMessage (not an exception).
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task Handle_ReturnsError_WhenDocNotIndexed()
    {
        var result = await _mediator!.Send(
            new SearchDocumentChunksByVectorQuery(Guid.NewGuid(), "x", TopK: 5, MinScore: 0.0),
            TestCancellationToken);

        result.Results.Should().BeEmpty();
        result.ErrorMessage.Should().NotBeNull();
    }
}
