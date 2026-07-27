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
/// Integration tests for <see cref="KeywordSearchService"/> against a real Postgres
/// (pgvector) Testcontainer — the residual "FTS integration test" tracked by SP3 (#3269) as a
/// hardening follow-up of the 1/3 keyword-FTS work (#3242).
///
/// <para>
/// These exercise behaviour that a unit test with an in-memory provider CANNOT: the SQL runs
/// against the GENERATED <c>search_vector</c> column and a query-time
/// <c>to_tsvector(&lt;cfg&gt;, "Content")</c>, so the schema must be built via the real EF
/// migrations (<c>MigrateAsync</c>, not <c>EnsureCreated</c>) — the migration
/// <c>20260713065129_RestoreSearchVectorColumns</c> creates the generated column + GIN index via
/// raw SQL that <c>EnsureCreated</c> would drop.
/// </para>
///
/// <para>
/// The regression under test (#2569 / #3242): a keyword query must be matched with the FTS config
/// of the document's own language. <see cref="KeywordSearchService"/> detects the game's dominant
/// language from <c>pdf_documents.Language</c> (ResolveGameFtsConfigAsync) and OVERRIDES the
/// caller's default so an Italian rulebook is stemmed with the <c>italian</c> config — plus the
/// Slice-A intent-synonym expansion (<c>setup → preparazione/allestimento</c>) so the English
/// loanword query still hits the native lexemes. The contrast test documents the footgun: the same
/// Italian content mislabelled as <c>en</c> silently returns nothing.
/// </para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3269")]
public sealed class KeywordSearchServiceItalianFtsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Setup section written in Italian — "Preparazione" stems to the same 'italian' lexeme the
    // Slice-A synonym expansion of "setup" targets. Deliberately avoids the literal token "setup".
    private const string SetupChunkContent =
        "Preparazione della partita per due giocatori: disponi la plancia principale al centro del " +
        "tavolo e assegna a ciascun giocatore le tessere e le risorse iniziali prima di cominciare.";

    // Noise chunk (Italian, but no setup/preparazione/allestimento lexeme) — proves selectivity.
    private const string NoiseChunkContent =
        "Titoli di coda e ringraziamenti. Il gioco e stato progettato e illustrato dallo studio.";

    // English setup section carrying the literal token "setup" — used to positively exercise the
    // ENGLISH hot path (the GENERATED search_vector column), the code path the mislabelled test does
    // NOT prove works.
    private const string EnglishSetupChunkContent =
        "Game setup for two players: place the main board at the center of the table and deal the " +
        "starting tiles and resources to each player before beginning the first round.";

    public KeywordSearchServiceItalianFtsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_keyword_it_fts_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
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

    [Fact]
    public async Task SearchAsync_ItalianDocument_SetupLoanwordQuery_OverridesToItalianAndFindsPreparazioneChunk()
    {
        // Arrange: an Italian-labelled document whose setup section only uses "Preparazione".
        var (gameId, setupChunkId, noiseChunkId) = await SeedGameWithChunksAsync(pdfLanguage: "it");
        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);

        // Act: caller passes the default English language; the service must detect the document's
        // Italian language and expand "setup" -> preparazione/allestimento so the query still hits.
        var results = await service.SearchAsync(
            query: "setup",
            gameId: gameId,
            language: "en",
            cancellationToken: TestCancellationToken);

        // Assert: the Italian setup chunk is retrieved (would return 0 under the pre-#3242 behaviour),
        // and the synonym expansion of the loanword stays selective (does not drag in the noise chunk).
        results.Should().NotBeEmpty("the Italian document language must override the caller's default 'en' config");
        results.Should().Contain(r => Guid.Parse(r.ChunkId) == setupChunkId);
        results.Should().NotContain(r => Guid.Parse(r.ChunkId) == noiseChunkId);
        results.Should().OnlyContain(r => r.RelevanceScore > 0);
    }

    [Fact]
    public async Task SearchAsync_ItalianDocument_DirectPreparazioneQuery_FindsChunkAndExcludesNoise()
    {
        // Arrange
        var (gameId, setupChunkId, noiseChunkId) = await SeedGameWithChunksAsync(pdfLanguage: "it");
        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);

        // Act: a native-lexeme query under the resolved 'italian' config.
        var results = await service.SearchAsync(
            query: "preparazione",
            gameId: gameId,
            language: "it",
            cancellationToken: TestCancellationToken);

        // Assert: matches the setup chunk, not the unrelated noise chunk.
        results.Should().ContainSingle();
        Guid.Parse(results[0].ChunkId).Should().Be(setupChunkId);
        results.Should().NotContain(r => Guid.Parse(r.ChunkId) == noiseChunkId);
    }

    [Fact]
    public async Task SearchAsync_MislabeledEnglishDocument_ItalianContent_SetupQuery_ReturnsNoMatch()
    {
        // Arrange: the SAME Italian content, but the document is (wrongly) labelled English.
        // This is the #2569 footgun the per-game language detection guards against: with an
        // 'english' config there is no synonym expansion and "setup" never matches "preparazione".
        var (gameId, _, _) = await SeedGameWithChunksAsync(pdfLanguage: "en");
        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);

        // Act
        var results = await service.SearchAsync(
            query: "setup",
            gameId: gameId,
            language: "en",
            cancellationToken: TestCancellationToken);

        // Assert: proves the match in the happy-path test is genuinely language-driven (non-vacuous).
        results.Should().BeEmpty("an 'english'-configured query cannot stem or synonym-expand into the Italian lexeme");
    }

    [Fact]
    public async Task SearchAsync_EnglishDocument_EnglishQuery_MatchesViaGeneratedSearchVectorColumn()
    {
        // Positive coverage of the ENGLISH hot path: an 'english'-resolved config matches against the
        // GENERATED search_vector column (KeywordSearchService.cs:87-88), NOT the query-time
        // to_tsvector the Italian tests exercise. This proves that code path genuinely works, so the
        // mislabelled test's BeEmpty isolates the language cause rather than a silently-broken english
        // branch (an empty/missing search_vector would also make this test fail, surfacing the problem).
        var (gameId, setupChunkId, _) = await SeedGameWithChunksAsync(
            pdfLanguage: "en", setupContent: EnglishSetupChunkContent);
        var service = new KeywordSearchService(_dbContext!, NullLogger<KeywordSearchService>.Instance);

        // Act: "setup" is a literal token in the English content and resolves to the 'english' config.
        var results = await service.SearchAsync(
            query: "setup",
            gameId: gameId,
            language: "en",
            cancellationToken: TestCancellationToken);

        results.Should().Contain(
            r => Guid.Parse(r.ChunkId) == setupChunkId,
            "the generated english search_vector column must match the literal 'setup' token in the content");
    }

    // ─── Seed helper ───────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds User -> SharedGame -> PdfDocument(Language=<paramref name="pdfLanguage"/>) -> 2 chunks
    /// (one Italian setup section, one Italian noise section). <c>text_chunks.GameId</c> and the
    /// <c>SearchAsync(gameId)</c> argument are the shared-game id (the corpus keys chunks by GameId).
    /// The generated <c>search_vector</c> column is left unset — Postgres computes it on insert.
    /// </summary>
    private async Task<(Guid GameId, Guid SetupChunkId, Guid NoiseChunkId)> SeedGameWithChunksAsync(
        string pdfLanguage, string? setupContent = null, string? noiseContent = null)
    {
        var setup = setupContent ?? SetupChunkContent;
        var noise = noiseContent ?? NoiseChunkContent;

        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"keyword-it-fts-{userId:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });

        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Keyword IT FTS Test Game",
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
            FileName = "keyword-it-fts-test.pdf",
            FilePath = "/tmp/keyword-it-fts-test.pdf",
            FileSizeBytes = 2048,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id,
            Language = pdfLanguage
        });

        var setupChunkId = Guid.NewGuid();
        var noiseChunkId = Guid.NewGuid();

        _dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = setupChunkId,
            PdfDocumentId = pdfId,
            GameId = sharedGame.Id,
            SharedGameId = sharedGame.Id,
            ChunkIndex = 0,
            PageNumber = 1,
            Content = setup,
            CharacterCount = setup.Length,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = noiseChunkId,
            PdfDocumentId = pdfId,
            GameId = sharedGame.Id,
            SharedGameId = sharedGame.Id,
            ChunkIndex = 1,
            PageNumber = 2,
            Content = noise,
            CharacterCount = noise.Length,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        return (sharedGame.Id, setupChunkId, noiseChunkId);
    }
}
