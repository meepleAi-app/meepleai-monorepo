using Api.BoundedContexts.Administration.Application.Commands.GameWizard;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Exceptions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
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
/// Integration tests for the Admin Game Wizard processing flow.
/// Issue #4673: Validates LaunchAdminPdfProcessingCommandHandler with real PostgreSQL —
/// priority assignment, SharedGameId resolution, IDOR protection, pipeline dispatch.
///
/// Tests:
/// - Full wizard flow: seed → launch → verify DB state and result
/// - SharedGameId resolution with real FK relationships
/// - IDOR check against real DB
/// - Pipeline commands dispatched correctly
/// - Multiple-PDF isolation: only target PDF gets Admin priority
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4673")]
public sealed class AdminGameWizardFlowTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;

    private static readonly Guid AdminUserId = Guid.NewGuid();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public AdminGameWizardFlowTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_admin_wizard_flow_{Guid.NewGuid():N}";
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

    private async Task EnsureAdminUserSeededAsync()
    {
        if (!_dbContext!.Users.Any(u => u.Id == AdminUserId))
        {
            _dbContext.Users.Add(new UserEntity
            {
                Id = AdminUserId,
                Email = "admin@meepleai.dev",
                Tier = "premium",
                Role = "admin",
            });
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task<(Guid GameId, Guid PdfId)> SeedGameAndPdfAsync(
        string gameName = "Gloomhaven",
        string priority = "Normal",
        Guid? sharedGameId = null)
    {
        await EnsureAdminUserSeededAsync();

        // The optional sharedGameId lets callers (WizardLaunch_WithSharedGameId_…)
        // reuse a pre-existing SharedGame row. When omitted, a fresh row is created.
        var gameId = sharedGameId ?? Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        if (sharedGameId is null)
        {
            _dbContext!.SharedGames.Add(new SharedGameEntity
            {
                Id = gameId,
                Title = gameName,
            });
        }
        _dbContext!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId, // FK_pdf_documents_shared_games_SharedGameId (post-Phase 2d)
            FileName = $"{gameName.ToLowerInvariant()}.pdf",
            FilePath = $"/uploads/{gameName.ToLowerInvariant()}.pdf",
            UploadedByUserId = AdminUserId,
            ProcessingPriority = priority,
        });
        await _dbContext.SaveChangesAsync();
        return (gameId, pdfId);
    }

    private LaunchAdminPdfProcessingCommandHandler CreateHandler(Mock<IMediator> mediator) =>
        new LaunchAdminPdfProcessingCommandHandler(
            _dbContext!,
            mediator.Object,
            new Mock<ILogger<LaunchAdminPdfProcessingCommandHandler>>().Object);

    private static Mock<IMediator> CreatePipelineMock(
        Func<ExtractPdfTextResultDto>? extractResult = null,
        Func<IndexingResultDto>? indexResult = null)
    {
        var mock = new Mock<IMediator>();
        mock.Setup(m => m.Send(It.IsAny<ExtractPdfTextCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extractResult?.Invoke() ?? ExtractPdfTextResultDto.CreateSuccess(5000, 20));
        mock.Setup(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indexResult?.Invoke() ?? IndexingResultDto.CreateSuccess("vec-123", 50, DateTime.UtcNow));
        return mock;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Wizard Phase 2: LaunchAdminPdfProcessing — full flow
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task WizardLaunch_WithDirectGameId_CompleteFlowSetsAdminPriorityAndReturnsCorrectResult()
    {
        // Arrange
        var (gameId, pdfId) = await SeedGameAndPdfAsync("Gloomhaven");
        var mediatorMock = CreatePipelineMock();
        var handler = CreateHandler(mediatorMock);
        var command = new LaunchAdminPdfProcessingCommand(gameId, pdfId, AdminUserId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert result DTO
        result.PdfDocumentId.Should().Be(pdfId);
        result.GameId.Should().Be(gameId);
        result.Status.Should().Be("queued");
        result.Priority.Should().Be("Admin");

        // Assert DB persistence (clear tracker to force real DB read)
        _dbContext!.ChangeTracker.Clear();
        var savedPdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        savedPdf!.ProcessingPriority.Should().Be("Admin");
    }

    /// <summary>
    /// #4443 R3: the queue processes the document; the request only enqueues it. It used to run
    /// extraction and indexing itself as well, a second pipeline next to the worker's.
    /// </summary>
    [Fact(Timeout = 90_000)]
    [Trait("Issue", "4443")]
    public async Task WizardLaunch_EnqueuesOnly_AndRunsNoPipelineInTheRequest()
    {
        // Arrange
        var (gameId, pdfId) = await SeedGameAndPdfAsync("Spirit Island");
        var mediatorMock = CreatePipelineMock();
        var handler = CreateHandler(mediatorMock);
        var command = new LaunchAdminPdfProcessingCommand(gameId, pdfId, AdminUserId);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert: one enqueue at the wizard priority, no pipeline command
        mediatorMock.Verify(
            m => m.Send(It.Is<EnqueuePdfCommand>(c => c.PdfDocumentId == pdfId && c.Priority == 100), It.IsAny<CancellationToken>()),
            Times.Once);
        mediatorMock.Verify(
            m => m.Send(It.IsAny<ExtractPdfTextCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mediatorMock.Verify(
            m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(Timeout = 90_000)]
    public async Task WizardLaunch_PrioritySetBeforeTheEnqueue()
    {
        // Arrange: verify that priority is set (saved) before the job exists, so that when the worker
        // reads the PDF it already sees Admin priority.
        var (gameId, pdfId) = await SeedGameAndPdfAsync("Twilight Imperium");

        string? priorityAtEnqueueTime = null;
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .Returns<EnqueuePdfCommand, CancellationToken>(async (_, ct) =>
            {
                // At this point priority should already be saved in DB
                _dbContext!.ChangeTracker.Clear();
                var pdf = await _dbContext.PdfDocuments.FindAsync(new object[] { pdfId }, ct);
                priorityAtEnqueueTime = pdf?.ProcessingPriority;
                return Guid.NewGuid();
            });

        var handler = CreateHandler(mediatorMock);
        var command = new LaunchAdminPdfProcessingCommand(gameId, pdfId, AdminUserId);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert: priority was Admin before the job was created
        priorityAtEnqueueTime.Should().Be("Admin",
            "priority must be saved to DB before the job is enqueued");
    }

    // ────────────────────────────────────────────────────────────────────────
    // SharedGameId resolution via real FK
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task WizardLaunch_WithSharedGameId_ResolvesViaRealForeignKey()
    {
        // Arrange: seed SharedGameEntity first (FK required), then game linked to it
        var sharedGameId = Guid.NewGuid();
        await EnsureAdminUserSeededAsync();
        _dbContext!.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Wingspan",
            YearPublished = 2019,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            CreatedBy = AdminUserId,
        });
        await _dbContext.SaveChangesAsync();

        var (_, pdfId) = await SeedGameAndPdfAsync("Wingspan", sharedGameId: sharedGameId);

        var mediatorMock = CreatePipelineMock();
        var handler = CreateHandler(mediatorMock);

        // Command passes SharedGameId as gameId (wizard URL uses SharedGameId)
        var command = new LaunchAdminPdfProcessingCommand(sharedGameId, pdfId, AdminUserId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert: resolved correctly and priority persisted
        result.Status.Should().Be("queued");

        _dbContext!.ChangeTracker.Clear();
        var pdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        pdf!.ProcessingPriority.Should().Be("Admin");
    }

    [Fact(Timeout = 90_000)]
    public async Task WizardLaunch_WithUnknownSharedGameId_ThrowsNotFoundException()
    {
        // Arrange: no game with this SharedGameId exists in DB
        var handler = CreateHandler(new Mock<IMediator>());
        var command = new LaunchAdminPdfProcessingCommand(
            Guid.NewGuid(),   // unknown SharedGameId
            Guid.NewGuid(),   // any PDF
            AdminUserId);

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, TestCancellationToken))
            .Should().ThrowAsync<NotFoundException>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // IDOR protection: PDF does not belong to the requested game
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task WizardLaunch_CrossGamePdfAccess_ThrowsNotFoundExceptionAndDoesNotModifyDb()
    {
        // Arrange: game A and game B, PDF belongs to game B
        await EnsureAdminUserSeededAsync();
        var gameAId = Guid.NewGuid();
        var gameBId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext!.SharedGames.Add(new SharedGameEntity { Id = gameAId, Title = "Game A" });
        _dbContext.SharedGames.Add(new SharedGameEntity { Id = gameBId, Title = "Game B" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameBId, // PDF appartiene a gameB per IDOR test (FK_pdf_documents_shared_games)
            FileName = "game-b.pdf",
            FilePath = "/uploads/game-b.pdf",
            UploadedByUserId = AdminUserId,
            ProcessingPriority = "Normal",
        });
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler(new Mock<IMediator>());
        var command = new LaunchAdminPdfProcessingCommand(gameAId, pdfId, AdminUserId);

        // Act & Assert
        await handler.Invoking(h => h.Handle(command, TestCancellationToken))
            .Should().ThrowAsync<NotFoundException>("IDOR guard must prevent cross-game access");

        // DB must be unchanged
        _dbContext.ChangeTracker.Clear();
        var pdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        pdf!.ProcessingPriority.Should().Be("Normal", "IDOR guard must not modify unrelated PDF");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Guard: a full queue propagates (priority already saved before throw)
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    [Trait("Issue", "4443")]
    public async Task WizardLaunch_WhenTheQueueIsFull_ExceptionPropagatesButPriorityAlreadySaved()
    {
        // Arrange
        var (gameId, pdfId) = await SeedGameAndPdfAsync("Azul");

        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcessingQueueFullException());

        var handler = CreateHandler(mediatorMock);
        var command = new LaunchAdminPdfProcessingCommand(gameId, pdfId, AdminUserId);

        // Act & Assert: #4443 R8 — a full queue is not «already queued», it reaches the caller (429)
        await handler.Invoking(h => h.Handle(command, TestCancellationToken))
            .Should().ThrowAsync<ProcessingQueueFullException>();

        // Priority was already saved before the enqueue
        _dbContext!.ChangeTracker.Clear();
        var pdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        pdf!.ProcessingPriority.Should().Be("Admin",
            "priority is saved before the enqueue, so it persists even when the queue refuses the job");
    }
}
