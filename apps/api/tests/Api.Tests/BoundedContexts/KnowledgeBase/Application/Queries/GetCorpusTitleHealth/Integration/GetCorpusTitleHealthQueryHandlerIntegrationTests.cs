using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetCorpusTitleHealth;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.GetCorpusTitleHealth.Integration;

/// <summary>
/// Integration tests for <see cref="GetCorpusTitleHealthQueryHandler"/> against a real PostgreSQL
/// database via Testcontainers (Epic #3338 WP3). The handler's value is its EF Core query — a
/// nullable-<c>GameId</c>-lifted join <c>text_chunks → shared_games → pdf_documents</c>, a
/// <c>LanguageOverride ?? Language</c> coalesce, and a server-side <c>Distinct()</c> over a named
/// projection — so these tests assert that translation end-to-end (a unit test with mocks would not
/// catch an untranslatable LINQ shape). The band/plausibility maths itself is covered by
/// <c>TitleHealthMetricTests</c>.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3338")]
public sealed class GetCorpusTitleHealthQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Guid _uploaderId = Guid.NewGuid();
    private string _dbName = null!;
    private MeepleAiDbContext _dbContext = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public GetCorpusTitleHealthQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_corpus_title_health_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);

        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_dbName);
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();
    }

    [Fact(Timeout = 90_000)]
    public async Task Handle_RealPostgres_GroupsPerGame_ComputesBandLanguageAndCoverage()
    {
        // Arrange ----------------------------------------------------------------
        // pdf_documents.UploadedByUserId is a Restrict FK to users → seed the uploader once.
        _dbContext.Users.Add(new UserEntity
        {
            Id = _uploaderId,
            Email = $"title-health-{_uploaderId:N}@test.local",
            DisplayName = "Title Health Test User",
            PasswordHash = "hashed",
            Role = "Admin",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = false,
        });

        // "Alpha Green" (en): 5 clean section titles, each repeated by a child chunk → distinct=5,
        // fraction=1.0 → green; SETUP is a lexicon section → canonical ≥ 1.
        var greenId = Guid.NewGuid();
        var greenPdf = Guid.NewGuid();
        SeedGame(greenId, "Alpha Green");
        SeedPdf(greenPdf, language: "en");
        AddChunks(greenPdf, greenId, new[] { "SETUP", "GAMEPLAY", "SCORING", "END OF GAME", "COMPONENTS" });
        AddChunks(greenPdf, greenId, new[] { "SETUP", "GAMEPLAY" }); // duplicate headings from child chunks

        // "Zeta Garbage" (effective it via LanguageOverride over Language=en): 4 garbage headings + 1
        // real "PREPARAZIONE" → distinct=5, plausible=1, fraction=0.2 → red; PREPARAZIONE is a lexicon
        // section → canonical=1.
        var redId = Guid.NewGuid();
        var redPdf = Guid.NewGuid();
        SeedGame(redId, "Zeta Garbage");
        SeedPdf(redPdf, language: "en", languageOverride: "it");
        AddChunks(redPdf, redId, new[] { "PREPARAZIONE", "D", "S U", "I L E X Y R F", "(14%), Oceani" });

        // "Multi Lang": headings from TWO pdfs of different languages. en pdf has 2 distinct headings,
        // it pdf has 1 → dominant language = en. distinct=3 (SETUP, GAMEPLAY, D), plausible=2 →
        // fraction=0.6667 → yellow.
        var multiId = Guid.NewGuid();
        var multiEnPdf = Guid.NewGuid();
        var multiItPdf = Guid.NewGuid();
        SeedGame(multiId, "Multi Lang");
        SeedPdf(multiEnPdf, language: "en");
        SeedPdf(multiItPdf, language: "it");
        AddChunks(multiEnPdf, multiId, new[] { "SETUP", "GAMEPLAY" });
        AddChunks(multiItPdf, multiId, new[] { "D" });

        // "No Headings": every chunk has a null/blank heading → the game has no extraction signal and
        // MUST NOT appear in the read-out.
        var noneId = Guid.NewGuid();
        var nonePdf = Guid.NewGuid();
        SeedGame(noneId, "No Headings");
        SeedPdf(nonePdf, language: "en");
        AddChunks(nonePdf, noneId, new string?[] { null, "", "   " });

        await _dbContext.SaveChangesAsync(Token);

        var sut = new GetCorpusTitleHealthQueryHandler(_dbContext);

        // Act --------------------------------------------------------------------
        var result = await sut.Handle(new GetCorpusTitleHealthQuery(), Token);

        // Assert -----------------------------------------------------------------
        result.Should().HaveCount(3, "the null-heading-only game contributes no signal and is excluded");
        result.Select(r => r.GameTitle).Should().ContainInOrder("Alpha Green", "Multi Lang", "Zeta Garbage");
        result.Select(r => r.GameTitle).Should().NotContain("No Headings");

        var green = result.Single(r => r.GameTitle == "Alpha Green");
        green.Band.Should().Be("green");
        green.DistinctHeadings.Should().Be(5, "duplicate child-chunk headings are deduplicated");
        green.PlausibleHeadings.Should().Be(5);
        green.PlausibleFraction.Should().Be(1.0);
        green.Language.Should().Be("en");
        green.CanonicalCoverage.Should().BeGreaterThanOrEqualTo(1, "SETUP is a curated lexicon section");

        var red = result.Single(r => r.GameTitle == "Zeta Garbage");
        red.Band.Should().Be("red");
        red.DistinctHeadings.Should().Be(5);
        red.PlausibleHeadings.Should().Be(1, "only PREPARAZIONE survives the plausibility filter");
        red.PlausibleFraction.Should().Be(0.2);
        red.CanonicalCoverage.Should().Be(1);
        red.Language.Should().Be("it", "LanguageOverride ?? Language must resolve to the override");

        var multi = result.Single(r => r.GameTitle == "Multi Lang");
        multi.Band.Should().Be("yellow");
        multi.DistinctHeadings.Should().Be(3);
        multi.PlausibleHeadings.Should().Be(2);
        multi.PlausibleFraction.Should().Be(0.6667, "2/3 rounded to 4 dp");
        multi.Language.Should().Be("en", "the language carrying more distinct headings wins the tiebreak");
    }

    [Fact(Timeout = 90_000)]
    public async Task Handle_RealPostgres_EmptyCorpus_ReturnsEmpty()
    {
        var sut = new GetCorpusTitleHealthQueryHandler(_dbContext);

        var result = await sut.Handle(new GetCorpusTitleHealthQuery(), Token);

        result.Should().BeEmpty("no chunks → no games to report");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private void SeedGame(Guid id, string title) => _dbContext.SharedGames.Add(new SharedGameEntity
    {
        Id = id,
        Title = title,
        CreatedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    });

    private void SeedPdf(Guid id, string language, string? languageOverride = null) =>
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = $"rulebook-{id:N}.pdf",
            FilePath = $"/test/{id:N}.pdf",
            FileSizeBytes = 1024L,
            UploadedByUserId = _uploaderId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            Language = language,
            LanguageOverride = languageOverride,
        });

    private void AddChunks(Guid pdfId, Guid gameId, IReadOnlyList<string?> headings)
    {
        for (var i = 0; i < headings.Count; i++)
        {
            _dbContext.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                GameId = gameId,
                Heading = headings[i],
                Content = $"body-{pdfId:N}-{i}",
                ChunkIndex = i,
                CharacterCount = 100,
            });
        }
    }
}
