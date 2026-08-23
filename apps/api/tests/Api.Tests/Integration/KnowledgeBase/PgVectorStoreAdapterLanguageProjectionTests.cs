using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for the <c>lang</c> column projection in <see cref="PgVectorStoreAdapter"/>.
///
/// <para>Issue #3740: the three read paths (<c>SearchAsync</c>, <c>SearchWithScoresAsync</c>,
/// <c>SearchByMultipleGameIdsAsync</c>) did not SELECT <c>lang</c>, while
/// <see cref="Embedding.Language"/> carries the initializer <c>"en"</c>. Every candidate therefore
/// reached the caller marked <c>"en"</c> whatever the real language of the chunk was — a missing
/// column masked by a non-null default. That is why the per-language correction of PR #3743 came
/// out a byte-identical no-op and the gate corpus looked monolingual.</para>
///
/// <para>These tests seed rows with three distinct <c>lang</c> values and assert the value read back
/// per chunk, keyed by text so ordering cannot make them pass by luck.</para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3740")]
public sealed class PgVectorStoreAdapterLanguageProjectionTests : IAsyncLifetime
{
    private const int Dims = 768; // must match vector(768) in the Initial migration

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;
    private IVectorStoreAdapter? _adapter;
    private Guid _gameId;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PgVectorStoreAdapterLanguageProjectionTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pgvector_lang_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<IVectorStoreAdapter, PgVectorStoreAdapter>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

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

        _adapter = _serviceProvider.GetRequiredService<IVectorStoreAdapter>();
        _gameId = await SeedTrilingualCorpusAsync();
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

    // --- Tests ----------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_ProjectsTheStoredLanguage_PerChunk()
    {
        var results = await _adapter!.SearchAsync(
            _gameId, QueryVector(), topK: 10, minScore: 0.0, documentIds: null,
            cancellationToken: TestCancellationToken);

        LanguageByText(results).Should().Equal(ExpectedLanguages);
    }

    [Fact]
    public async Task SearchWithScoresAsync_ProjectsTheStoredLanguage_PerChunk()
    {
        var results = await _adapter!.SearchWithScoresAsync(
            _gameId, QueryVector(), topK: 10, minScore: 0.0, documentIds: null,
            cancellationToken: TestCancellationToken);

        LanguageByText(results.Select(r => r.Embedding)).Should().Equal(ExpectedLanguages);
    }

    [Fact]
    public async Task SearchByMultipleGameIdsAsync_ProjectsTheStoredLanguage_PerChunk()
    {
        var results = await _adapter!.SearchByMultipleGameIdsAsync(
            new[] { _gameId }, QueryVector(), topK: 10, minScore: 0.0, documentIds: null,
            cancellationToken: TestCancellationToken);

        LanguageByText(results).Should().Equal(ExpectedLanguages);
    }

    // --- Helpers --------------------------------------------------------------

    /// <summary>The three seeded chunks, keyed by text so assertions never depend on result order.</summary>
    private static SortedDictionary<string, string> ExpectedLanguages => new()
    {
        ["Setup the board and place two settlements"] = "en",
        ["Prepara il tabellone e piazza due insediamenti"] = "it",
        ["Baue das Spielbrett auf und setze zwei Siedlungen"] = "de"
    };

    private static SortedDictionary<string, string> LanguageByText(IEnumerable<Embedding> embeddings)
    {
        var byText = new SortedDictionary<string, string>();
        foreach (var e in embeddings)
        {
            byText[e.TextContent] = e.Language;
        }

        return byText;
    }

    private static Vector QueryVector() => new(MakeVector());

    /// <summary>
    /// One vector shared by query and all three chunks: every row scores ~1.0, so all three come
    /// back and the test measures the projection, not the ranking.
    /// </summary>
    private static float[] MakeVector()
    {
        var v = new float[Dims];
        v[0] = 1f;
        return v;
    }

    private async Task<Guid> SeedTrilingualCorpusAsync()
    {
        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"pgvector-lang-{userId:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });

        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Language Projection Test Game",
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
            FileName = "pgvector-lang-test.pdf",
            FilePath = "/tmp/pgvector-lang-test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id
        });

        var vectorDocId = Guid.NewGuid();
        _dbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocId,
            GameId = sharedGame.Id,
            PdfDocumentId = pdfId,
            ChunkCount = ExpectedLanguages.Count,
            TotalCharacters = 150,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = Dims
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var chunkIndex = 0;
        foreach (var (text, lang) in ExpectedLanguages)
        {
            await InsertEmbeddingRawAsync(vectorDocId, sharedGame.Id, chunkIndex++, text, lang);
        }

        return sharedGame.Id;
    }

    private async Task InsertEmbeddingRawAsync(
        Guid vectorDocumentId, Guid gameId, int chunkIndex, string textContent, string lang)
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
                 @chunkIndex, 1, NOW(), @lang, false, 0)
            """;

        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@vdId", vectorDocumentId);
        cmd.Parameters.AddWithValue("@gameId", gameId);
        cmd.Parameters.AddWithValue("@text", textContent);
        cmd.Parameters.AddWithValue("@vec", new Pgvector.Vector(MakeVector()));
        cmd.Parameters.AddWithValue("@chunkIndex", chunkIndex);
        cmd.Parameters.AddWithValue("@lang", lang);

        await cmd.ExecuteNonQueryAsync(TestCancellationToken);
    }
}
