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
/// #4660 punto 2 — <see cref="PgVectorStoreAdapter.ReplaceByVectorDocumentIdAsync"/> sostituisce gli
/// embedding di un documento vettoriale in modo atomico.
/// </summary>
/// <remarks>
/// <para>
/// Prima il re-index cancellava gli embedding con un <c>DELETE</c> in autocommit e poi li reinseriva con un
/// <c>COPY</c>. Se il <c>COPY</c> falliva, il documento vettoriale — che un re-index della pipeline a coda lascia
/// <c>completed</c>, per decisione del 2026-10-25 — restava <c>completed</c> senza più un embedding. Ora le due
/// operazioni stanno in una transazione: un inserimento fallito lascia gli embedding vecchi. È atomico sulla
/// sola tabella degli embedding: i passaggi di testo che la pipeline ha salvato prima non ne fanno parte.
/// </para>
/// <para>
/// L'inserimento fallisce qui con una chiave duplicata nello stesso lotto: l'errore arriva dal server a fine
/// <c>COPY</c>, cioè dopo che il <c>DELETE</c> è già stato eseguito.
/// </para>
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4660")]
public sealed class PgVectorStoreAdapterReplaceTests : IAsyncLifetime
{
    private const int Dims = 768; // must match vector(768) in the Initial migration

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;
    private IVectorStoreAdapter? _adapter;
    private Guid _userId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PgVectorStoreAdapterReplaceTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pgvector_replace_{Guid.NewGuid():N}";
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
            Email = $"pgvector-replace-{_userId:N}@test.local",
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
    public async Task Replace_LeavesOnlyTheNewEmbeddingsOfThatDocument()
    {
        var vectorDocumentId = await SeedVectorDocumentAsync();
        var otherDocumentId = await SeedVectorDocumentAsync();
        var oldOnes = new[] { AnEmbeddingOf(vectorDocumentId, 0), AnEmbeddingOf(vectorDocumentId, 1) };
        var untouched = AnEmbeddingOf(otherDocumentId, 0);
        await _adapter!.IndexBatchAsync([.. oldOnes, untouched], Ct);

        var newOnes = new[] { AnEmbeddingOf(vectorDocumentId, 0), AnEmbeddingOf(vectorDocumentId, 1), AnEmbeddingOf(vectorDocumentId, 2) };
        await _adapter.ReplaceByVectorDocumentIdAsync(vectorDocumentId, [.. newOnes], Ct);

        (await EmbeddingIdsOfAsync(vectorDocumentId)).Should().BeEquivalentTo(newOnes.Select(e => e.Id));
        (await EmbeddingIdsOfAsync(otherDocumentId)).Should().Equal(
            new[] { untouched.Id }, "another document's embeddings are not part of the replacement");
    }

    /// <summary>
    /// Il punto della correzione: l'inserimento fallisce DOPO che il DELETE è stato eseguito. Con due
    /// operazioni separate gli embedding vecchi erano già persi; in una transazione tornano.
    /// </summary>
    [Fact]
    public async Task Replace_WhenTheInsertFails_KeepsTheOldEmbeddings()
    {
        var vectorDocumentId = await SeedVectorDocumentAsync();
        var oldOnes = new[] { AnEmbeddingOf(vectorDocumentId, 0), AnEmbeddingOf(vectorDocumentId, 1) };
        await _adapter!.IndexBatchAsync([.. oldOnes], Ct);

        var duplicate = AnEmbeddingOf(vectorDocumentId, 0);
        var act = () => _adapter.ReplaceByVectorDocumentIdAsync(vectorDocumentId, [duplicate, duplicate], Ct);

        (await act.Should().ThrowAsync<PostgresException>("two rows with the same id make the COPY fail at the end"))
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        (await EmbeddingIdsOfAsync(vectorDocumentId)).Should().BeEquivalentTo(
            oldOnes.Select(e => e.Id),
            "the DELETE ran in the same transaction as the failed COPY: it rolls back with it");
    }

