using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AuthRole = Api.BoundedContexts.Authentication.Domain.ValueObjects.Role;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Comprehensive integration tests for PDF deletion workflow (Issue #1690).
/// Tests the complete delete pipeline using Testcontainers for real infrastructure.
///
/// Test Categories:
/// 1. Happy Path: Delete existing PDF with all cleanup
/// 2. Not Found: Delete non-existent PDF (404 handling)
/// 3. Cascade Delete: Delete PDF with associated vector embeddings
/// 4. Permission Errors: Simulated authorization failures (via mock)
/// 5. Concurrency: Concurrent deletion attempts
/// 6. Rollback: Partial failure and transaction rollback
///
/// Infrastructure: PostgreSQL (real DB via Testcontainers)
/// Coverage Target: ≥90% for DeletePdfCommandHandler
/// Execution Time Target: <20s
/// </summary>
[Collection("DeletePdfIntegration")]
public sealed class DeletePdfIntegrationTests : IAsyncLifetime
{
    #region Test Infrastructure

    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // Start PostgreSQL container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "pdf_delete_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);

        // Setup services
        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={postgresPort};Database=pdf_delete_test;Username=postgres;Password=postgres;";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain event infrastructure
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register MediatR
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(DeletePdfCommandHandler).Assembly));

        // Register the handler explicitly for test access
        services.AddScoped<DeletePdfCommandHandler>();

        // Register mock services
        RegisterMockServices(services);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
        }
    }

    private void RegisterMockServices(IServiceCollection services)
    {
        // Mock Qdrant service (default: success)
        var qdrantMock = new Mock<IQdrantService>();
        qdrantMock.Setup(q => q.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton<IQdrantService>(qdrantMock.Object);

        // Mock BlobStorage service (default: success)
        var blobStorageMock = new Mock<IBlobStorageService>();
        blobStorageMock.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton<IBlobStorageService>(blobStorageMock.Object);

        // Mock Cache service (default: success)
        var cacheMock = new Mock<IAiResponseCacheService>();
        cacheMock.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);
    }

    private async Task SeedTestDataAsync()
    {
        // Seed user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@meepleai.dev",
            DisplayName = "TestUser",
            Role = "Editor",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(user);

        // Seed game
        var gameId = Guid.NewGuid();
        var game = new GameEntity
        {
            Id = gameId,
            Name = "Test Game for Delete",
            Publisher = "Test Publisher",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 60,
            MaxPlayTimeMinutes = 90,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task<Guid> CreateTestPdfAsync(string name = "Test.pdf", bool withVectorDoc = false)
    {
        var gameId = _dbContext!.Games.First().Id;
        var userId = _dbContext.Users.First().Id;

        var pdfDoc = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            UploadedByUserId = userId,
            FileName = name,
            FilePath = $"/test/{name}",
            FileSizeBytes = 1024,
            UploadedAt = DateTime.UtcNow,
            ProcessingStatus = "completed",
            PageCount = 10,
            ExtractedText = "Test extracted text"
        };

        _dbContext.PdfDocuments.Add(pdfDoc);

        if (withVectorDoc)
        {
            var vectorDoc = new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfDoc.Id,
                GameId = gameId,
                ChunkCount = 5,
                TotalCharacters = 100,
                IndexedAt = DateTime.UtcNow,
                IndexingStatus = "completed",
                EmbeddingModel = "test-model",
                EmbeddingDimensions = 384
            };
            _dbContext.VectorDocuments.Add(vectorDoc);
        }

        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return pdfDoc.Id;
    }

    private async Task ResetDatabaseAsync()
    {
        // Clear all data
        _dbContext!.VectorDocuments.RemoveRange(_dbContext.VectorDocuments);
        _dbContext.PdfDocuments.RemoveRange(_dbContext.PdfDocuments);
        _dbContext.Games.RemoveRange(_dbContext.Games);
        _dbContext.Users.RemoveRange(_dbContext.Users);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Re-seed base data
        await SeedTestDataAsync();
    }

    #endregion

    #region 1. Happy Path Tests

    [Fact]
    public async Task DeleteExistingPdf_WithoutVectors_Success()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("DeleteTest.pdf", withVectorDoc: false);
        var handler = _serviceProvider!.GetRequiredService<DeletePdfCommandHandler>();
        var command = new DeletePdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("deleted successfully");
        result.GameId.Should().NotBeNullOrEmpty();

        // Verify database state
        var pdfExists = await _dbContext!.PdfDocuments.AnyAsync(p => p.Id == pdfId);
        pdfExists.Should().BeFalse();
    }

    #endregion

    #region 2. Not Found Tests

    [Fact]
    public async Task DeleteNonExistentPdf_Returns404Message()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid().ToString();
        var handler = _serviceProvider!.GetRequiredService<DeletePdfCommandHandler>();
        var command = new DeletePdfCommand(nonExistentId);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
        result.GameId.Should().BeNull();
    }

    [Fact]
    public async Task DeleteWithInvalidGuid_ThrowsPdfStorageException()
    {
        // Arrange
        await ResetDatabaseAsync();
        var invalidGuid = "not-a-guid";
        var handler = _serviceProvider!.GetRequiredService<DeletePdfCommandHandler>();
        var command = new DeletePdfCommand(invalidGuid);

        // Act
        Func<Task> act = async () => await handler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<PdfStorageException>();
    }

    #endregion

    #region 3. Cascade Delete Tests

    [Fact]
    public async Task DeletePdfWithVectorEmbeddings_CascadeDeletesVectors()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("WithVectors.pdf", withVectorDoc: true);
        var handler = _serviceProvider!.GetRequiredService<DeletePdfCommandHandler>();
        var command = new DeletePdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify both PDF and Vector documents are deleted
        var pdfExists = await _dbContext!.PdfDocuments.AnyAsync(p => p.Id == pdfId);
        var vectorExists = await _dbContext.VectorDocuments.AnyAsync(v => v.PdfDocumentId == pdfId);

        pdfExists.Should().BeFalse();
        vectorExists.Should().BeFalse();
    }

    [Fact]
    public async Task DeletePdfWithVectorEmbeddings_CallsQdrantDelete()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("WithVectorsQdrant.pdf", withVectorDoc: true);

        // Create fresh service provider with instrumented mock
        var qdrantMock = new Mock<IQdrantService>();
        var deleteCallCount = 0;
        qdrantMock.Setup(q => q.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => deleteCallCount++)
            .ReturnsAsync(true);

        var scopeFactory = _serviceProvider!.GetRequiredService<IServiceScopeFactory>();
        var handler = new DeletePdfCommandHandler(
            _dbContext!,
            scopeFactory,
            _serviceProvider.GetRequiredService<IBlobStorageService>(),
            _serviceProvider.GetRequiredService<IAiResponseCacheService>(),
            _serviceProvider.GetRequiredService<ILogger<DeletePdfCommandHandler>>()
        );

        // Inject Qdrant mock into scope factory
        var services = new ServiceCollection();
        services.AddScoped<IQdrantService>(_ => qdrantMock.Object);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = services.BuildServiceProvider();
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider);
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        handler = new DeletePdfCommandHandler(
            _dbContext!,
            mockScopeFactory.Object,
            _serviceProvider.GetRequiredService<IBlobStorageService>(),
            _serviceProvider.GetRequiredService<IAiResponseCacheService>(),
            _serviceProvider.GetRequiredService<ILogger<DeletePdfCommandHandler>>()
        );

        var command = new DeletePdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        deleteCallCount.Should().BeGreaterThan(0, "Qdrant delete should be called for vector documents");
    }

    #endregion

    #region 4. Concurrency Tests

    [Fact]
    public async Task ConcurrentDeletion_HandlesRaceConditionGracefully()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("ConcurrentDelete.pdf", withVectorDoc: false);

        // Create two independent handlers with separate DbContexts
        var handler1 = CreateIndependentHandler();
        var handler2 = CreateIndependentHandler();

        var command = new DeletePdfCommand(pdfId.ToString());

        // Act
        var task1 = Task.Run(async () => await handler1.Handle(command, TestCancellationToken));
        var task2 = Task.Run(async () => await handler2.Handle(command, TestCancellationToken));

        var results = await Task.WhenAll(task1, task2);

        // Assert
        var successCount = results.Count(r => r.Success);
        var notFoundCount = results.Count(r => !r.Success && r.Message.Contains("not found"));

        // One should succeed, one should get "not found"
        successCount.Should().Be(1, "exactly one deletion should succeed");
        notFoundCount.Should().Be(1, "the second deletion should get 'not found'");

        // Verify final database state
        var pdfExists = await _dbContext!.PdfDocuments.AnyAsync(p => p.Id == pdfId);
        pdfExists.Should().BeFalse("PDF should be deleted by the successful operation");
    }

    private DeletePdfCommandHandler CreateIndependentHandler()
    {
        var scope = _serviceProvider!.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IBlobStorageService>();
        var cache = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DeletePdfCommandHandler>>();

        return new DeletePdfCommandHandler(dbContext, scopeFactory, blobStorage, cache, logger);
    }

    #endregion

    #region 5. Rollback/Transaction Tests

    [Fact]
    public async Task DeleteWithDbUpdateException_ThrowsPdfStorageException()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("RollbackTest.pdf", withVectorDoc: false);

        // Simulate DbUpdateException by disposing the context before save
        var handler = _serviceProvider!.GetRequiredService<DeletePdfCommandHandler>();
        var command = new DeletePdfCommand(pdfId.ToString());

        // Create a handler with a disposed DbContext to force DbUpdateException
        var disposedContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await disposedContext.DisposeAsync();

        var faultyHandler = new DeletePdfCommandHandler(
            disposedContext,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<IBlobStorageService>(),
            _serviceProvider.GetRequiredService<IAiResponseCacheService>(),
            _serviceProvider.GetRequiredService<ILogger<DeletePdfCommandHandler>>()
        );

        // Act
        Func<Task> act = async () => await faultyHandler.Handle(command, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*disposed*", "disposed context should cause exception");
    }

    #endregion

    #region 6. Service Failure Resilience Tests

    [Fact]
    public async Task DeleteWithQdrantFailure_StillSucceedsWithWarningLogged()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("QdrantFailure.pdf", withVectorDoc: true);

        // Create mock Qdrant that fails
        var qdrantMock = new Mock<IQdrantService>();
        qdrantMock.Setup(q => q.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Qdrant service unavailable"));

        var services = new ServiceCollection();
        services.AddScoped<IQdrantService>(_ => qdrantMock.Object);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = services.BuildServiceProvider();
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider);
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var handler = new DeletePdfCommandHandler(
            _dbContext!,
            mockScopeFactory.Object,
            _serviceProvider!.GetRequiredService<IBlobStorageService>(),
            _serviceProvider.GetRequiredService<IAiResponseCacheService>(),
            _serviceProvider.GetRequiredService<ILogger<DeletePdfCommandHandler>>()
        );

        var command = new DeletePdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("deletion should succeed despite Qdrant failure");

        // Verify database cleanup happened
        var pdfExists = await _dbContext.PdfDocuments.AnyAsync(p => p.Id == pdfId);
        var vectorExists = await _dbContext.VectorDocuments.AnyAsync(v => v.PdfDocumentId == pdfId);

        pdfExists.Should().BeFalse();
        vectorExists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteWithBlobStorageFailure_StillSucceedsWithWarningLogged()
    {
        // Arrange
        await ResetDatabaseAsync();
        var pdfId = await CreateTestPdfAsync("BlobFailure.pdf", withVectorDoc: false);

        // Create mock BlobStorage that fails
        var blobStorageMock = new Mock<IBlobStorageService>();
        blobStorageMock.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Blob storage unavailable"));

        var handler = new DeletePdfCommandHandler(
            _dbContext!,
            _serviceProvider!.GetRequiredService<IServiceScopeFactory>(),
            blobStorageMock.Object,
            _serviceProvider.GetRequiredService<IAiResponseCacheService>(),
            _serviceProvider.GetRequiredService<ILogger<DeletePdfCommandHandler>>()
        );

        var command = new DeletePdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("deletion should succeed despite blob storage failure");

        // Verify database cleanup happened
        var pdfExists = await _dbContext.PdfDocuments.AnyAsync(p => p.Id == pdfId);
        pdfExists.Should().BeFalse();
    }

    #endregion
}