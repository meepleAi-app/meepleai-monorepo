using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// #4955: un chunk che ha figli non è un candidato del recupero, in nessun percorso. È il genitore di
/// <c>AdvancedChunkingService</c>, che concatena i suoi figli; quando l'estrazione non ha titoli la
/// sezione è una sola e il genitore è il documento intero. Vinceva per lunghezza in tutti e tre i
/// percorsi: nel ramo keyword (<c>ts_rank_cd</c> diviso per il logaritmo della lunghezza; sul gate il
/// chunk 0 di Maracaibo valeva 1,24 per una domanda su Ark Nova, con le sole «e», «di», «come»), nella
/// ricerca full-text del prompt (<c>plainto_tsquery</c> chiede tutte le parole, che il documento intero
/// contiene quasi sempre) e nel braccio vettoriale, con il testo di un manuale intero. Serve Postgres
/// vero: il filtro sta nella SQL.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4955")]
public sealed class ParentChunkRetrievalIntegrationTests : IAsyncLifetime
{
    private const int Dims = 768; // vector(768) nella migrazione iniziale
    private const string EnglishQuestion = "How do conservation projects and reputation work?";
    private const string ItalianQuestion = "Come funzionano i progetti di conservazione e la reputazione?";

    private static readonly string[] EnglishChildren =
    [
        "Conservation projects score points on the conservation track.",
        "Reputation lets you take cards from the display."
    ];

