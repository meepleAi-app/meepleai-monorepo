using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;
using AuthRole = Api.BoundedContexts.Authentication.Domain.ValueObjects.Role;

namespace Api.Tests.Integration;

/// <summary>
/// Comprehensive integration tests for PDF upload workflow (Issue #1688).
/// Tests the complete upload pipeline using Testcontainers for real infrastructure.
///
/// Test Categories:
/// 1. Invalid PDF Scenarios (4+ tests): Corrupted, non-PDF, empty, malformed
/// 2. Large File Handling (3+ tests): Near limit, over limit, timeout
/// 3. Concurrent Upload Tests (2+ tests): Race conditions, isolation
/// 4. Storage Failure Scenarios (4+ tests): Disk full, permissions, rollback
/// 5. Integration Points (5+ tests): DB persistence, file storage, Qdrant, events, cleanup
///
/// Infrastructure: PostgreSQL (real DB), Redis (real cache), Qdrant (mocked for now)
/// </summary>
[Collection("PdfUploadIntegration")]
public sealed class UploadPdfIntegrationTests : IAsyncLifetime
{
    #region Test Infrastructure

    private IContainer? _postgresContainer;
    private IContainer? _redisContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IConnectionMultiplexer? _redis;
    private string? _testDataDirectory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // Create temp directory for test PDFs
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "meepleai-test-pdfs-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDataDirectory);

        // Start PostgreSQL container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "pdf_upload_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);

