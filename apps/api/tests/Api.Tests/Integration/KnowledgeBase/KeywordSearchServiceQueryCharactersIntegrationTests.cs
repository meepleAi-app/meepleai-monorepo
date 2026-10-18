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
/// #4479: la domanda di un utente arriva a <c>to_tsquery</c>, e la sua punteggiatura non deve romperlo.
/// Trovato con la prima misura delle domande di riferimento (#4470): «Per usare un porto specializzato
/// (2:1), devo aver costruito una città o basta un insediamento?» mandava in errore la chat con
/// <c>42601: syntax error in tsquery</c>, perché i due punti sono sintassi del tsquery. Serve Postgres
/// vero: è il suo parser che rifiuta la query.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4479")]
public sealed class KeywordSearchServiceQueryCharactersIntegrationTests : IAsyncLifetime
{
    private const string PortQuestion =
        "Per usare un porto specializzato (2:1), devo aver costruito una città o basta un insediamento?";

    private const string ItalianPortChunk =
        "Commercio marittimo: chi possiede un insediamento o una città su un porto specializzato " +
        "scambia 2 carte della risorsa indicata con 1 carta a scelta della riserva.";

    private const string EnglishPortChunk =
        "Maritime trade: a player with a settlement or a city on a special harbor may trade 2 cards " +
        "of the matching resource for 1 card of their choice from the supply.";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public KeywordSearchServiceQueryCharactersIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_keyword_chars_{Guid.NewGuid():N}";
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
    /// Il percorso italiano (to_tsvector al momento della query) e quello inglese (la colonna generata
    /// <c>search_vector</c>) passano entrambi il tsquery a <c>to_tsquery</c>.
    /// </summary>
    [Theory]
    [InlineData("it", ItalianPortChunk)]
    [InlineData("en", EnglishPortChunk)]
    public async Task SearchAsync_AQuestionWithAColon_FindsTheRule_InsteadOfFailing(string language, string content)
    {
        var (gameId, chunkId) = await SeedGameWithChunkAsync(language, content);
        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);

        var question = language == "it"
            ? PortQuestion
            : "To use a special harbor (2:1), do I need a city or is a settlement enough?";
        var results = await service.SearchAsync(question, gameId, language: language, cancellationToken: Ct);

        results.Should().Contain(r => Guid.Parse(r.ChunkId) == chunkId,
            "the rule is in the chunk, and the colon of «2:1» is punctuation, not tsquery syntax");
    }

    [Theory]
    [InlineData("carta:*")]
    [InlineData("turno:A")]
    [InlineData(":")]
    [InlineData("porto <2> insediamento")]
    public async Task SearchAsync_TsQuerySyntaxInTheQuestion_IsNotAnError(string question)
    {
        var (gameId, _) = await SeedGameWithChunkAsync("it", ItalianPortChunk);
        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);

        var act = () => service.SearchAsync(question, gameId, language: "it", cancellationToken: Ct);

        await act.Should().NotThrowAsync();
    }

    private async Task<(Guid GameId, Guid ChunkId)> SeedGameWithChunkAsync(string pdfLanguage, string content)
    {
        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"keyword-chars-{userId:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });

        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Keyword Characters Test Game",
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
            FileName = "keyword-chars-test.pdf",
            FilePath = "/tmp/keyword-chars-test.pdf",
            FileSizeBytes = 2048,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id,
            Language = pdfLanguage
        });

        var chunkId = Guid.NewGuid();
        _dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = chunkId,
            PdfDocumentId = pdfId,
            GameId = sharedGame.Id,
            SharedGameId = sharedGame.Id,
            ChunkIndex = 0,
            PageNumber = 1,
            Content = content,
            CharacterCount = content.Length,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(Ct);
        return (sharedGame.Id, chunkId);
    }
}