    /// <summary>
    /// Una transazione già aperta sul contesto (di uno unit of work) non si annida: il DELETE e il COPY ne
    /// fanno parte e seguono il suo esito.
    /// </summary>
    [Fact]
    public async Task Replace_InsideATransactionAlreadyOpenOnTheContext_JoinsItAndFollowsItsOutcome()
    {
        var vectorDocumentId = await SeedVectorDocumentAsync();
        var oldOne = AnEmbeddingOf(vectorDocumentId, 0);
        await _adapter!.IndexBatchAsync([oldOne], Ct);

        var transaction = await _dbContext!.Database.BeginTransactionAsync(Ct);
        await using (transaction.ConfigureAwait(false))
        {
            var newOne = AnEmbeddingOf(vectorDocumentId, 0);
            await _adapter.ReplaceByVectorDocumentIdAsync(vectorDocumentId, [newOne], Ct);
            (await EmbeddingIdsOfAsync(vectorDocumentId)).Should().Equal(
                new[] { newOne.Id }, "inside the transaction the replacement is visible");

            await transaction.RollbackAsync(Ct);
        }

        (await EmbeddingIdsOfAsync(vectorDocumentId)).Should().Equal(
            new[] { oldOne.Id }, "the outer transaction rolled back, and the replacement with it");
    }

    /// <summary>
    /// L'id da cancellare e quello sulle righe nuove vengono da due posti: se non coincidono, la sostituzione
    /// cancellerebbe l'indice di un documento e aggiungerebbe righe a un altro. Lancia prima di toccare niente.
    /// </summary>
    [Fact]
    public async Task Replace_WithAnEmbeddingOfAnotherDocument_ThrowsAndDeletesNothing()
    {
        var vectorDocumentId = await SeedVectorDocumentAsync();
        var otherDocumentId = await SeedVectorDocumentAsync();
        var oldOne = AnEmbeddingOf(vectorDocumentId, 0);
        await _adapter!.IndexBatchAsync([oldOne], Ct);

        var act = () => _adapter.ReplaceByVectorDocumentIdAsync(
            vectorDocumentId, [AnEmbeddingOf(vectorDocumentId, 0), AnEmbeddingOf(otherDocumentId, 1)], Ct);

        await act.Should().ThrowAsync<ArgumentException>();
        (await EmbeddingIdsOfAsync(vectorDocumentId)).Should().Equal(new[] { oldOne.Id });
        (await EmbeddingIdsOfAsync(otherDocumentId)).Should().BeEmpty();
    }

    /// <summary>
    /// Un lotto vuoto non cancella niente: non c'è nulla con cui sostituire, e un indice cancellato per un
    /// errore a monte è peggio di un indice vecchio.
    /// </summary>
    [Fact]
    public async Task Replace_WithAnEmptyBatch_DoesNotDeleteAnything()
    {
        var vectorDocumentId = await SeedVectorDocumentAsync();
        var oldOne = AnEmbeddingOf(vectorDocumentId, 0);
        await _adapter!.IndexBatchAsync([oldOne], Ct);

        await _adapter.ReplaceByVectorDocumentIdAsync(vectorDocumentId, [], Ct);

        (await EmbeddingIdsOfAsync(vectorDocumentId)).Should().Equal(oldOne.Id);
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

    /// <summary>Un PDF di un gioco del catalogo e il suo documento vettoriale, come li lascia l'indicizzazione prima degli embedding.</summary>
    private async Task<Guid> SeedVectorDocumentAsync()
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
            ChunkCount = 2,
            TotalCharacters = 60,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = Dims,
        });
        await _dbContext.SaveChangesAsync(Ct);
        return vectorDocumentId;
    }

    private async Task<List<Guid>> EmbeddingIdsOfAsync(Guid vectorDocumentId)
    {
        var connection = _dbContext!.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);

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
}