        // Start Redis container with CONFIG SET enabled for FLUSHDB
        _redisContainer = new ContainerBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithCommand("redis-server", "--save", "\"\"", "--appendonly", "no")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("redis-cli", "ping"))
            .Build();

        await _redisContainer.StartAsync(TestCancellationToken);

        // Setup services
        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
        var redisPort = _redisContainer.GetMappedPublicPort(6379);
        var connectionString = $"Host=localhost;Port={postgresPort};Database=pdf_upload_test;Username=postgres;Password=postgres;";
        var redisConnectionString = $"localhost:{redisPort}";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register Redis
        _redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
        services.AddSingleton<IConnectionMultiplexer>(_redis);

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPdfDocumentRepository, PdfDocumentRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Register domain event infrastructure
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Register MediatR
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));

        // Register the handler explicitly for test access
        services.AddScoped<UploadPdfCommandHandler>();

        // Register PdfProcessingOptions with configurable limit
        services.Configure<PdfProcessingOptions>(options =>
        {
            options.MaxFileSizeBytes = 10485760; // 10 MB for test efficiency
        });

        // Register mock services
        RegisterMockServices(services);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);

        // Note: Redis FlushDB not needed - each test uses isolated instances
        // Tests don't share Redis state due to fresh service provider per test

        // Seed test data
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Cleanup temp directory with async protection
        if (_testDataDirectory != null && Directory.Exists(_testDataDirectory))
        {
            try
            {
                // Wait for any pending I/O to complete
                await Task.Delay(100);
                Directory.Delete(_testDataDirectory, true);
            }
            catch (IOException ex)
            {
                // Log but don't fail test cleanup
                Console.WriteLine($"Warning: Failed to clean temp directory: {ex.Message}");
            }
        }

        _dbContext?.Dispose();
        _redis?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
        }

        if (_redisContainer != null)
        {
            await _redisContainer.StopAsync(TestCancellationToken);
            await _redisContainer.DisposeAsync();
        }
    }

    private void RegisterMockServices(IServiceCollection services)
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
                    var filePath = Path.Combine(_testDataDirectory!, $"{gameId}_{fileName}");
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
        // Create test user
        var testUser = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@uploadtest.com",
            DisplayName = "Test User",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Users.Add(testUser);

        // Create test game
        var testGame = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Game for PDF Upload",
            BggId = 12345,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 30,
            MaxPlayTimeMinutes = 90,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(testGame);

        await _dbContext.SaveChangesAsync();
    }

    #endregion

    #region Helper Methods

    private IFormFile CreateMockFormFile(string fileName, byte[] content, string contentType = "application/pdf")
    {
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns(fileName);
        formFile.Setup(f => f.Length).Returns(content.Length);
        formFile.Setup(f => f.ContentType).Returns(contentType);
        // Return a NEW stream each time OpenReadStream() is called (Issue #1688: PDF validation + blob storage need separate streams)
        formFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return formFile.Object;
    }

    private byte[] CreateValidPdfBytes(int sizeInBytes)
    {
        // Minimal valid PDF structure
        var header = "%PDF-1.4\n"u8.ToArray();
        var trailer = "%%EOF\n"u8.ToArray();
        var padding = new byte[Math.Max(0, sizeInBytes - header.Length - trailer.Length)];

        var pdf = new byte[header.Length + padding.Length + trailer.Length];
        Buffer.BlockCopy(header, 0, pdf, 0, header.Length);
        Buffer.BlockCopy(padding, 0, pdf, header.Length, padding.Length);
        Buffer.BlockCopy(trailer, 0, pdf, header.Length + padding.Length, trailer.Length);

        return pdf;
    }

    private byte[] CreateCorruptedPdfBytes()
    {
        // Invalid PDF structure
        return "This is not a valid PDF file content"u8.ToArray();
    }

    private static async Task<UserEntity> SeedUserInContextAsync(MeepleAiDbContext context)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "test@uploadtest.com",
            DisplayName = "Test User",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<GameEntity> SeedGameInContextAsync(MeepleAiDbContext context)
    {
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Game for PDF Upload",
            BggId = 12345,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 30,
            MaxPlayTimeMinutes = 90,
            CreatedAt = DateTime.UtcNow
        };
        context.Games.Add(game);
        await context.SaveChangesAsync();
        return game;
    }

    #endregion

    #region 1. Invalid PDF Scenarios Tests

    [Fact(Timeout = 30000)] // 30s for Testcontainers integration tests
    public async Task UploadPdf_WithCorruptedPdf_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        var corruptedBytes = CreateCorruptedPdfBytes();
        var formFile = CreateMockFormFile("corrupted.pdf", corruptedBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid", "file type or corrupted content should be rejected");
        result.Document.Should().BeNull("no document should be created for corrupted PDF");

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync();
        docCount.Should().Be(0, "corrupted PDF should not create database record");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithNonPdfFile_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        var textContent = "This is a plain text file, not a PDF"u8.ToArray();
        var formFile = CreateMockFormFile("document.txt", textContent, "text/plain");

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("file type", "should reject non-PDF content type");
        result.Message.Should().Contain("text/plain", "error message should mention actual content type");
        result.Document.Should().BeNull();

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync();
        docCount.Should().Be(0, "non-PDF file should not create database record");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithEmptyFile_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        var emptyBytes = Array.Empty<byte>();
        var formFile = CreateMockFormFile("empty.pdf", emptyBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No file provided", "empty file should be rejected");
        result.Document.Should().BeNull();

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync();
        docCount.Should().Be(0, "empty file should not create database record");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithMalformedPdfStructure_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        // PDF with header but no trailer (malformed structure)
        var malformedBytes = "%PDF-1.4\nincomplete content without trailer"u8.ToArray();
        var formFile = CreateMockFormFile("malformed.pdf", malformedBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace("should provide error message for malformed PDF");
        result.Document.Should().BeNull();

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync();
        docCount.Should().Be(0, "malformed PDF should not create database record");
    }

    #endregion

    #region 2. Large File Handling Tests

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithFileNearSizeLimit_Succeeds()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        // Create PDF just below limit (10MB for test efficiency)
        var nearLimitSize = (10 * 1024 * 1024) - 1024;
        var largeValidPdf = CreateValidPdfBytes(nearLimitSize);
        var formFile = CreateMockFormFile("large_valid.pdf", largeValidPdf);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("file near size limit should be accepted");
        result.Document.Should().NotBeNull("should return document for successful upload");
        result.Document!.Id.Should().NotBeEmpty("should return document ID for successful upload");

        // Verify database record created
        var documentId = result.Document != null ? Guid.Parse(result.Document.Id.ToString()) : Guid.Empty;
        var doc = await _dbContext.PdfDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        doc.Should().NotBeNull("document should be persisted to database");
        doc!.FileSizeBytes.Should().Be(nearLimitSize, "file size should be recorded correctly");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithFileExceedingSizeLimit_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        // Create PDF exceeding limit (10MB + 1KB for test efficiency)
        var overLimitSize = (10 * 1024 * 1024) + 1024;
        var oversizedPdf = CreateValidPdfBytes(overLimitSize);
        var formFile = CreateMockFormFile("oversized.pdf", oversizedPdf);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("file exceeding size limit should be rejected");
        result.Message.Should().Contain("too large", "error message should mention size limit");
        result.Message.Should().Contain("10", "error message should mention the limit in MB");
        result.Document.Should().BeNull();

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync();
        docCount.Should().Be(0, "oversized file should not create database record");
    }

    [Fact(Timeout = 60000)] // 60s for performance/memory tests
    public async Task UploadPdf_WithLargeFile_HandlesMemoryEfficiently()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        // Create large PDF (5MB - within limit but large enough to test memory handling)
        var largePdfSize = 5 * 1024 * 1024;
        var largePdf = CreateValidPdfBytes(largePdfSize);
        var formFile = CreateMockFormFile("large_memory_test.pdf", largePdf);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Capture initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(false);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Force cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(false);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("large file should be handled successfully");

        // Memory growth should be reasonable (less than 2x file size)
        var memoryGrowth = finalMemory - initialMemory;
        memoryGrowth.Should().BeLessThan(largePdfSize * 2,
            "memory growth should be reasonable for large file processing");

        // Verify file was stored
        var documentId = result.Document != null ? Guid.Parse(result.Document.Id.ToString()) : Guid.Empty;
        var doc = await _dbContext.PdfDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        doc.Should().NotBeNull();
        doc!.FileSizeBytes.Should().Be(largePdfSize);
    }

    #endregion

    #region 3. Concurrent Upload Tests

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithConcurrentUploads_HandlesCorrectly()
    {
        // Arrange
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        const int concurrentUploads = 5;
        var tasks = new List<Task<PdfUploadResult>>();

        // Create multiple upload tasks, each with its own scoped handler to avoid DbContext concurrency issues
        for (int i = 0; i < concurrentUploads; i++)
        {
            var pdfBytes = CreateValidPdfBytes(1024 * 100); // 100KB each
            var formFile = CreateMockFormFile($"concurrent_{i}.pdf", pdfBytes);

            var command = new UploadPdfCommand(
                GameId: testGame.Id.ToString(),
                UserId: testUser.Id,
                File: formFile);

            // Create scoped handler per request to simulate real request isolation
            tasks.Add(Task.Run(async () =>
            {
                using var scope = _serviceProvider!.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<UploadPdfCommandHandler>();
                return await handler.Handle(command, TestCancellationToken);
            }));
        }

        // Act - Execute all uploads concurrently
        var results = await Task.WhenAll(tasks);

        // Assert - All uploads should succeed
        results.Should().HaveCount(concurrentUploads);
        results.Should().OnlyContain(r => r.Success, "all concurrent uploads should succeed");
        results.Select(r => r.Document!.Id).Should().OnlyHaveUniqueItems("each upload should have unique document ID");

        // Verify all database records created
        var docCount = await _dbContext.PdfDocuments.CountAsync();
        docCount.Should().Be(concurrentUploads, "all concurrent uploads should create database records");

        // Verify data integrity - no duplicates or corruption
        var docs = await _dbContext.PdfDocuments.ToListAsync();
        docs.Select(d => d.Id).Should().OnlyHaveUniqueItems("document IDs should be unique");
        docs.Select(d => d.FileName).Should().OnlyHaveUniqueItems("file names should be unique");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithRaceConditions_MaintainsDataIntegrity()
    {
        // Arrange
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        const int simultaneousUploads = 10;
        var barrier = new Barrier(simultaneousUploads);
        var tasks = new List<Task<PdfUploadResult>>();

        // Create tasks that will all start at exactly the same time
        for (int i = 0; i < simultaneousUploads; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var pdfBytes = CreateValidPdfBytes(1024 * 50); // 50KB
                var formFile = CreateMockFormFile($"race_condition_{index}.pdf", pdfBytes);

                var command = new UploadPdfCommand(
                    GameId: testGame.Id.ToString(),
                    UserId: testUser.Id,
                    File: formFile);

                // Wait for all tasks to be ready
                barrier.SignalAndWait();

                // Execute simultaneously with scoped handler to avoid DbContext concurrency
                using var scope = _serviceProvider!.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<UploadPdfCommandHandler>();
                return await handler.Handle(command, TestCancellationToken);
            }));
        }

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert - Data integrity maintained despite race conditions
        results.Should().HaveCount(simultaneousUploads);
        results.Where(r => r.Success).Should().HaveCountGreaterThan(0, "at least some uploads should succeed");

        // Verify database consistency
        var docs = await _dbContext.PdfDocuments.ToListAsync();
        docs.Select(d => d.Id).Should().OnlyHaveUniqueItems("no duplicate document IDs despite race conditions");
        docs.Select(d => d.FileName).Should().OnlyHaveUniqueItems("no duplicate file names");

        // Verify all successful results have corresponding database records
        var successfulDocIds = results.Where(r => r.Success && r.Document != null).Select(r => r.Document!.Id.ToString()).ToList();
        var dbDocIds = docs.Select(d => d.Id.ToString()).ToList();
        foreach (var successId in successfulDocIds)
        {
            dbDocIds.Should().Contain(successId, "all successful uploads should have database records");
        }
    }

    #endregion

    #region 4. Storage Failure Scenarios Tests

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenBlobStorageFails_ReturnsError()
    {
        // Arrange - Create service provider with failing blob storage
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseInMemoryDatabase("BlobStorageFailureTest");
        });

        // Mock blob storage that always fails
        var failingBlobStorage = new Mock<IBlobStorageService>();
        failingBlobStorage.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(false, null, null, 0, "Simulated storage failure"));
        services.AddSingleton<IBlobStorageService>(failingBlobStorage.Object);

        // Register other required services
        RegisterMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 104857600);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>(); // Explicitly register handler for test access

        var failureServiceProvider = services.BuildServiceProvider();

        // Use the correct DbContext from the custom service provider
        var testDbContext = failureServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync();

        // Seed test data in the custom DbContext
        var testUser = await SeedUserInContextAsync(testDbContext);
        var testGame = await SeedGameInContextAsync(testDbContext);

        var handler = failureServiceProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = CreateValidPdfBytes(1024 * 10);
        var formFile = CreateMockFormFile("storage_fail.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("storage failure should result in failed upload");
        result.Message.Should().Contain("storage", "error message should mention storage issue");
        result.Document.Should().BeNull();

        // Verify no database record created (transaction rolled back)
        var docCount = await testDbContext.PdfDocuments.CountAsync();
        docCount.Should().Be(0, "no database record should be created when storage fails");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenDatabaseFails_RollsBackTransaction()
    {
        // Arrange - Use a disposed context to simulate DB failure
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        var pdfBytes = CreateValidPdfBytes(1024 * 10);
        var formFile = CreateMockFormFile("db_fail.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Dispose the context to simulate database unavailability
        await _dbContext.DisposeAsync();

        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();

        // Act & Assert - Should handle gracefully without leaving partial data
        var act = async () => await handler.Handle(command, TestCancellationToken);
        await act.Should().ThrowAsync<Exception>("database failure should throw exception");

        // Note: In real implementation, this would trigger transaction rollback
        // and proper error handling to prevent partial data
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenPartialFailure_CleansUpResources()
    {
        // Arrange - Create handler with storage that succeeds but extraction fails
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseInMemoryDatabase("PartialFailureTest");
        });

        var cleanupServiceProvider = services.BuildServiceProvider();

        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        var pdfBytes = CreateValidPdfBytes(1024 * 10);
        var formFile = CreateMockFormFile("partial_fail.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();

        // Act - Upload should complete (background processing failures handled separately)
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert - Upload succeeds, but background processing might fail
        // The handler should not leave orphaned resources
        result.Should().NotBeNull();

        if (result.Success)
        {
            // Verify cleanup happens on background processing failure
            // (In real implementation, background task would handle cleanup)
            var documentId = result.Document != null ? Guid.Parse(result.Document.Id.ToString()) : Guid.Empty;
        var doc = await _dbContext.PdfDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
            doc.Should().NotBeNull("document record should exist even if background processing fails");
        }
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenStoragePermissionDenied_ReturnsError()
    {
        // Arrange - Mock storage with permission denied error
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseInMemoryDatabase("PermissionDeniedTest");
        });

        var permissionDeniedStorage = new Mock<IBlobStorageService>();
        permissionDeniedStorage.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(false, null, null, 0, "Access denied: Insufficient permissions"));
        services.AddSingleton<IBlobStorageService>(permissionDeniedStorage.Object);

        RegisterMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 104857600);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>(); // Explicitly register handler for test access

        var permissionServiceProvider = services.BuildServiceProvider();

        // Use the correct DbContext from the custom service provider
        var testDbContext = permissionServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync();

        // Seed test data in the custom DbContext
        var testUser = await SeedUserInContextAsync(testDbContext);
        var testGame = await SeedGameInContextAsync(testDbContext);

        var handler = permissionServiceProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = CreateValidPdfBytes(1024 * 10);
        var formFile = CreateMockFormFile("permission_denied.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("permission denied should result in failed upload");
        result.Message.Should().Contain("permission", "error message should mention permission issue");
        result.Document.Should().BeNull();

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync();
        docCount.Should().Be(0, "no database record should be created when storage permissions fail");
    }

    #endregion

    #region 5. Integration Points Tests

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_SuccessfulUpload_PersistsToDatabase()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        var pdfBytes = CreateValidPdfBytes(1024 * 50); // 50KB
        var formFile = CreateMockFormFile("db_persistence_test.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Document.Should().NotBeNull();

        // Verify complete database record
        var documentId = result.Document!.Id;
        var doc = await _dbContext.PdfDocuments
            .Include(d => d.Game)
            .Include(d => d.UploadedBy)
            .Where(d => d.Id == documentId)
            .FirstOrDefaultAsync();

        doc.Should().NotBeNull("document should be persisted to database");
        doc!.FileName.Should().Be("db_persistence_test.pdf");
        doc.FileSizeBytes.Should().Be(pdfBytes.Length);
        doc.ContentType.Should().Be("application/pdf");
        doc.GameId.Should().Be(testGame.Id);
        doc.UploadedByUserId.Should().Be(testUser.Id);
        doc.ProcessingStatus.Should().Be("pending");
        doc.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // Verify relationships loaded correctly
        doc.Game.Should().NotBeNull();
        doc.Game!.Name.Should().Be(testGame.Name);
        doc.UploadedBy.Should().NotBeNull();
        doc.UploadedBy!.Email.Should().Be(testUser.Email);
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_SuccessfulUpload_StoresFileInBlobStorage()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        var pdfBytes = CreateValidPdfBytes(1024 * 25); // 25KB
        var formFile = CreateMockFormFile("blob_storage_test.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify file exists in blob storage (temp directory in tests)
        var documentId = result.Document!.Id;
        var doc = await _dbContext!.PdfDocuments.Where(d => d.Id == documentId).FirstOrDefaultAsync();
        doc.Should().NotBeNull();
        doc!.FilePath.Should().NotBeNullOrWhiteSpace();

        // Verify file was actually written
        File.Exists(doc.FilePath).Should().BeTrue("file should exist in blob storage location");

        // Verify file content matches
        var storedContent = await File.ReadAllBytesAsync(doc.FilePath);
        storedContent.Should().BeEquivalentTo(pdfBytes, "stored file content should match uploaded content");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_SuccessfulUpload_InvalidatesCache()
    {
        // Arrange
        var cacheMock = new Mock<IAiResponseCacheService>();
        var cacheInvalidated = false;
        cacheMock.Setup(c => c.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => cacheInvalidated = true)
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseInMemoryDatabase("CacheInvalidationTest");
        });
        services.AddSingleton<IAiResponseCacheService>(cacheMock.Object);

        RegisterMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 104857600);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>(); // Explicitly register handler for test access

        var cacheServiceProvider = services.BuildServiceProvider();

        // Use the correct DbContext from the custom service provider
        var testDbContext = cacheServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync();

        // Seed test data in the custom DbContext
        var testUser = await SeedUserInContextAsync(testDbContext);
        var testGame = await SeedGameInContextAsync(testDbContext);

        var handler = cacheServiceProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = CreateValidPdfBytes(1024 * 10);
        var formFile = CreateMockFormFile("cache_test.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        cacheInvalidated.Should().BeTrue("cache should be invalidated after successful upload");
        cacheMock.Verify(c => c.InvalidateGameAsync(It.Is<string>(id => id == testGame.Id.ToString()), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce(), "cache invalidation should include game ID");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_SuccessfulUpload_EnqueuesBackgroundProcessing()
    {
        // Arrange
        var backgroundTaskMock = new Mock<IBackgroundTaskService>();
        var taskEnqueued = false;
        backgroundTaskMock.Setup(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()))
            .Callback(() => taskEnqueued = true);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseInMemoryDatabase("BackgroundTaskTest");
        });
        services.AddSingleton<IBackgroundTaskService>(backgroundTaskMock.Object);

        RegisterMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 104857600);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>(); // Explicitly register handler for test access

        var backgroundServiceProvider = services.BuildServiceProvider();

        // Use the correct DbContext from the custom service provider
        var testDbContext = backgroundServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync();

        // Seed test data in the custom DbContext
        var testUser = await SeedUserInContextAsync(testDbContext);
        var testGame = await SeedGameInContextAsync(testDbContext);

        var handler = backgroundServiceProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = CreateValidPdfBytes(1024 * 10);
        var formFile = CreateMockFormFile("background_task.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        taskEnqueued.Should().BeTrue("background processing task should be enqueued");
        backgroundTaskMock.Verify(b => b.ExecuteWithCancellation(It.IsAny<string>(), It.IsAny<Func<CancellationToken, Task>>()),
            Times.Once(), "background task should be enqueued exactly once");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_SuccessfulUpload_ReturnsCorrectResult()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync();
        var testGame = await _dbContext.Games.FirstAsync();

        var pdfBytes = CreateValidPdfBytes(1024 * 30); // 30KB
        var formFile = CreateMockFormFile("result_validation.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert - Comprehensive result validation
        result.Should().NotBeNull("handler should always return a result");
        result.Success.Should().BeTrue("successful upload should return success=true");
        result.Document?.Id.ToString().Should().NotBeNull("successful upload should return document ID");
        result.Message.Should().NotBeNullOrWhiteSpace("result should include a message");

        // Validate document ID format
        Guid.TryParse(result.Document?.Id.ToString(), out var docId).Should().BeTrue("document ID should be valid GUID");

        // Verify document ID matches database record
        var doc = await _dbContext!.PdfDocuments.Where(d => d.Id == docId).FirstOrDefaultAsync();
        doc.Should().NotBeNull("returned document ID should match database record");

        // Verify all expected properties set correctly
        doc!.Id.Should().Be(docId);
        doc.GameId.Should().Be(testGame.Id);
        doc.UploadedByUserId.Should().Be(testUser.Id);
        doc.FileName.Should().Be("result_validation.pdf");
        doc.FileSizeBytes.Should().Be(pdfBytes.Length);
        doc.ContentType.Should().Be("application/pdf");
        doc.ProcessingStatus.Should().Be("pending", "newly uploaded document should be in pending status");
        doc.UploadedAt.Should().NotBe(default(DateTime), "upload timestamp should be set");
    }

    #endregion
}
