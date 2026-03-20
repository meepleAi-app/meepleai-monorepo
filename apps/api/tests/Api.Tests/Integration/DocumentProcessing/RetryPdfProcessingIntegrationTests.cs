using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for PDF retry mechanism (Issue #4216).
/// Tests the complete retry workflow from command to database persistence.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Issue", "4216")]
[Trait("Category", TestCategories.Integration)]
public sealed class RetryPdfProcessingIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public RetryPdfProcessingIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_retry_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain event infrastructure
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register IProcessingMetricsService (required by PdfStateChangedMetricsEventHandler picked up by MediatR assembly scan)
        var mockMetricsService = new Moq.Mock<Api.BoundedContexts.DocumentProcessing.Application.Services.IProcessingMetricsService>();
        services.AddScoped(_ => mockMetricsService.Object);

        // Register UserNotifications dependencies (required by PdfNotificationEventHandler picked up by MediatR assembly scan)
        var mockNotifPrefsRepo = new Moq.Mock<Api.BoundedContexts.UserNotifications.Domain.Repositories.INotificationPreferencesRepository>();
        services.AddScoped(_ => mockNotifPrefsRepo.Object);
        var mockNotifRepo = new Moq.Mock<Api.BoundedContexts.UserNotifications.Domain.Repositories.INotificationRepository>();
        services.AddScoped(_ => mockNotifRepo.Object);
        var mockPushService = new Moq.Mock<Api.Services.IPushNotificationService>();
        services.AddScoped(_ => mockPushService.Object);
        var mockEmailQueueRepo = new Moq.Mock<Api.BoundedContexts.UserNotifications.Domain.Repositories.IEmailQueueRepository>();
        services.AddScoped(_ => mockEmailQueueRepo.Object);
        var mockEmailTemplateService = new Moq.Mock<Api.BoundedContexts.UserNotifications.Application.Services.IEmailTemplateService>();
        services.AddSingleton(_ => mockEmailTemplateService.Object);

        // Register KnowledgeBase dependencies (required by AutoCreateAgentOnPdfReadyHandler picked up by MediatR assembly scan)
        var mockTypologyRepo = new Moq.Mock<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IAgentTypologyRepository>();
        services.AddScoped(_ => mockTypologyRepo.Object);

        // Register MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(RetryPdfProcessingCommand).Assembly);
        });

        // Register handler explicitly
        services.AddScoped<RetryPdfProcessingCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Use MigrateAsync instead of EnsureCreatedAsync to avoid DDL generation issues
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task SeedRequiredDataAsync(Guid userId, Guid gameId)
    {
        // Add user (required FK)
        var user = new UserEntity
        {
            Id = userId,
            Email = $"test-{userId:N}@example.com",
            DisplayName = "Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        // Add game (required FK)
        var game = new GameEntity
        {
            Id = gameId,
            Name = "Test Game",
            BggId = Guid.NewGuid().GetHashCode() & 0x7FFFFFFF, // Positive int
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 30,
            MaxPlayTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    [Fact]
    public async Task RetryPdfProcessing_WithFailedPdf_SuccessfullyRetries()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        await SeedRequiredDataAsync(userId, gameId);

        var failedPdf = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "test.pdf",
            FilePath = "/test/path.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            ProcessingState = PdfProcessingState.Failed.ToString(),
            ProcessingError = "Network timeout",
            ErrorCategory = ErrorCategory.Network.ToString(),
            FailedAtState = PdfProcessingState.Extracting.ToString(),
            RetryCount = 0,
            Language = "en"
        };

        _dbContext!.PdfDocuments.Add(failedPdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var command = new RetryPdfProcessingCommand(pdfId, userId);
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.Message}");
        Assert.Equal(1, result.RetryCount);
        Assert.Equal(PdfProcessingState.Extracting.ToString(), result.CurrentState);

        // Verify database was updated
        var updatedPdf = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == pdfId, TestCancellationToken);

        Assert.NotNull(updatedPdf);
        Assert.Equal(1, updatedPdf.RetryCount);
        Assert.Equal(PdfProcessingState.Extracting.ToString(), updatedPdf.ProcessingState);
        Assert.Null(updatedPdf.ProcessingError);
    }

    [Fact]
    public async Task RetryPdfProcessing_WhenAtMaxRetries_ReturnsFailure()
    {
        // Arrange - Create a PDF that has exhausted retries
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        await SeedRequiredDataAsync(userId, gameId);

        var exhaustedPdf = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "test.pdf",
            FilePath = "/test/path.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            ProcessingState = PdfProcessingState.Failed.ToString(),
            ProcessingError = "Final error",
            ErrorCategory = ErrorCategory.Service.ToString(),
            FailedAtState = PdfProcessingState.Indexing.ToString(),
            RetryCount = 3, // Max retries reached
            Language = "en"
        };

        _dbContext!.PdfDocuments.Add(exhaustedPdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var command = new RetryPdfProcessingCommand(pdfId, userId);
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Cannot retry", result.Message); // Domain exception message
        Assert.Contains("MaxRetries=3", result.Message);
        Assert.Equal(3, result.RetryCount);

        // Verify database was NOT modified
        var unchangedPdf = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == pdfId, TestCancellationToken);

        Assert.NotNull(unchangedPdf);
        Assert.Equal(3, unchangedPdf.RetryCount);
        Assert.Equal(PdfProcessingState.Failed.ToString(), unchangedPdf.ProcessingState);
    }

    [Fact]
    public async Task RetryPdfProcessing_WhenNotFailed_ReturnsFailure()
    {
        // Arrange - Create a PDF that is processing
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        await SeedRequiredDataAsync(userId, gameId);

        var processingPdf = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "test.pdf",
            FilePath = "/test/path.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            ProcessingState = PdfProcessingState.Extracting.ToString(),
            RetryCount = 0,
            Language = "en"
        };

        _dbContext!.PdfDocuments.Add(processingPdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var command = new RetryPdfProcessingCommand(pdfId, userId);
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Cannot retry", result.Message); // Domain exception message
        Assert.Equal(0, result.RetryCount);
    }

    [Fact]
    public async Task RetryPdfProcessing_WithUnauthorizedUser_ReturnsFailure()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        await SeedRequiredDataAsync(ownerId, gameId);

        // Add other user (no game needed for them)
        var otherUser = new UserEntity
        {
            Id = otherUserId,
            Email = $"other-{otherUserId:N}@example.com",
            DisplayName = "Other User",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(otherUser);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var failedPdf = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "test.pdf",
            FilePath = "/test/path.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = ownerId, // Different owner
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            ProcessingState = PdfProcessingState.Failed.ToString(),
            ProcessingError = "Error",
            RetryCount = 0,
            Language = "en"
        };

        _dbContext!.PdfDocuments.Add(failedPdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act & Assert
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var command = new RetryPdfProcessingCommand(pdfId, otherUserId);
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => mediator.Send(command, TestCancellationToken));
        Assert.Contains("not authorized", ex.Message);
    }

    [Fact]
    public async Task RetryPdfProcessing_WithNonExistentPdf_ReturnsNotFound()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act & Assert
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var command = new RetryPdfProcessingCommand(pdfId, userId);
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => mediator.Send(command, TestCancellationToken));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RetryPdfProcessing_MultipleRetries_IncrementsCountCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        await SeedRequiredDataAsync(userId, gameId);

        var failedPdf = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = gameId,
            FileName = "test.pdf",
            FilePath = "/test/path.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            ProcessingState = PdfProcessingState.Failed.ToString(),
            ProcessingError = "Error 1",
            ErrorCategory = ErrorCategory.Network.ToString(),
            FailedAtState = PdfProcessingState.Extracting.ToString(),
            RetryCount = 0,
            Language = "en"
        };

        _dbContext!.PdfDocuments.Add(failedPdf);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Act - First retry
        var command1 = new RetryPdfProcessingCommand(pdfId, userId);
        var result1 = await mediator.Send(command1, TestCancellationToken);

        // Simulate another failure
        var pdf = await _dbContext.PdfDocuments.FindAsync(new object[] { pdfId }, TestCancellationToken);
        pdf!.ProcessingState = PdfProcessingState.Failed.ToString();
        pdf.ProcessingError = "Error 2";
        pdf.ErrorCategory = ErrorCategory.Parsing.ToString();
        pdf.FailedAtState = PdfProcessingState.Chunking.ToString();
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Second retry
        var command2 = new RetryPdfProcessingCommand(pdfId, userId);
        var result2 = await mediator.Send(command2, TestCancellationToken);

        // Assert
        Assert.True(result1.Success);
        Assert.Equal(1, result1.RetryCount);

        Assert.True(result2.Success);
        Assert.Equal(2, result2.RetryCount);

        // Verify final state
        var finalPdf = await _dbContext.PdfDocuments.FindAsync(new object[] { pdfId }, TestCancellationToken);
        Assert.Equal(2, finalPdf!.RetryCount);
        Assert.Equal(PdfProcessingState.Chunking.ToString(), finalPdf.ProcessingState);
    }
}
