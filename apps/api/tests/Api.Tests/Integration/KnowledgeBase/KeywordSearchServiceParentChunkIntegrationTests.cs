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
/// #4955: il braccio lessicale non restituisce un chunk che ha figli. È il genitore di
/// <c>AdvancedChunkingService</c>, che concatena i suoi figli; quando l'estrazione non ha titoli la
/// sezione è una sola e il genitore è il documento intero. <c>ts_rank_cd</c> normalizzato per il
/// logaritmo della lunghezza lo premia per numero di occorrenze: sul gate il chunk 0 di Maracaibo
/// valeva 1,24 per una domanda su Ark Nova, con le sole «e», «di», «come». Serve Postgres vero: il
/// filtro sta nella SQL.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4955")]
public sealed class KeywordSearchServiceParentChunkIntegrationTests : IAsyncLifetime
{
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

    public KeywordSearchServiceParentChunkIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_keyword_parent_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
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

    /// <summary>
    /// Il genitore contiene ogni parola dei figli, quindi corrisponde alla domanda quanto loro e senza
    /// filtro sarebbe fra i risultati. Il percorso inglese legge la colonna generata
    /// <c>search_vector</c>, quello italiano <c>to_tsvector</c> al momento della query: il filtro vale
    /// per entrambi.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    public async Task SearchAsync_AChunkWithChildren_IsNotReturned_ItsChildrenAre(string language)
    {
        var children = language == "it" ? ItalianChildren : EnglishChildren;
        var (gameId, pdfId) = await SeedGameAsync(language);
        var parentId = await AddChunkAsync(gameId, pdfId, chunkIndex: 0, level: 0, parentId: null, string.Join(' ', children));
        var childIds = new List<Guid>();
        for (var i = 0; i < children.Length; i++)
            childIds.Add(await AddChunkAsync(gameId, pdfId, chunkIndex: i + 1, level: 2, parentId, children[i]));

        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);
        var question = language == "it" ? ItalianQuestion : EnglishQuestion;
        var results = await service.SearchAsync(question, gameId, language: language, cancellationToken: Ct);

        var returned = results.Select(r => Guid.Parse(r.ChunkId)).ToList();
        returned.Should().NotContain(parentId,
            "the parent only repeats its children's text, and its length decides its rank, not its relevance");
        returned.Should().BeEquivalentTo(childIds,
            "each child carries a word of the question, and the filter must not take them along with the parent");
    }

    /// <summary>
    /// Un chunk radice senza figli è l'unico che porta il suo testo: il paragrafo corto di
    /// <c>ProcessPlainText</c>, o un documento indicizzato senza gerarchia. Un filtro su
    /// <c>Level</c> o su <c>ParentChunkId</c> lo perderebbe.
    /// </summary>
    [Fact]
    public async Task SearchAsync_ARootChunkWithoutChildren_IsStillReturned()
    {
        var (gameId, pdfId) = await SeedGameAsync("en");
        var rootId = await AddChunkAsync(gameId, pdfId, chunkIndex: 0, level: 0, parentId: null, EnglishChildren[0]);

        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);
        var results = await service.SearchAsync(EnglishQuestion, gameId, language: "en", cancellationToken: Ct);

        results.Select(r => Guid.Parse(r.ChunkId)).Should().ContainSingle()
            .Which.Should().Be(rootId, "nothing else carries this text, so excluding it would lose it");
    }

    private async Task<(Guid GameId, Guid PdfId)> SeedGameAsync(string pdfLanguage)
    {
        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"keyword-parent-{userId:N}@test.local",
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
            FileName = "keyword-parent-test.pdf",
            FilePath = "/tmp/keyword-parent-test.pdf",
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
}
