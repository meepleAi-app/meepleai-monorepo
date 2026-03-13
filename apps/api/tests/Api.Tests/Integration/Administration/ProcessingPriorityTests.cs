using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests verifying that LaunchAdminPdfProcessingCommand sets ProcessingPriority="Admin"
/// in real PostgreSQL and the change is durable across DB reads.
/// Issue #4673: Validate that priority assignment works with real schema + constraints.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4673")]
public sealed class ProcessingPriorityTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;

    private static readonly Guid UserId = Guid.NewGuid();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ProcessingPriorityTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_processing_priority_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        // MeepleAiDbContext requires IMediator + IDomainEventCollector; provide no-op mocks
        services.AddSingleton<IMediator>(new Mock<IMediator>().Object);
        services.AddSingleton<Api.SharedKernel.Application.Services.IDomainEventCollector>(
            new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>().Object);
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private async Task EnsureUserSeededAsync()
    {
        if (!_dbContext!.Users.Any(u => u.Id == UserId))
        {
            _dbContext.Users.Add(new UserEntity
            {
                Id = UserId,
                Email = "admin@meepleai.dev",
                Tier = "premium",
                Role = "admin",
            });
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task<(Guid GameId, Guid PdfId)> SeedGameWithPdfAsync(
        string processingPriority = "Normal",
        Guid? sharedGameId = null)
    {
        await EnsureUserSeededAsync();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext!.Games.Add(new GameEntity
        {
            Id = gameId,
            Name = "Gloomhaven",
            SharedGameId = sharedGameId,
        });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "gloomhaven.pdf",
            FilePath = "/uploads/gloomhaven.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = processingPriority,
        });
        await _dbContext.SaveChangesAsync();
        return (gameId, pdfId);
    }

    private LaunchAdminPdfProcessingCommandHandler CreateHandler(Mock<IMediator> mediator)
    {
        return new LaunchAdminPdfProcessingCommandHandler(
            _dbContext!,
            mediator.Object,
            new Mock<ILogger<LaunchAdminPdfProcessingCommandHandler>>().Object);
    }

    private static Mock<IMediator> CreateSuccessfulMediatorMock()
    {
        var mock = new Mock<IMediator>();
        mock.Setup(m => m.Send(It.IsAny<ExtractPdfTextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExtractPdfTextResultDto.CreateSuccess(1000, 5));
        mock.Setup(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexingResultDto.CreateSuccess("vec-doc-1", 10, DateTime.UtcNow));
        return mock;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Happy path: priority persists to real PostgreSQL
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_AdminLaunch_SetsPriorityAdminInRealPostgresDatabase()
    {
        // Arrange
        var (gameId, pdfId) = await SeedGameWithPdfAsync("Normal");

        var mediator = CreateSuccessfulMediatorMock();
        var handler = CreateHandler(mediator);

        var command = new LaunchAdminPdfProcessingCommand(gameId, pdfId, UserId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: result DTO
        result.Status.Should().Be("processing");
        result.Priority.Should().Be("Admin");

        // Assert: DB state is persisted (re-read from DB, not tracked entity)
        _dbContext!.ChangeTracker.Clear();
        var pdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        pdf!.ProcessingPriority.Should().Be("Admin", "priority must be durably saved in PostgreSQL");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_AdminLaunch_OnlyTargetPdfPriorityChanged_OtherPdfsUnaffected()
    {
        // Arrange: two PDFs for the same game
        await EnsureUserSeededAsync();
        var gameId = Guid.NewGuid();
        var targetPdfId = Guid.NewGuid();
        var otherPdfId = Guid.NewGuid();

        _dbContext!.Games.Add(new GameEntity { Id = gameId, Name = "Pandemic" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = targetPdfId,
            GameId = gameId,
            FileName = "pandemic.pdf",
            FilePath = "/uploads/pandemic.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = "Normal",
        });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = otherPdfId,
            GameId = gameId,
            FileName = "pandemic-expansion.pdf",
            FilePath = "/uploads/pandemic-expansion.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = "Normal",
        });
        await _dbContext.SaveChangesAsync();

        var mediator = CreateSuccessfulMediatorMock();
        var handler = CreateHandler(mediator);
        var command = new LaunchAdminPdfProcessingCommand(gameId, targetPdfId, UserId);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert: only target PDF has Admin priority
        _dbContext.ChangeTracker.Clear();
        var targetPdf = await _dbContext.PdfDocuments.FindAsync([targetPdfId], TestCancellationToken);
        var otherPdf = await _dbContext.PdfDocuments.FindAsync([otherPdfId], TestCancellationToken);

        targetPdf!.ProcessingPriority.Should().Be("Admin");
        otherPdf!.ProcessingPriority.Should().Be("Normal", "unrelated PDFs must not be affected");
    }

    // ────────────────────────────────────────────────────────────────────────
    // SharedGameId resolution: wizard passes SharedGameId → resolves to Game.Id
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_WithSharedGameId_ResolvesAndSetsPriorityInRealDatabase()
    {
        // Arrange: seed SharedGameEntity first (FK required), then game linked to it
        var sharedGameId = Guid.NewGuid();
        await EnsureUserSeededAsync();
        _dbContext!.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Gloomhaven",
            YearPublished = 2017,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 120,
            MinAge = 12,
            CreatedBy = UserId,
        });
        await _dbContext.SaveChangesAsync();

        var (_, pdfId) = await SeedGameWithPdfAsync("Normal", sharedGameId);

        var mediator = CreateSuccessfulMediatorMock();
        var handler = CreateHandler(mediator);

        // Command uses SharedGameId (as the admin wizard does)
        var command = new LaunchAdminPdfProcessingCommand(sharedGameId, pdfId, UserId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: SharedGameId was resolved and priority was set
        result.Status.Should().Be("processing");

        _dbContext!.ChangeTracker.Clear();
        var pdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        pdf!.ProcessingPriority.Should().Be("Admin", "handler must resolve SharedGameId and persist priority");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: PDF belongs to different game → IDOR protection
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_WhenPdfBelongsToDifferentGame_ThrowsNotFoundExceptionInRealDatabase()
    {
        // Arrange: two separate games, PDF belongs to gameB
        await EnsureUserSeededAsync();
        var gameAId = Guid.NewGuid();
        var gameBId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext!.Games.Add(new GameEntity { Id = gameAId, Name = "Game A" });
        _dbContext.Games.Add(new GameEntity { Id = gameBId, Name = "Game B" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameBId,
            FileName = "game-b.pdf",
            FilePath = "/uploads/game-b.pdf",
            UploadedByUserId = UserId,
            ProcessingPriority = "Normal",
        });
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler(new Mock<IMediator>());
        var command = new LaunchAdminPdfProcessingCommand(gameAId, pdfId, UserId);

        // Act & Assert: IDOR protection via NotFoundException
        await handler.Invoking(h => h.Handle(command, TestCancellationToken))
            .Should().ThrowAsync<NotFoundException>();

        // Assert: PDF priority was NOT modified
        _dbContext.ChangeTracker.Clear();
        var pdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        pdf!.ProcessingPriority.Should().Be("Normal", "IDOR guard must prevent priority modification");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: PDF does not exist
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_WhenPdfNotFound_ThrowsNotFoundException()
    {
        // Arrange: game exists but PDF does not
        var gameId = Guid.NewGuid();
        _dbContext!.Games.Add(new GameEntity { Id = gameId, Name = "Catan" });
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler(new Mock<IMediator>());
        var command = new LaunchAdminPdfProcessingCommand(gameId, Guid.NewGuid(), UserId);

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, TestCancellationToken))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Priority idempotency: re-launching admin PDF keeps Admin priority
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_WhenAlreadyAdminPriority_RemainsAdminAfterRelaunch()
    {
        // Arrange: PDF already has Admin priority (re-launch scenario)
        var (gameId, pdfId) = await SeedGameWithPdfAsync("Admin");

        var mediator = CreateSuccessfulMediatorMock();
        var handler = CreateHandler(mediator);
        var command = new LaunchAdminPdfProcessingCommand(gameId, pdfId, UserId);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert: still Admin after re-launch
        _dbContext!.ChangeTracker.Clear();
        var pdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        pdf!.ProcessingPriority.Should().Be("Admin");
    }
}
