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
using FluentAssertions;

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

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();

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
        result.Success.Should().BeTrue($"Expected success but got: {result.Message}");
        result.RetryCount.Should().Be(1);
        result.CurrentState.Should().Be(PdfProcessingState.Extracting.ToString());

        // Verify database was updated
        var updatedPdf = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == pdfId, TestCancellationToken);

        updatedPdf.Should().NotBeNull();
        updatedPdf.RetryCount.Should().Be(1);
        updatedPdf.ProcessingState.Should().Be(PdfProcessingState.Extracting.ToString());
        updatedPdf.ProcessingError.Should().BeNull();
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
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot retry"); // Domain exception message
        result.Message.Should().Contain("MaxRetries=3");
        result.RetryCount.Should().Be(3);

        // Verify database was NOT modified
        var unchangedPdf = await _dbContext.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == pdfId, TestCancellationToken);

        unchangedPdf.Should().NotBeNull();
        unchangedPdf.RetryCount.Should().Be(3);
        unchangedPdf.ProcessingState.Should().Be(PdfProcessingState.Failed.ToString());
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
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot retry"); // Domain exception message
        result.RetryCount.Should().Be(0);
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
        var act = () => mediator.Send(command, TestCancellationToken);
        var ex = (await act.Should().ThrowAsync<ForbiddenException>()).Which;
        ex.Message.Should().Contain("not authorized");
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
        var act2 = () => mediator.Send(command, TestCancellationToken);
        var ex = (await act2.Should().ThrowAsync<NotFoundException>()).Which;
        ex.Message.Should().ContainEquivalentOf("not found");
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
        result1.Success.Should().BeTrue();
        result1.RetryCount.Should().Be(1);

        result2.Success.Should().BeTrue();
        result2.RetryCount.Should().Be(2);

        // Verify final state
        var finalPdf = await _dbContext.PdfDocuments.FindAsync(new object[] { pdfId }, TestCancellationToken);
        finalPdf!.RetryCount.Should().Be(2);
        finalPdf.ProcessingState.Should().Be(PdfProcessingState.Chunking.ToString());
    }
}