    private static readonly string[] ItalianChildren =
    [
        "I progetti di conservazione danno punti sul tracciato della conservazione.",
        "La reputazione permette di prendere carte dall'esposizione."
    ];

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ParentChunkRetrievalIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_parent_chunk_{Guid.NewGuid():N}";
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
            catch { /* best-effort cleanup */ }
        }
    }

    // --- Ramo keyword: KeywordSearchService --------------------------------------------------

    /// <summary>
    /// Il genitore contiene ogni parola dei figli, quindi corrisponde alla domanda quanto loro e senza
    /// filtro sarebbe fra i risultati. Il percorso inglese legge la colonna generata
    /// <c>search_vector</c>, quello italiano <c>to_tsvector</c> al momento della query: il filtro vale
    /// per entrambi.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    public async Task KeywordSearch_AChunkWithChildren_IsNotReturned_ItsChildrenAre(string language)
    {
        var children = language == "it" ? ItalianChildren : EnglishChildren;
        var corpus = await SeedParentWithChildrenAsync(language, children);

        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);
        var question = language == "it" ? ItalianQuestion : EnglishQuestion;
        var results = await service.SearchAsync(question, corpus.GameId, language: language, cancellationToken: Ct);

        var returned = results.Select(r => Guid.Parse(r.ChunkId)).ToList();
        returned.Should().NotContain(corpus.ParentId,
            "the parent only repeats its children's text, and its length decides its rank, not its relevance");
        returned.Should().BeEquivalentTo(corpus.ChildIds,
            "each child carries a word of the question, and the filter must not take them along with the parent");
    }

    /// <summary>
    /// Un chunk radice senza figli è l'unico che porta il suo testo: il paragrafo corto di
    /// <c>ProcessPlainText</c>, o un documento indicizzato senza gerarchia. Un filtro su
    /// <c>Level</c> o su <c>ParentChunkId</c> lo perderebbe.
    /// </summary>
    [Fact]
    public async Task KeywordSearch_ARootChunkWithoutChildren_IsStillReturned()
    {
        var (gameId, pdfId) = await SeedGameAsync("en");
        var rootId = await AddChunkAsync(gameId, pdfId, chunkIndex: 0, level: 0, parentId: null, EnglishChildren[0]);

        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);
        var results = await service.SearchAsync(EnglishQuestion, gameId, language: "en", cancellationToken: Ct);

        results.Select(r => Guid.Parse(r.ChunkId)).Should().ContainSingle()
            .Which.Should().Be(rootId, "nothing else carries this text, so excluding it would lose it");
    }

    // --- Ricerca full-text del prompt: TextChunkSearchService ---------------------------------

    /// <summary>
    /// <c>plainto_tsquery</c> chiede TUTTE le parole della domanda: il documento intero le contiene
    /// quasi sempre, un chunk vero raramente, e <c>ts_rank</c> non divide per la lunghezza. Misurato su
    /// Ark Nova: il documento intero era primo con 0,9999 contro 0,91 del miglior chunk.
    /// </summary>
    [Fact]
    public async Task FullTextSearch_AChunkWithChildren_IsNotReturned_ItsChildrenAre()
    {
        var corpus = await SeedParentWithChildrenAsync("en", EnglishChildren);
        var service = new TextChunkSearchService(_dbContext!, NullLogger<TextChunkSearchService>.Instance);

        var results = await service.FullTextSearchAsync(corpus.GameId, "conservation projects", limit: 10, Ct);

        results.Select(r => r.ChunkIndex).Should().Equal(new[] { 1 },
            "only the first child holds both words; the parent holds them too, but it is not a candidate");
    }

    [Fact]
    public async Task FullTextSearch_ARootChunkWithoutChildren_IsStillReturned()
    {
        var (gameId, pdfId) = await SeedGameAsync("en");
        await AddChunkAsync(gameId, pdfId, chunkIndex: 0, level: 0, parentId: null, EnglishChildren[0]);
        var service = new TextChunkSearchService(_dbContext!, NullLogger<TextChunkSearchService>.Instance);

        var results = await service.FullTextSearchAsync(gameId, "conservation projects", limit: 10, Ct);

        results.Select(r => r.ChunkIndex).Should().Equal(new[] { 0 },
            "nothing else carries this text, so excluding it would lose it");
    }

    // --- Braccio vettoriale: PgVectorStoreAdapter ---------------------------------------------

    /// <summary>
    /// I tre metodi di lettura dell'adattatore. Ogni riga ha lo stesso vettore della domanda, quindi
    /// tutte superano la soglia: il test misura il filtro, non l'ordinamento. La riga senza
    /// <c>source_chunk_id</c> (una foto, un embedding scritto senza il suo chunk) non ha figli e resta.
    /// </summary>
    [Theory]
    [InlineData(nameof(IVectorStoreAdapter.SearchAsync))]
    [InlineData(nameof(IVectorStoreAdapter.SearchWithScoresAsync))]
    [InlineData(nameof(IVectorStoreAdapter.SearchByMultipleGameIdsAsync))]
    public async Task VectorSearch_AChunkWithChildren_IsNotReturned(string method)
    {
        var corpus = await SeedParentWithChildrenAsync("en", EnglishChildren);
        var vectorDocumentId = await AddVectorDocumentAsync(corpus.GameId, corpus.PdfId);
        var parentText = string.Join(' ', EnglishChildren);
        await AddEmbeddingAsync(vectorDocumentId, corpus.GameId, 0, parentText, corpus.ParentId);
        await AddEmbeddingAsync(vectorDocumentId, corpus.GameId, 1, EnglishChildren[0], corpus.ChildIds[0]);
        await AddEmbeddingAsync(vectorDocumentId, corpus.GameId, 2, EnglishChildren[1], corpus.ChildIds[1]);
        await AddEmbeddingAsync(vectorDocumentId, corpus.GameId, 3, "A photo of the box", sourceChunkId: null);

        var adapter = _serviceProvider!.GetRequiredService<IVectorStoreAdapter>();
        var query = new Vector(MakeVector());
        var texts = method switch
        {
            nameof(IVectorStoreAdapter.SearchAsync) =>
                (await adapter.SearchAsync(corpus.GameId, query, topK: 10, minScore: 0.0, cancellationToken: Ct))
                    .Select(e => e.TextContent),
            nameof(IVectorStoreAdapter.SearchWithScoresAsync) =>
                (await adapter.SearchWithScoresAsync(corpus.GameId, query, topK: 10, minScore: 0.0, cancellationToken: Ct))
                    .Select(s => s.Embedding.TextContent),
            _ =>
                (await adapter.SearchByMultipleGameIdsAsync(new[] { corpus.GameId }, query, topK: 10, minScore: 0.0, cancellationToken: Ct))
                    .Select(e => e.TextContent),
        };

        texts.Should().BeEquivalentTo(new[] { EnglishChildren[0], EnglishChildren[1], "A photo of the box" },
            "the parent's text is all in its children; a row with no source chunk has no children and stays");
    }

    // --- Seme ----------------------------------------------------------------------------------

    private sealed record ParentWithChildren(Guid GameId, Guid PdfId, Guid ParentId, IReadOnlyList<Guid> ChildIds);

    private async Task<ParentWithChildren> SeedParentWithChildrenAsync(string language, string[] children)
    {
        var (gameId, pdfId) = await SeedGameAsync(language);
        var parentId = await AddChunkAsync(gameId, pdfId, chunkIndex: 0, level: 0, parentId: null, string.Join(' ', children));
        var childIds = new List<Guid>();
        for (var i = 0; i < children.Length; i++)
            childIds.Add(await AddChunkAsync(gameId, pdfId, chunkIndex: i + 1, level: 2, parentId, children[i]));

        return new ParentWithChildren(gameId, pdfId, parentId, childIds);
    }

    private async Task<(Guid GameId, Guid PdfId)> SeedGameAsync(string pdfLanguage)
    {
        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"parent-chunk-{userId:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });

        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Keyword Hierarchy Test Game",
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
            FileName = "parent-chunk-test.pdf",
            FilePath = "/tmp/parent-chunk-test.pdf",
            FileSizeBytes = 2048,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id,
            Language = pdfLanguage
        });

        await _dbContext.SaveChangesAsync(Ct);
        return (sharedGame.Id, pdfId);
    }

    private async Task<Guid> AddChunkAsync(
        Guid gameId, Guid pdfId, int chunkIndex, short level, Guid? parentId, string content)
    {
        var chunkId = Guid.NewGuid();
        _dbContext!.TextChunks.Add(new TextChunkEntity
        {
            Id = chunkId,
            PdfDocumentId = pdfId,
            GameId = gameId,
            SharedGameId = gameId,
            ChunkIndex = chunkIndex,
            PageNumber = 1,
            Content = content,
            CharacterCount = content.Length,
            Level = level,
            ParentChunkId = parentId,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(Ct);
        return chunkId;
    }

    private async Task<Guid> AddVectorDocumentAsync(Guid gameId, Guid pdfId)
    {
        var vectorDocumentId = Guid.NewGuid();
        _dbContext!.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = gameId,
            PdfDocumentId = pdfId,
            ChunkCount = 4,
            TotalCharacters = 200,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = Dims
        });

        await _dbContext.SaveChangesAsync(Ct);
        return vectorDocumentId;
    }

    private async Task AddEmbeddingAsync(
        Guid vectorDocumentId, Guid gameId, int chunkIndex, string textContent, Guid? sourceChunkId)
    {
        var conn = _dbContext!.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(Ct);

        await using var cmd = (NpgsqlCommand)conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO pgvector_embeddings
                (id, vector_document_id, game_id, text_content, vector, model,
                 chunk_index, page_number, created_at, lang, is_translation, role_tags, source_chunk_id)
            VALUES
                (@id, @vdId, @gameId, @text, @vec, 'test-model',
                 @chunkIndex, 1, NOW(), 'en', false, 0, @sourceChunkId)
            """;

        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@vdId", vectorDocumentId);
        cmd.Parameters.AddWithValue("@gameId", gameId);
        cmd.Parameters.AddWithValue("@text", textContent);
        cmd.Parameters.AddWithValue("@vec", new Pgvector.Vector(MakeVector()));
        cmd.Parameters.AddWithValue("@chunkIndex", chunkIndex);
        cmd.Parameters.Add(new NpgsqlParameter("@sourceChunkId", NpgsqlTypes.NpgsqlDbType.Uuid)
        {
            Value = (object?)sourceChunkId ?? DBNull.Value
        });

        await cmd.ExecuteNonQueryAsync(Ct);
    }

    /// <summary>Un solo vettore per domanda e righe: ogni riga vale ~1,0 e tutte tornano.</summary>
    private static float[] MakeVector()
    {
        var v = new float[Dims];
        v[0] = 1f;
        return v;
    }
}
