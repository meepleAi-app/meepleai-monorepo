using System.Data;
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
/// #4662 — <see cref="PgVectorStoreAdapter"/> non lascia aperta la connessione del <c>DbContext</c> quando ha
/// finito.
/// </summary>
/// <remarks>
/// <para>
/// L'adattatore apriva a mano la connessione del contesto e non la richiudeva: la connessione restava
/// occupata fino alla distruzione del <c>DbContext</c>, cioè per tutta la richiesta. Una risposta aperta
/// (SSE o no) vive per tutta la generazione del modello, quindi teneva una connessione del pool per tutto quel
/// tempo, e con il pool piccolo due risposte aperte bastavano perché una terza richiesta non riuscisse
/// nemmeno ad autenticarsi.
/// </para>
/// <para>
/// Qui la prova è diretta: dopo ogni operazione pubblica dell'adattatore la connessione del contesto è chiusa,
/// cioè è tornata al pool. Dentro una transazione già aperta sul contesto invece resta aperta, perché la
/// transazione ne ha bisogno, e chiuderla la annullerebbe.
/// </para>
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4662")]
public sealed class PgVectorStoreAdapterConnectionTests : IAsyncLifetime
{
    private const int Dims = 768; // must match vector(768) in the Initial migration

    public enum Operation
    {
        Search,
        SearchWithScores,
        SearchByMultipleGameIds,
        IndexBatch,
        Replace,
        DeleteByVectorDocument,
        DeleteBySourceChunks,
        CollectionExists,
        EnsureCollectionExists,
    }

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;
    private IVectorStoreAdapter? _adapter;
    private Guid _userId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PgVectorStoreAdapterConnectionTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pgvector_conn_{Guid.NewGuid():N}";
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
            Email = $"pgvector-conn-{_userId:N}@test.local",
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

    /// <summary>
    /// Ogni operazione pubblica dell'adattatore, chiamata con la connessione del contesto chiusa, la lascia
    /// chiusa: non resta occupata fino alla fine della richiesta.
    /// </summary>
    [Theory]
    [InlineData(Operation.Search)]
    [InlineData(Operation.SearchWithScores)]
    [InlineData(Operation.SearchByMultipleGameIds)]
    [InlineData(Operation.IndexBatch)]
    [InlineData(Operation.Replace)]
    [InlineData(Operation.DeleteByVectorDocument)]
    [InlineData(Operation.DeleteBySourceChunks)]
    [InlineData(Operation.CollectionExists)]
    [InlineData(Operation.EnsureCollectionExists)]
    public async Task EveryOperation_LeavesTheConnectionOfTheContextClosed(Operation operation)
    {
        var (gameId, vectorDocumentId) = await SeedVectorDocumentAsync();
        var connection = _dbContext!.Database.GetDbConnection();
        connection.State.Should().Be(ConnectionState.Closed, "setup: nothing is holding the connection yet");
        _serviceProvider!.GetRequiredService<MeepleAiDbContext>().Should().BeSameAs(
            _dbContext, "setup: the adapter is built over the same context as this test reads, or the test proves nothing");

        await RunAsync(operation, gameId, vectorDocumentId);

        connection.State.Should().Be(
            ConnectionState.Closed,
            "the connection went back to the pool: an answer that stays open for the whole generation of the "
            + "model would otherwise keep one pooled connection for all that time");
    }

    /// <summary>
    /// Nessun metodo dell'interfaccia resta fuori dalla prova sopra: un decimo metodo, scritto con l'apertura a
    /// mano, passerebbe senza che il test se ne accorga. Se questo test cade, aggiungi il metodo a
    /// <see cref="Operation"/> e a <see cref="RunAsync"/>.
    /// </summary>
    [Fact]
    public void EveryMethodOfTheInterface_IsCoveredByAnOperation()
    {
        var covered = new[]
        {
            "SearchAsync", "SearchWithScoresAsync", "SearchByMultipleGameIdsAsync", "IndexBatchAsync",
            "ReplaceByVectorDocumentIdAsync", "DeleteByVectorDocumentIdAsync", "DeleteBySourceChunkIdsAsync",
            "CollectionExistsAsync", "EnsureCollectionExistsAsync",
        };

        typeof(IVectorStoreAdapter).GetMethods().Select(m => m.Name).Should().BeEquivalentTo(
            covered, "every method of the adapter has to give the connection back (see Operation)");
        Enum.GetValues<Operation>().Should().HaveCount(covered.Length);
    }

