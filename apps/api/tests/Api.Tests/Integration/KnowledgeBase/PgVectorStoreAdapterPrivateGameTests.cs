using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Issue #4244: gli embedding del PDF di un gioco PRIVATO ricevono come <c>game_id</c> l'id del
/// gioco privato.
/// </summary>
/// <remarks>
/// <see cref="PgVectorStoreAdapter.IndexBatchAsync"/> non riceve il gioco: lo legge dal documento
/// vettoriale. Per un gioco privato le due colonne che guardava (<c>shared_game_id</c> e
/// <c>GameId</c>) sono null, e la lettura di quel null faceva fallire l'indicizzazione prima ancora
/// di arrivare ai passaggi. <c>pgvector_embeddings.game_id</c> è un id unico senza chiave esterna,
/// condiviso o privato: è lì che la ricerca vettoriale filtra.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4244")]
public sealed class PgVectorStoreAdapterPrivateGameTests : IAsyncLifetime
{
    private const int Dims = 768; // must match vector(768) in the Initial migration

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;
    private IVectorStoreAdapter? _adapter;
    private Guid _userId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PgVectorStoreAdapterPrivateGameTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pgvector_private_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<IVectorStoreAdapter, PgVectorStoreAdapter>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(Ct);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, Ct);
            }
        }

        _adapter = _serviceProvider.GetRequiredService<IVectorStoreAdapter>();

        _userId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _userId,
            Email = $"pgvector-private-{_userId:N}@test.local",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(Ct);
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

    [Fact]
    public async Task IndexBatch_ForTheDocumentOfAPrivateGame_WritesThePrivateGameAsGameId()
    {
        var privateGameId = Guid.NewGuid();
        _dbContext!.PrivateGames.Add(new PrivateGameEntity
        {
            Id = privateGameId,
            OwnerId = _userId,
            Title = "Prototipo",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
        });
        var vectorDocumentId = await SeedVectorDocumentAsync(sharedGameId: null, privateGameId: privateGameId);

        await _adapter!.IndexBatchAsync([AnEmbeddingOf(vectorDocumentId)], Ct);

        (await GameIdsOfTheEmbeddingsAsync(vectorDocumentId)).Should().Equal(privateGameId);
        (await _adapter.SearchAsync(privateGameId, new Vector(MakeVector()), topK: 5, minScore: 0.0, cancellationToken: Ct))
            .Should().ContainSingle("la ricerca vettoriale sul gioco privato trova il suo passaggio");
    }

    /// <summary>Il controllo: per un gioco del catalogo non cambia nulla.</summary>
    [Fact]
    public async Task IndexBatch_ForTheDocumentOfASharedGame_WritesTheSharedGameAsGameId()
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext!.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Gioco del catalogo",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
        });
        var vectorDocumentId = await SeedVectorDocumentAsync(sharedGameId: sharedGameId, privateGameId: null);

        await _adapter!.IndexBatchAsync([AnEmbeddingOf(vectorDocumentId)], Ct);

        (await GameIdsOfTheEmbeddingsAsync(vectorDocumentId)).Should().Equal(sharedGameId);
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private static float[] MakeVector()
    {
        var v = new float[Dims];
        v[0] = 1f;
        return v;
    }

    private static Embedding AnEmbeddingOf(Guid vectorDocumentId) =>
        new(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDocumentId,
            textContent: "Each player draws five cards.",
            vector: new Vector(MakeVector()),
            model: "test-model",
            chunkIndex: 0,
            pageNumber: 1);

    /// <summary>
    /// Un PDF e il suo documento vettoriale, del gioco condiviso O di quello privato: come li
    /// lascia l'indicizzazione prima di scrivere gli embedding.
    /// </summary>
    private async Task<Guid> SeedVectorDocumentAsync(Guid? sharedGameId, Guid? privateGameId)
    {
        var pdfId = Guid.NewGuid();
        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = _userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Indexing",
            SharedGameId = sharedGameId,
            PrivateGameId = privateGameId,
        });

        var vectorDocumentId = Guid.NewGuid();
        _dbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = sharedGameId,
            SharedGameId = sharedGameId,
            PrivateGameId = privateGameId,
            PdfDocumentId = pdfId,
            ChunkCount = 1,
            TotalCharacters = 30,
            IndexingStatus = "processing",
            EmbeddingModel = "test-model",
            EmbeddingDimensions = Dims,
        });
        await _dbContext.SaveChangesAsync(Ct);
        return vectorDocumentId;
    }

    private async Task<List<Guid>> GameIdsOfTheEmbeddingsAsync(Guid vectorDocumentId)
    {
        var connection = _dbContext!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);

        await using var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = "SELECT game_id FROM pgvector_embeddings WHERE vector_document_id = @vdId";
        command.Parameters.AddWithValue("@vdId", vectorDocumentId);

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(Ct);
        while (await reader.ReadAsync(Ct))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }
}
