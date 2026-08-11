using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Api.Infrastructure.DomainEventOutbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// End-to-end integration test for Issue #1852 cover propagation flow:
///   pipeline writes pdf_documents.cover_r2_key
///   + IDomainEventCollector.Collect(PdfCoverGeneratedEvent)
///   → MeepleAiDbContext.SaveChangesAsync dispatches via MediatR
///   → PdfCoverGeneratedEventHandler populates shared_games.pdf_cover_r2_key.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1852")]
public sealed class PdfCoverPropagationIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private IDomainEventCollector _eventCollector = null!;
    private IServiceProvider _serviceProvider = null!;

    public PdfCoverPropagationIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"cover_propagation_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // #3633: prerequisito necessario ma NON sufficiente — il test resta rosso anche con questo
        // override, la causa residua non è ancora stata isolata (vedi #3633).
        //
        // Serve comunque: il default DI è OutboxOnly (cutover T9 di #1535), quindi gli eventi vanno
        // in domain_event_outbox e NON sono pubblicati inline via MediatR — senza l'override
        // PdfCoverGeneratedEventHandler non verrebbe invocato in nessun caso. È la terza classe
        // rimasta indietro rispetto al cutover, dopo DomainEventDispatcherIntegrationTests e
        // FullStackCrossContextWorkflowTests (#3647).
        services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
            Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.Hybrid }));

        // PdfCoverGeneratedEventHandler depends on ISharedGameRepository, which is not
        // registered in the base builder (it lives in SharedGameCatalogServiceExtensions).
        // Register the real implementation so handler can load and update the SharedGame aggregate.
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();

        if (!string.IsNullOrEmpty(_testDbName))
            await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task PdfCoverGeneratedEvent_PropagatesToSharedGame_OnSaveChanges()
    {
        // Arrange — seed user, shared game, pdf document
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(CreateUser(userId));
        await _dbContext.SaveChangesAsync();

        var sg = CreateSharedGame(Guid.NewGuid(), userId, pdfCoverR2Key: null);
        _dbContext.SharedGames.Add(sg);
        await _dbContext.SaveChangesAsync();

        var pdf = CreatePdfDocument(Guid.NewGuid(), sg.Id, userId,
            coverGenerationStatus: "Pending",
            coverR2Key: null);
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Simulate pipeline write: update pdf columns (anemic EF update) + collect domain event
        var trackedPdf = await _dbContext.PdfDocuments.FindAsync(pdf.Id);
        trackedPdf!.CoverR2Key = "pdf-cover-integration";
        trackedPdf.CoverGenerationStatus = "Generated";
        trackedPdf.CoverPageIndex = 0;

        _eventCollector.Collect(new PdfCoverGeneratedEvent(
            pdfDocumentId: pdf.Id,
            sharedGameId: sg.Id,
            coverR2Key: "pdf-cover-integration",
            coverPageIndex: 0));

        // Act — SaveChangesAsync commits the EF write AND dispatches the event via MediatR,
        // which invokes PdfCoverGeneratedEventHandler → shared_games.pdf_cover_r2_key updated.
        await _dbContext.SaveChangesAsync();

        // Assert — handler must have populated shared_games.pdf_cover_r2_key
        var refreshed = await _dbContext.SharedGames.AsNoTracking().SingleAsync(x => x.Id == sg.Id);
        refreshed.PdfCoverR2Key.Should().Be("pdf-cover-integration");
    }

    [Fact]
    public async Task PdfCoverGeneratedEvent_NullSharedGameId_IsNoOp()
    {
        // Arrange — seed user + shared game
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(CreateUser(userId));
        await _dbContext.SaveChangesAsync();

        var sg = CreateSharedGame(Guid.NewGuid(), userId, pdfCoverR2Key: null);
        _dbContext.SharedGames.Add(sg);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Event with sharedGameId: null → handler logs and returns without touching DB
        _eventCollector.Collect(new PdfCoverGeneratedEvent(
            pdfDocumentId: Guid.NewGuid(),
            sharedGameId: null,
            coverR2Key: "should-not-propagate",
            coverPageIndex: 0));

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert — shared_games.pdf_cover_r2_key must remain NULL
        var refreshed = await _dbContext.SharedGames.AsNoTracking().SingleAsync(x => x.Id == sg.Id);
        refreshed.PdfCoverR2Key.Should().BeNull();
    }

    // ---------- seed helpers (column set copied from AddSharedGamePdfCoverR2KeyMigrationTests) ----------

    private static UserEntity CreateUser(Guid userId) => new()
    {
        Id = userId,
        Email = $"cover-prop-test-{userId:N}@meepleai.test",
        DisplayName = "Cover Propagation Test User",
        PasswordHash = "test-hash",
        Role = "user",
        Tier = "free",
        CreatedAt = DateTime.UtcNow
    };

    private static SharedGameEntity CreateSharedGame(Guid gameId, Guid createdBy, string? pdfCoverR2Key) => new()
    {
        Id = gameId,
        Title = $"Test Game {gameId:N}",
        YearPublished = 2024,
        Description = "Integration test game for cover propagation",
        MinPlayers = 1,
        MaxPlayers = 4,
        PlayingTimeMinutes = 60,
        MinAge = 10,
        ImageUrl = string.Empty,
        ThumbnailUrl = string.Empty,
        CreatedBy = createdBy,
        CreatedAt = DateTime.UtcNow,
        PdfCoverR2Key = pdfCoverR2Key
    };

    private static PdfDocumentEntity CreatePdfDocument(
        Guid pdfId,
        Guid sharedGameId,
        Guid uploadedByUserId,
        string coverGenerationStatus,
        string? coverR2Key) => new()
        {
            Id = pdfId,
            SharedGameId = sharedGameId,
            UploadedByUserId = uploadedByUserId,
            FileName = $"test-{pdfId:N}.pdf",
            FilePath = $"/test/{pdfId:N}.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            CoverGenerationStatus = coverGenerationStatus,
            CoverR2Key = coverR2Key
        };
}