    /// <summary>
    /// Dentro una transazione già aperta sul contesto la connessione NON si chiude sotto di essa: la
    /// transazione ne ha bisogno, e chiuderla la annullerebbe. Il lavoro dell'adattatore ne segue l'esito.
    /// </summary>
    [Fact]
    public async Task InsideATransactionAlreadyOpen_TheConnectionStaysOpen_AndFollowsTheOutcome()
    {
        var (_, vectorDocumentId) = await SeedVectorDocumentAsync();
        var embedding = AnEmbeddingOf(vectorDocumentId, 0);
        var connection = _dbContext!.Database.GetDbConnection();

        var transaction = await _dbContext.Database.BeginTransactionAsync(Ct);
        await using (transaction.ConfigureAwait(false))
        {
            await _adapter!.IndexBatchAsync([embedding], Ct);

            connection.State.Should().Be(ConnectionState.Open, "the transaction still needs it");
            (await EmbeddingIdsOfAsync(vectorDocumentId)).Should().Equal(new[] { embedding.Id });

            await transaction.RollbackAsync(Ct);
        }

        (await EmbeddingIdsOfAsync(vectorDocumentId)).Should().BeEmpty("the outer transaction rolled back");
    }

    private async Task RunAsync(Operation operation, Guid gameId, Guid vectorDocumentId)
    {
        var vector = new Vector(MakeVector());
        switch (operation)
        {
            case Operation.Search:
                await _adapter!.SearchAsync(gameId, vector, topK: 5, minScore: 0.0, cancellationToken: Ct);
                break;
            case Operation.SearchWithScores:
                await _adapter!.SearchWithScoresAsync(gameId, vector, topK: 5, minScore: 0.0, cancellationToken: Ct);
                break;
            case Operation.SearchByMultipleGameIds:
                await _adapter!.SearchByMultipleGameIdsAsync([gameId], vector, topK: 5, minScore: 0.0, cancellationToken: Ct);
                break;
            case Operation.IndexBatch:
                await _adapter!.IndexBatchAsync([AnEmbeddingOf(vectorDocumentId, 0)], Ct);
                break;
            case Operation.Replace:
                await _adapter!.ReplaceByVectorDocumentIdAsync(vectorDocumentId, [AnEmbeddingOf(vectorDocumentId, 0)], Ct);
                break;
            case Operation.DeleteByVectorDocument:
                await _adapter!.DeleteByVectorDocumentIdAsync(vectorDocumentId, Ct);
                break;
            case Operation.DeleteBySourceChunks:
                await _adapter!.DeleteBySourceChunkIdsAsync([Guid.NewGuid()], Ct);
                break;
            case Operation.CollectionExists:
                await _adapter!.CollectionExistsAsync(gameId, Ct);
                break;
            case Operation.EnsureCollectionExists:
                await _adapter!.EnsureCollectionExistsAsync(gameId, Dims, Ct);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private static float[] MakeVector()
    {
        var v = new float[Dims];
        v[0] = 1f;
        return v;
    }

    private static Embedding AnEmbeddingOf(Guid vectorDocumentId, int chunkIndex) =>
        new(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDocumentId,
            textContent: $"Each player draws five cards ({chunkIndex}).",
            vector: new Vector(MakeVector()),
            model: "test-model",
            chunkIndex: chunkIndex,
            pageNumber: 1);

    /// <summary>Un gioco del catalogo, il suo PDF e il documento vettoriale, come li lascia l'indicizzazione.</summary>
    private async Task<(Guid GameId, Guid VectorDocumentId)> SeedVectorDocumentAsync()
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext!.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = $"Gioco {sharedGameId:N}",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 2,
            CreatedBy = _userId,
            CreatedAt = DateTime.UtcNow,
        });

        var pdfId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = _userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            SharedGameId = sharedGameId,
        });

        var vectorDocumentId = Guid.NewGuid();
        _dbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = sharedGameId,
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfId,
            ChunkCount = 1,
            TotalCharacters = 30,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = Dims,
        });
        await _dbContext.SaveChangesAsync(Ct);
        return (sharedGameId, vectorDocumentId);
    }

    /// <summary>Gli id degli embedding di un documento, letti con la connessione del contesto (riaperta e richiusa se serve).</summary>
    private async Task<List<Guid>> EmbeddingIdsOfAsync(Guid vectorDocumentId)
    {
        var connection = _dbContext!.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
            await connection.OpenAsync(Ct);

        try
        {
            await using var command = (NpgsqlCommand)connection.CreateCommand();
            command.CommandText = "SELECT id FROM pgvector_embeddings WHERE vector_document_id = @vdId ORDER BY chunk_index, id";
            command.Parameters.AddWithValue("@vdId", vectorDocumentId);

            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(Ct);
            while (await reader.ReadAsync(Ct))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }
}
