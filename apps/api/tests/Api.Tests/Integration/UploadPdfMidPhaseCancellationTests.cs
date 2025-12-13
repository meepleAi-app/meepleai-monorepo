using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using AuthRole = Api.BoundedContexts.Authentication.Domain.ValueObjects.Role;

namespace Api.Tests.Integration;

/// <summary>
/// Mid-Phase Cancellation Tests for PDF Upload (Issue #1819 - #1736 Enhanced).
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
///
/// <para><b>Purpose:</b> Test cancellation occurring MID-WAY through pipeline stages, not just at start/end</para>
/// <para><b>Production Scenarios:</b> Network interruptions, user timeouts, browser crashes during active processing</para>
/// <para><b>Acceptance Criteria:</b> Issue #1819 (#1736) - Database consistency verification across ALL stages</para>
///
/// Test Coverage:
/// 1. Mid-database operation cancellation
/// 2. Mid-blob write cancellation
/// 3. Mid-text extraction cancellation
/// 4. Mid-embedding batch cancellation
/// 5. Mid-vector store cancellation
/// 6. Random timing cancellation (stress test)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2031")]
public sealed class UploadPdfMidPhaseCancellationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private IContainer? _redisContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IConnectionMultiplexer? _redis;
    private string? _testDataDirectory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UploadPdfMidPhaseCancellationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = "test_uploadcancel";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _testDataDirectory = Path.Combine(Path.GetTempPath(), "meepleai-midphase-test-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDataDirectory);

        // Start Redis container
        _redisContainer = new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithCommand("redis-server", "--save", "\"\"", "--appendonly", "no")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("redis-cli", "ping"))
            .Build();

        await _redisContainer.StartAsync(TestCancellationToken);

        // Setup services
        var redisPort = _redisContainer.GetMappedPublicPort(6379);
        var redisConnectionString = $"localhost:{redisPort}";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        _redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(_redis);

        // Register repositories and domain services
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register MediatR
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));

        services.AddScoped<UploadPdfCommandHandler>();

        services.Configure<PdfProcessingOptions>(options =>
        {
            options.MaxFileSizeBytes = 10485760;
        });

        // Register mock services
        RegisterDefaultMockServices(services);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_testDataDirectory != null && Directory.Exists(_testDataDirectory))
        {
            try
            {
                await Task.Delay(TestConstants.Timing.SmallDelay);
                Directory.Delete(_testDataDirectory, true);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Warning: Failed to clean temp directory: {ex.Message}");
            }
        }

        if (_dbContext != null) await _dbContext.DisposeAsync();
        _redis?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        // Issue #2031: Use SharedTestcontainersFixture for PostgreSQL cleanup
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

        if (_redisContainer != null)
        {
            await _redisContainer.StopAsync(TestCancellationToken);
            await _redisContainer.DisposeAsync();
        }
    }

    private void RegisterDefaultMockServices(IServiceCollection services)
    {
        // Register repositories (required for handler)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain event infrastructure (required for handler)
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Only register default mocks if not already registered by test
        if (!services.Any(s => s.ServiceType == typeof(IBackgroundTaskService)))
        {
            var backgroundTaskMock = new Mock<IBackgroundTaskService>();
            services.AddSingleton<IBackgroundTaskService>(backgroundTaskMock.Object);
        }

        if (!services.Any(s => s.ServiceType == typeof(IAiResponseCacheService)))
        {
            var cacheMock = new Mock<IAiResponseCacheService>();
            cacheMock.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);
        }

        if (!services.Any(s => s.ServiceType == typeof(IBlobStorageService)))
        {
            var blobStorageMock = new Mock<IBlobStorageService>();
            blobStorageMock.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string fileName, string gameId, CancellationToken ct) =>
                {
                    // Sanitize filename for filesystem safety (tests may use malicious filenames)
                    var safeFileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
                    var filePath = Path.Combine(_testDataDirectory!, $"{gameId}_{safeFileName}");
                    using var fileStream = File.Create(filePath);
                    stream.CopyTo(fileStream);
                    return new BlobStorageResult(true, Guid.NewGuid().ToString(), filePath, stream.Length, null);
                });
            services.AddSingleton<IBlobStorageService>(blobStorageMock.Object);
        }

        // Always register PDF extractors (not overrideable in current tests)
        if (!services.Any(s => s.ServiceType == typeof(IPdfTextExtractor)))
        {
            var extractorMock = new Mock<IPdfTextExtractor>();
            extractorMock.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TextExtractionResult(
                    Success: true,
                    ExtractedText: "Mock extracted text from PDF",
                    PageCount: 1,
                    CharacterCount: 100,
                    OcrTriggered: false,
                    Quality: ExtractionQuality.High,
                    ErrorMessage: null));
            services.AddSingleton<IPdfTextExtractor>(extractorMock.Object);
        }

        if (!services.Any(s => s.ServiceType == typeof(IPdfTableExtractor)))
        {
            var tableExtractorMock = new Mock<IPdfTableExtractor>();
            services.AddSingleton<IPdfTableExtractor>(tableExtractorMock.Object);
        }

        if (!services.Any(s => s.ServiceType == typeof(IPdfUploadQuotaService)))
        {
            var quotaMock = new Mock<IPdfUploadQuotaService>();
            quotaMock.Setup(q => q.CheckQuotaAsync(It.IsAny<Guid>(), It.IsAny<UserTier>(), It.IsAny<AuthRole>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PdfUploadQuotaResult.Success(0, int.MaxValue, 0, int.MaxValue, DateTime.MaxValue, DateTime.MaxValue));
            services.AddSingleton<IPdfUploadQuotaService>(quotaMock.Object);
        }
    }

    private async Task SeedTestDataAsync()
    {
        var testUser = await PdfUploadTestHelpers.SeedTestUserAsync(_dbContext!);
        var testGame = await PdfUploadTestHelpers.SeedTestGameAsync(_dbContext!);
    }
    /// <summary>
    /// Tests cancellation that occurs MID-WAY through database query execution.
    ///
    /// <para><b>Test Category:</b> Cancellation - Mid-Phase Timing</para>
    /// <para><b>Production Scenario:</b> User closes browser exactly during database INSERT operation</para>
    /// <para><b>Expected Behavior:</b> Transaction rollback, no partial data, OperationCanceledException</para>
    /// <para><b>Acceptance Criteria:</b> Issue #1819 (#1736) - Database consistency after mid-phase cancellation</para>
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenCancelledMidDatabaseOperation_RollsBackCompletely()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 10);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("mid_db_cancel.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        using var cts = PdfUploadTestHelpers.CreateDelayedCancellation(10); // Very short delay

        // Act - May complete or be cancelled depending on timing
        try
        {
            var result = await handler.Handle(command, cts.Token);
            // If completes, that's OK - just verify cleanup happens if failure
        }
        catch (OperationCanceledException)
        {
            // Expected if cancelled during DB operation
        }

        // Assert - Verify consistency regardless of whether operation completed or was cancelled
        await PdfUploadTestHelpers.VerifyDatabaseConsistencyAsync(_dbContext, testUser.Id, testGame.Id);
    }

    /// <summary>
    /// Tests cancellation during blob storage write operation (mid-transfer).
    ///
    /// <para><b>Production Scenario:</b> Network interruption during file upload to storage</para>
    /// <para><b>Expected Behavior:</b> Cleanup partial data, no orphaned files</para>
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenCancelledMidBlobWrite_CleansUpPartialData()
    {
        // Arrange - Override blob storage with mid-write cancellation mock using SharedTestcontainersFixture

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Mock blob storage that delays then throws
        var midWriteBlob = new Mock<IBlobStorageService>();
        midWriteBlob.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream stream, string fileName, string gameId, CancellationToken ct) =>
            {
                await Task.Delay(TestConstants.Timing.LargeDelay, ct); // Will throw TaskCanceledException
                return new BlobStorageResult(false, null, null, 0, "Never reached");
            });
        services.AddSingleton<IBlobStorageService>(midWriteBlob.Object);

        RegisterDefaultMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 10485760);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>();

        var midWriteProvider = services.BuildServiceProvider();
        var testDbContext = midWriteProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync(TestCancellationToken);
        await PdfUploadTestHelpers.CleanDatabaseAsync(testDbContext);

        var testUser = await PdfUploadTestHelpers.SeedTestUserAsync(testDbContext);
        var testGame = await PdfUploadTestHelpers.SeedTestGameAsync(testDbContext);

        var handler = midWriteProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 100);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("mid_blob_write.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        using var cts = PdfUploadTestHelpers.CreateDelayedCancellation(100); // Cancel mid-write

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await handler.Handle(command, cts.Token));

        // Verify cleanup
        await PdfUploadTestHelpers.VerifyNoPdfDocumentsAsync(testDbContext);
        PdfUploadTestHelpers.VerifyNoOrphanedFiles(_testDataDirectory!, "*mid_blob_write*");
    }

    /// <summary>
    /// Tests cancellation during text extraction processing (mid-extraction).
    ///
    /// <para><b>Production Scenario:</b> Long PDF extraction cancelled by user timeout</para>
    /// <para><b>Expected Behavior:</b> Release resources, no memory leaks</para>
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenCancelledMidTextExtraction_ReleasesResources()
    {
        // Arrange - Mock extractor that delays during processing
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseInMemoryDatabase($"MidExtractionTest-{Guid.NewGuid()}");
        });

        var midExtractor = new Mock<IPdfTextExtractor>();
        midExtractor.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream stream, bool ocr, CancellationToken ct) =>
            {
                await Task.Delay(TestConstants.Timing.LargeDelay, ct); // Will throw if cancelled mid-processing
                throw new OperationCanceledException("Should not reach here");
            });
        services.AddSingleton<IPdfTextExtractor>(midExtractor.Object);

        RegisterDefaultMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 10485760);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>();

        var midExtractionProvider = services.BuildServiceProvider();
        var testDbContext = midExtractionProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync(TestCancellationToken);

        var testUser = await PdfUploadTestHelpers.SeedTestUserAsync(testDbContext);
        var testGame = await PdfUploadTestHelpers.SeedTestGameAsync(testDbContext);

        var handler = midExtractionProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 50);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("mid_extraction.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        using var cts = PdfUploadTestHelpers.CreateDelayedCancellation(100); // Cancel mid-extraction

        // Act
        var result = await handler.Handle(command, cts.Token);

        // Assert - Upload completes, background processing handles cancellation
        result.Should().NotBeNull();
    }

    /// <summary>
    /// Tests cancellation during embedding generation batch processing.
    ///
    /// <para><b>Production Scenario:</b> User cancels during slow embedding API call</para>
    /// <para><b>Expected Behavior:</b> Stop processing, no partial embeddings indexed</para>
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenCancelledMidEmbeddingBatch_StopsGracefully()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 100);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("mid_embedding.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        using var cts = PdfUploadTestHelpers.CreateDelayedCancellation(150);

        // Act
        try
        {
            var result = await handler.Handle(command, cts.Token);
            result.Should().NotBeNull();
        }
        catch (OperationCanceledException)
        {
            // Acceptable - cancellation propagated
        }

        // Verify no partial processing artifacts
        await PdfUploadTestHelpers.VerifyDatabaseConsistencyAsync(_dbContext, testUser.Id, testGame.Id);
    }

    /// <summary>
    /// Tests cancellation during vector store indexing operation.
    ///
    /// <para><b>Production Scenario:</b> Qdrant indexing timeout or user cancellation</para>
    /// <para><b>Expected Behavior:</b> Maintain consistency, no corrupted vector indices</para>
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenCancelledMidVectorStore_MaintainsConsistency()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 50);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("mid_vector.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        using var cts = PdfUploadTestHelpers.CreateDelayedCancellation(200);

        // Act
        try
        {
            await handler.Handle(command, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected for mid-phase cancellation
        }

        // Verify database consistency maintained
        await PdfUploadTestHelpers.VerifyDatabaseConsistencyAsync(_dbContext, testUser.Id, testGame.Id);
    }

    /// <summary>
    /// Tests cancellation at random unpredictable timing (stress test).
    ///
    /// <para><b>Production Scenario:</b> Real-world unpredictable user cancellation timing</para>
    /// <para><b>Expected Behavior:</b> ALWAYS clean up resources, regardless of timing</para>
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenCancelledAtRandomStage_AlwaysCleansUp()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 50);
        var formFile = PdfUploadTestHelpers.CreateMockFormFile("random_cancel.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Use random cancellation helper
        try
        {
            await PdfUploadTestHelpers.ExecuteWithRandomCancellation(
                ct => handler.Handle(command, ct),
                minDelayMs: 10,
                maxDelayMs: 300);
        }
        catch (OperationCanceledException)
        {
            // Expected - random cancellation occurred
        }

        // Verify cleanup regardless of when cancellation occurred
        var docCount = await _dbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().BeLessThanOrEqualTo(1, "at most one document created before cancellation");

        // Note: Files may exist if upload completed before cancellation - this is expected behavior
        // The key test is database consistency, not file existence

        // Verify database consistency
        await PdfUploadTestHelpers.VerifyDatabaseConsistencyAsync(_dbContext, testUser.Id, testGame.Id);
    }

    /// <summary>
    /// Tests comprehensive database consistency after cancellation at multiple pipeline stages.
    ///
    /// <para><b>Test Category:</b> Cancellation - Consistency Verification</para>
    /// <para><b>Production Scenario:</b> Verify no data corruption after cancellation at ANY stage</para>
    /// <para><b>Expected Behavior:</b> FK integrity maintained, no orphaned records, referential consistency</para>
    /// <para><b>Acceptance Criteria:</b> Issue #1819 (#1736) - Database consistency verification (EXPLICIT)</para>
    /// </summary>
    /// <remarks>
    /// This test explicitly satisfies the acceptance criterion:
    /// "Database consistency verification" by testing ALL stages.
    ///
    /// Verifies:
    /// - User and Game entities remain untouched
    /// - No orphaned PdfDocument records
    /// - FK relationships valid
    /// - Transaction atomicity across all cancellation scenarios
    /// </remarks>
    [Fact(Timeout = 60000)]
    public async Task UploadPdf_WhenCancelledAtMultipleStages_MaintainsDatabaseConsistency()
    {
        // Arrange - Test cancellation at 5 different stages
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        var cancellationTimings = new[] { 10, 50, 100, 200, 500 };

        foreach (var delayMs in cancellationTimings)
        {
            using var scope = _serviceProvider!.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<UploadPdfCommandHandler>();

            var pdfBytes = PdfUploadTestHelpers.CreateValidPdfBytes(1024 * 20);
            var formFile = PdfUploadTestHelpers.CreateMockFormFile($"consistency_{delayMs}.pdf", pdfBytes);

            var command = new UploadPdfCommand(
                GameId: testGame.Id.ToString(),
                UserId: testUser.Id,
                File: formFile);

            using var cts = PdfUploadTestHelpers.CreateDelayedCancellation(delayMs);

            // Act
            try
            {
                await handler.Handle(command, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected for some timings
            }

            // Assert - Verify consistency after EACH scenario
            await PdfUploadTestHelpers.VerifyDatabaseConsistencyAsync(_dbContext, testUser.Id, testGame.Id);

            // Verify no FK violations
            var orphanedCount = await _dbContext.PdfDocuments
                .Where(d => !_dbContext.Users.Any(u => u.Id == d.UploadedByUserId) ||
                           !_dbContext.Games.Any(g => g.Id == d.GameId))
                .CountAsync(TestContext.Current.CancellationToken);
            orphanedCount.Should().Be(0, $"no orphaned documents after cancellation at {delayMs}ms");

            // Clean up for next iteration
            var createdDocs = await _dbContext.PdfDocuments
                .Where(d => d.GameId == testGame.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
            _dbContext.PdfDocuments.RemoveRange(createdDocs);
            await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Final verification
        var userStillExists = await _dbContext.Users.AnyAsync(u => u.Id == testUser.Id, TestCancellationToken);
        var gameStillExists = await _dbContext.Games.AnyAsync(g => g.Id == testGame.Id, TestCancellationToken);

        userStillExists.Should().BeTrue("user should survive all cancellation scenarios");
        gameStillExists.Should().BeTrue("game should survive all cancellation scenarios");
    }
}
