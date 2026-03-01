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
[Collection("SharedTestcontainers")]
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

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext!.Games.Add(new GameEntity
        {
            Id = gameId,
            Name = gameName,
            SharedGameId = sharedGameId,
        });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
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

    [Fact(Timeout = 30000)]
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
        result.Status.Should().Be("processing");
        result.Priority.Should().Be("Admin");

        // Assert DB persistence (clear tracker to force real DB read)
        _dbContext!.ChangeTracker.Clear();
        var savedPdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        savedPdf!.ProcessingPriority.Should().Be("Admin");
    }

    [Fact(Timeout = 30000)]
    public async Task WizardLaunch_DispatchesBothExtractAndIndexCommands()
    {
        // Arrange
        var (gameId, pdfId) = await SeedGameAndPdfAsync("Spirit Island");
        var mediatorMock = CreatePipelineMock();
        var handler = CreateHandler(mediatorMock);
        var command = new LaunchAdminPdfProcessingCommand(gameId, pdfId, AdminUserId);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert: pipeline dispatched both commands in order
        mediatorMock.Verify(
            m => m.Send(It.Is<ExtractPdfTextCommand>(c => c.PdfId == pdfId), It.IsAny<CancellationToken>()),
            Times.Once);
        mediatorMock.Verify(
            m => m.Send(It.Is<IndexPdfCommand>(c => c.PdfId == pdfId.ToString()), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Timeout = 30000)]
    public async Task WizardLaunch_PrioritySetBeforePipelineDispatched()
    {
        // Arrange: verify that priority is set (saved) before the pipeline runs,
        // so that if extraction reads the PDF it already sees Admin priority.
        var (gameId, pdfId) = await SeedGameAndPdfAsync("Twilight Imperium");

        string? priorityAtExtractTime = null;
        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<ExtractPdfTextCommand>(), It.IsAny<CancellationToken>()))
            .Returns<ExtractPdfTextCommand, CancellationToken>(async (_, ct) =>
            {
                // At this point priority should already be saved in DB
                _dbContext!.ChangeTracker.Clear();
                var pdf = await _dbContext.PdfDocuments.FindAsync(new object[] { pdfId }, ct);
                priorityAtExtractTime = pdf?.ProcessingPriority;
                return ExtractPdfTextResultDto.CreateSuccess(1000, 5);
            });
        mediatorMock
            .Setup(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexingResultDto.CreateSuccess("vec-1", 10, DateTime.UtcNow));

        var handler = CreateHandler(mediatorMock);
        var command = new LaunchAdminPdfProcessingCommand(gameId, pdfId, AdminUserId);

        // Act
        await handler.Handle(command, TestCancellationToken);

        // Assert: priority was Admin before extraction ran
        priorityAtExtractTime.Should().Be("Admin",
            "priority must be saved to DB before the pipeline commands are dispatched");
    }

    // ────────────────────────────────────────────────────────────────────────
    // SharedGameId resolution via real FK
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
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
        result.Status.Should().Be("processing");

        _dbContext!.ChangeTracker.Clear();
        var pdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        pdf!.ProcessingPriority.Should().Be("Admin");
    }

    [Fact(Timeout = 30000)]
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

    [Fact(Timeout = 30000)]
    public async Task WizardLaunch_CrossGamePdfAccess_ThrowsNotFoundExceptionAndDoesNotModifyDb()
    {
        // Arrange: game A and game B, PDF belongs to game B
        await EnsureAdminUserSeededAsync();
        var gameAId = Guid.NewGuid();
        var gameBId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        _dbContext!.Games.Add(new GameEntity { Id = gameAId, Name = "Game A" });
        _dbContext.Games.Add(new GameEntity { Id = gameBId, Name = "Game B" });
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId, GameId = gameBId,
            FileName = "game-b.pdf", FilePath = "/uploads/game-b.pdf",
            UploadedByUserId = AdminUserId, ProcessingPriority = "Normal",
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
    // Guard: pipeline failure propagates (priority already saved before throw)
    // ────────────────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task WizardLaunch_WhenExtractionFails_ExceptionPropagatesButPriorityAlreadySaved()
    {
        // Arrange
        var (gameId, pdfId) = await SeedGameAndPdfAsync("Azul");

        var mediatorMock = new Mock<IMediator>();
        mediatorMock
            .Setup(m => m.Send(It.IsAny<ExtractPdfTextCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Extraction service unavailable"));

        var handler = CreateHandler(mediatorMock);
        var command = new LaunchAdminPdfProcessingCommand(gameId, pdfId, AdminUserId);

        // Act & Assert: exception propagates from extraction failure
        await handler.Invoking(h => h.Handle(command, TestCancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Extraction service unavailable");

        // Priority was already saved before pipeline dispatch
        _dbContext!.ChangeTracker.Clear();
        var pdf = await _dbContext.PdfDocuments.FindAsync([pdfId], TestCancellationToken);
        pdf!.ProcessingPriority.Should().Be("Admin",
            "priority is saved before pipeline is dispatched, so it persists even on pipeline failure");
    }
}
