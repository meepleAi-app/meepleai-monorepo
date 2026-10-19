using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
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
/// #4347 (assorbita da #4390), su Postgres: una transizione di stato fatta dal repository non deve
/// cancellare ciò che l'aggregato non porta. Il caso della issue è l'avanzamento: ogni ramo d'errore del
/// caricamento scrive <c>Failed</c> con il messaggio e poi passa a <c>Failed</c> dal repository, che lo
/// riportava a NULL. Il test unit <c>PdfDocumentRepositoryUnownedColumnsTests</c> copre tutte le colonne su
/// InMemory; qui si prova l'SQL vero.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4347")]
public sealed class PdfDocumentRepositoryUnownedColumnsIntegrationTests : IAsyncLifetime
{
    private const string FailedProgress =
        """{"CurrentStep":"Failed","PercentComplete":0,"ErrorMessage":"Embedding service unreachable"}""";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public PdfDocumentRepositoryUnownedColumnsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"pdf_unowned_cols_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static PdfDocumentRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    /// <summary>
    /// Utente e gioco prima del documento: sono FK di <c>pdf_documents</c>, e con id inventati l'INSERT
    /// fallisce con 23503 prima di arrivare a ciò che si prova (pitfall #2620).
    /// </summary>
    private async Task<Guid> SeedDocumentAsync(Func<PdfDocumentBuilder, PdfDocumentBuilder> state, Action<PdfDocumentEntity> columns)
    {
        var userId = Guid.NewGuid();
        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = userId,
            Email = $"pdf-unowned-{userId:N}@test.com",
            DisplayName = "Pdf Unowned Columns Test User",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        });
        var gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gioco con regolamento" });
        await _dbContext.SaveChangesAsync();

        var document = state(new PdfDocumentBuilder().WithGameId(gameId).WithUploadedBy(userId)).Build();
        var repository = CreateRepository(_dbContext);
        await repository.AddAsync(document, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Le colonne che l'aggregato non porta si scrivono come fa la pipeline: sull'entità.
        var entity = await _dbContext.PdfDocuments.AsTracking().SingleAsync(p => p.Id == document.Id);
        columns(entity);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        return document.Id;
    }

    private async Task<PdfDocumentEntity> ReloadAsync(Guid id)
    {
        await using var fresh = _fixture.CreateDbContext(_connectionString);
        return await fresh.PdfDocuments.AsNoTracking().SingleAsync(p => p.Id == id);
    }

    [Fact]
    public async Task MarkAsFailed_ThroughTheRepository_KeepsTheFailedProgressAndItsMessage()
    {
        // Arrange: come nel caricamento, prima l'avanzamento Failed con il messaggio, poi la transizione.
        var id = await SeedDocumentAsync(b => b.ThatIsProcessing(), e => e.ProcessingProgressJson = FailedProgress);
        var repository = CreateRepository(_dbContext);

        // Act
        var document = await repository.GetByIdAsync(id, CancellationToken.None);
        document!.MarkAsFailed("Embedding service unreachable", ErrorCategory.Service, PdfProcessingState.Embedding);
        await repository.UpdateAsync(document, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        var after = await ReloadAsync(id);
        after.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed));
        after.ProcessingProgressJson.Should().Be(FailedProgress,
            "GET /pdfs/{id}/progress legge solo questa colonna: azzerata, risponde con un corpo vuoto");
    }

    [Fact]
    public async Task MetadataChange_OnAReadyPdf_KeepsItsContentProvenanceAndQueueFields()
    {
        // Arrange: un PDF pronto, con ciò che la pipeline e la coda gli hanno scritto.
        var seededAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var id = await SeedDocumentAsync(b => b.ThatIsCompleted(), e =>
        {
            e.ExtractedText = "Setup. Place 3 tiles on the board.";
            e.CharacterCount = 34;
            e.StructuredElementsJson = """[{"text":"Setup","page":1,"type":"Title"}]""";
            e.IndexerVersion = "v-sentinella";
            e.ProcessingPriority = "Admin";
            e.LicenseType = 2;
            e.ImageRegionsSeededAt = seededAt;
            e.ImageRegionSeedAttempts = 1;
            e.ProcessingProgressJson = """{"CurrentStep":"Completed","PercentComplete":100}""";
        });
        var repository = CreateRepository(_dbContext);

        // Act: un cambio di metadati qualsiasi (SetActiveForRagCommandHandler).
        var document = await repository.GetByIdAsync(id, CancellationToken.None);
        document!.SetActiveForRag(false);
        await repository.UpdateAsync(document, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        var after = await ReloadAsync(id);
        after.IsActiveForRag.Should().BeFalse("la modifica dell'aggregato arriva al DB");
        after.ExtractedText.Should().Be("Setup. Place 3 tiles on the board.");
        after.CharacterCount.Should().Be(34);
        after.StructuredElementsJson.Should().NotBeNull();
        after.IndexerVersion.Should().Be("v-sentinella", "senza versione il PDF torna fra quelli da reindicizzare");
        after.ProcessingPriority.Should().Be("Admin");
        after.LicenseType.Should().Be(2);
        after.ImageRegionsSeededAt.Should().Be(seededAt, "azzerata, il seed delle immagini ripartirebbe");
        after.ImageRegionSeedAttempts.Should().Be(1);
        after.ProcessingProgressJson.Should().Contain("Completed");
    }

    [Fact]
    public async Task Retry_StartsTheNewRunWithoutThePreviousProgress()
    {
        // Arrange: un PDF fallito, con l'avanzamento Failed del suo giro.
        var id = await SeedDocumentAsync(b => b.ThatIsFailed(), e => e.ProcessingProgressJson = FailedProgress);
        var repository = CreateRepository(_dbContext);

        // Act: nuovo tentativo (RetryPdfProcessingCommandHandler). Lo elabora la coda, che non scrive
        // l'avanzamento: se restasse quello Failed, il client smetterebbe di interrogare al primo colpo.
        var document = await repository.GetByIdAsync(id, CancellationToken.None);
        document!.Retry();
        await repository.UpdateAsync(document, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        // Assert
        var after = await ReloadAsync(id);
        after.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        after.ProcessingProgressJson.Should().BeNull("un giro nuovo comincia senza l'avanzamento del precedente");
    }
}
