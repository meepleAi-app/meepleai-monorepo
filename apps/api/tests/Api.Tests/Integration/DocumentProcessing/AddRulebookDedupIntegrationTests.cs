using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Issue #2949 Task 2: proves AddRulebookCommandHandler, after migration to
/// IPdfDeduplicationService, still (a) reuses an existing Ready PDF via EntityLink
/// without creating a new record, and (b) performs a full upload when no hash matches.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Issue", "2949")]
[Trait("Category", TestCategories.Integration)]
public sealed class AddRulebookDedupIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AddRulebookDedupIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_addrulebookdedup_{Guid.NewGuid():N}";
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(conn);
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IPdfDeduplicationService, PdfDeduplicationService>();
        // The reuse branch calls CreateKbCardEntityLinkSafelyAsync → _mediator.Send(CreateEntityLinkCommand)
        // → CreateEntityLinkCommandHandler, which REQUIRES IEntityLinkRepository
        // (CreateEntityLinkCommandHandler.cs:18,22-27). Without this registration the handler's
        // broad catch(Exception) (AddRulebookCommandHandler.cs:339) swallows the DI failure, the
        // Game→KbCard link is never created, and the reuse test's linkExists assertion fails.
        // EntityLinkRepository's ctor (MeepleAiDbContext, IDomainEventCollector) resolves from
        // CreateBase's shared-kernel registrations; IUnitOfWork (also needed by the handler) is
        // likewise part of CreateBase. If BuildServiceProvider throws for IDomainEventCollector or
        // IUnitOfWork, register them explicitly — but attempt without first.
        services.AddScoped<IEntityLinkRepository, EntityLinkRepository>();
        services.AddScoped<AddRulebookCommandHandler>();

        // Blob storage returns a deterministic GUID FileId so the record Id is parseable.
        // BlobStorageResult is a POSITIONAL record (Success, FileId, FilePath, FileSizeBytes,
        // ErrorMessage = null) — Api/Services/Pdf/IBlobStorageService.cs:164-169 — so it MUST
        // be constructed positionally; an object-initializer fails to compile (CS7036).
        var blobMock = new Mock<IBlobStorageService>();
        blobMock.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobStorageResult(true, Guid.NewGuid().ToString(), "/test/rulebook.pdf", 1024));
        services.AddSingleton<IBlobStorageService>(blobMock.Object);

        // The minimal DI container above does NOT register IBackgroundTaskService or
        // IPdfUploadQuotaService, both NON-optional ctor params of AddRulebookCommandHandler
        // (AddRulebookCommandHandler.cs:43-52). Without them GetRequiredService<AddRulebookCommandHandler>()
        // throws at construction for BOTH tests. Register happy-path mocks.
        var backgroundTaskMock = new Mock<IBackgroundTaskService>();
        services.AddSingleton<IBackgroundTaskService>(backgroundTaskMock.Object);

        var quotaMock = new Mock<IPdfUploadQuotaService>();
        quotaMock.Setup(q => q.ReserveQuotaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QuotaReservationResult.Success(DateTime.UtcNow.AddHours(1)));
        services.AddSingleton<IPdfUploadQuotaService>(quotaMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await EnsureCreatedWithRetry(_dbContext);
        await SeedBaseDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        if (_serviceProvider is IAsyncDisposable ad) await ad.DisposeAsync();
        else (_serviceProvider as IDisposable)?.Dispose();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); } catch { /* ignore */ }
        }
    }

    private Guid _gameId;
    private Guid _userId;

    private async Task SeedBaseDataAsync()
    {
        _userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = _userId, Email = "rb@meepleai.dev", DisplayName = "RB", Role = "Editor",
            Tier = "Free", CreatedAt = DateTime.UtcNow
        });

        _gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = _gameId, Title = "Dedup Game", YearPublished = 2024, MinPlayers = 2,
            MaxPlayers = 4, PlayingTimeMinutes = 60, CreatedAt = DateTime.UtcNow
        });

        // User owns the game (AddRulebookCommandHandler enforces ownership).
        // NOTE: UserLibraryEntryEntity has NO CreatedAt property — its timestamp column is
        // AddedAt (defaults to DateTime.UtcNow), so we omit any timestamp initializer here.
        _dbContext.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(), UserId = _userId, SharedGameId = _gameId
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private static IFormFile MakePdfFile(byte[] content, string name = "rulebook.pdf")
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(name);
        mock.Setup(f => f.Length).Returns(content.Length);
        mock.Setup(f => f.ContentType).Returns("application/pdf");
        mock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return mock.Object;
    }

    // Minimal valid-looking PDF (>=50 bytes, %PDF- header).
    private static byte[] PdfBytes(string tag)
    {
        var body = "%PDF-1.4\n" + tag + new string('X', 64) + "\n%%EOF";
        return System.Text.Encoding.ASCII.GetBytes(body);
    }

    [Fact]
    public async Task Handle_DuplicateContentHash_ReusesExistingViaEntityLink_NoNewRecord()
    {
        // Arrange: a Ready PDF already exists for the game with a known content hash.
        var content = PdfBytes("dup");
        var dedup = _serviceProvider!.GetRequiredService<IPdfDeduplicationService>();
        string hash;
        using (var s = new MemoryStream(content))
            hash = await dedup.ComputeContentHashAsync(s, TestCancellationToken);

        var existingId = Guid.NewGuid();
        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = existingId, SharedGameId = _gameId, UploadedByUserId = _userId,
            FileName = "existing.pdf", FilePath = "/test/existing.pdf", FileSizeBytes = content.Length,
            UploadedAt = DateTime.UtcNow, ContentHash = hash,
            ProcessingState = nameof(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready)
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var handler = _serviceProvider.GetRequiredService<AddRulebookCommandHandler>();
        var command = new AddRulebookCommand(_gameId, _userId, MakePdfFile(content));

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: reused, not new; no second PDF record created.
        result.IsNew.Should().BeFalse();
        result.PdfDocumentId.Should().Be(existingId);

        var pdfCount = await _dbContext.PdfDocuments.CountAsync(p => p.SharedGameId == _gameId, TestCancellationToken);
        pdfCount.Should().Be(1, "duplicate content must reuse the existing record");

        var linkExists = await _dbContext.EntityLinks.AnyAsync(
            el => el.SourceEntityId == _gameId && el.TargetEntityId == existingId
                  && el.TargetEntityType == MeepleEntityType.KbCard,
            TestCancellationToken);
        linkExists.Should().BeTrue("reuse must create the Game->KbCard EntityLink");
    }

    [Fact]
    public async Task Handle_NoHashMatch_PerformsFullUpload_CreatesNewRecord()
    {
        // Arrange: no existing PDF with this hash.
        var handler = _serviceProvider!.GetRequiredService<AddRulebookCommandHandler>();
        var command = new AddRulebookCommand(_gameId, _userId, MakePdfFile(PdfBytes("fresh")));

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.IsNew.Should().BeTrue();
        var pdfCount = await _dbContext!.PdfDocuments.CountAsync(p => p.SharedGameId == _gameId, TestCancellationToken);
        pdfCount.Should().Be(1, "a fresh upload must create exactly one new record");
    }

    private static async Task EnsureCreatedWithRetry(MeepleAiDbContext context)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try { await context.Database.MigrateAsync(TestCancellationToken); return; }
            catch (NpgsqlException) when (attempt < maxAttempts)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }
}
