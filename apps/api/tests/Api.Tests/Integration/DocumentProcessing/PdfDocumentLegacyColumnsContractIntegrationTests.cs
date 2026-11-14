using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// La fase contract di #4440, su Postgres: la migrazione toglie da <c>pdf_documents</c> le sei colonne dell'estrattore
/// iText (tabelle, diagrammi, regole atomiche e i loro contatori) e lascia il contenuto estratto che le pipeline usano.
/// Nessun check sulle PR verso main-dev vede un difetto di schema di questa tabella: i test del gestore usano InMemory.
/// Una colonna non tolta rende rosso il primo test con il suo nome; una proprieta' rimessa nell'entita' rende rosso il
/// secondo con 42703.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4440")]
[Trait("Issue", "4930")]
public sealed class PdfDocumentLegacyColumnsContractIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public PdfDocumentLegacyColumnsContractIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"pdf_legacy_cols_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await using var db = _fixture.CreateDbContext(_connectionString);
        await db.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static PdfDocumentRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    [Fact]
    public async Task Migrations_DropTheSixLegacyColumns_AndKeepTheExtractedContent()
    {
        await using var db = _fixture.CreateDbContext(_connectionString);

        // I nomi come stanno nel DB: le sei colonne legacy ed ExtractedText non hanno HasColumnName, quindi sono PascalCase.
        var columns = await db.Database
            .SqlQuery<string>($"""
                SELECT column_name AS "Value" FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'pdf_documents'
                  AND column_name IN ('ExtractedTables', 'ExtractedDiagrams', 'AtomicRules',
                                      'TableCount', 'DiagramCount', 'AtomicRuleCount',
                                      'ExtractedText', 'structured_elements_json')
                ORDER BY column_name
                """)
            .ToListAsync(Ct);

        columns.Should().Equal(["ExtractedText", "structured_elements_json"],
            "#4440 toglie le sei colonne dell'estrattore iText; il testo e gli elementi strutturati restano");
    }

    [Fact]
    public async Task PdfDocument_RoundTripsThroughTheRepository_OnTheMigratedSchema()
    {
        // Utente e gioco prima del documento: sono FK di pdf_documents (pitfall #2620).
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        await using (var db = _fixture.CreateDbContext(_connectionString))
        {
            db.Set<UserEntity>().Add(new UserEntity
            {
                Id = userId,
                Email = $"pdf-legacy-{userId:N}@test.com",
                DisplayName = "Pdf Legacy Columns Test User",
                Role = "user",
                Tier = "free",
                CreatedAt = DateTime.UtcNow
            });
            db.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gioco con regolamento" });
            await db.SaveChangesAsync(Ct);
        }

        // Un modello che mappa ancora una colonna tolta fallisce qui, sull'INSERT e su ogni lettura, con 42703.
        var document = new PdfDocumentBuilder().WithGameId(gameId).WithUploadedBy(userId).ThatIsCompleted().Build();
        await using (var db = _fixture.CreateDbContext(_connectionString))
        {
            await CreateRepository(db).AddAsync(document, Ct);
            await db.SaveChangesAsync(Ct);
        }

        // Il contenuto estratto si scrive come fa la pipeline: sull'entita'.
        await using (var db = _fixture.CreateDbContext(_connectionString))
        {
            var entity = await db.PdfDocuments.AsTracking().SingleAsync(p => p.Id == document.Id, Ct);
            entity.ExtractedText = "Setup. Place 3 tiles on the board.";
            entity.StructuredElementsJson = """[{"text":"Setup","page":1,"type":"Title"}]""";
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = _fixture.CreateDbContext(_connectionString))
        {
            var repository = CreateRepository(db);
            var read = await repository.GetByIdAsync(document.Id, Ct);
            read!.SetActiveForRag(false);
            await repository.UpdateAsync(read, Ct);
            await db.SaveChangesAsync(Ct);
        }

        await using var check = _fixture.CreateDbContext(_connectionString);
        var stored = await check.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == document.Id, Ct);
        stored.IsActiveForRag.Should().BeFalse();
        stored.ExtractedText.Should().Be("Setup. Place 3 tiles on the board.");
        stored.StructuredElementsJson.Should().NotBeNull();
    }
}
