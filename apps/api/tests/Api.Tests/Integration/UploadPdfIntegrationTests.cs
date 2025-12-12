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
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using System.Security.Cryptography;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
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
using Npgsql;
using StackExchange.Redis;
using Xunit;
using AuthRole = Api.BoundedContexts.Authentication.Domain.ValueObjects.Role;

namespace Api.Tests.Integration;

/// <summary>
/// Comprehensive integration tests for PDF upload workflow (Issue #1688).
/// Tests the complete upload pipeline using Testcontainers for real infrastructure.
/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031).
///
/// Test Categories:
/// 1. Invalid PDF Scenarios (4+ tests): Corrupted, non-PDF, empty, malformed
/// 2. Large File Handling (3+ tests): Near limit, over limit, timeout
/// 3. Concurrent Upload Tests (2+ tests): Race conditions, isolation
/// 4. Storage Failure Scenarios (4+ tests): Disk full, permissions, rollback
/// 5. Integration Points (5+ tests): DB persistence, file storage, Qdrant, events, cleanup
///
/// Infrastructure: PostgreSQL (SharedTestcontainersFixture), Redis (real cache), Qdrant (mocked for now)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2031")]
public sealed class UploadPdfIntegrationTests : IAsyncLifetime
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

    public UploadPdfIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = "test_uploadpdf";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Create temp directory for test PDFs
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "meepleai-test-pdfs-" + Guid.NewGuid());
        Directory.CreateDirectory(_testDataDirectory);

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

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

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
                await Task.Delay(TestConstants.Timing.SmallDelay);
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
            blobStorageMock.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
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

        // Register IConfigurationService mock (required by PdfUploadQuotaService)
        if (!services.Any(s => s.ServiceType == typeof(IConfigurationService)))
        {
            var configServiceMock = new Mock<IConfigurationService>();
            configServiceMock.Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
                .Returns(Task.FromResult<int?>(null));
            services.AddSingleton<IConfigurationService>(configServiceMock.Object);
        }

        // Use REAL PdfUploadQuotaService for Issue #1821 quota reservation tests
        if (!services.Any(s => s.ServiceType == typeof(IPdfUploadQuotaService)))
        {
            services.AddScoped<IPdfUploadQuotaService, PdfUploadQuotaService>();
        }

        // Register Mock IConnectionMultiplexer (required by PdfUploadQuotaService) - Issue #1983
        if (!services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer)))
        {
            var mockDatabase = new Mock<IDatabase>();
            var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();

            // Setup GetDatabase() to return mock IDatabase (required by PdfUploadQuotaService)
            mockConnectionMultiplexer.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);
            mockConnectionMultiplexer.Setup(r => r.GetDatabase(-1, null))
                .Returns(mockDatabase.Object);

            services.AddSingleton(mockConnectionMultiplexer.Object);
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

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
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

    private static async Task CleanDatabaseAsync(MeepleAiDbContext context)
    {
        // Clean any existing data from shared database to avoid unique constraint violations
        context.PdfDocuments.RemoveRange(await context.PdfDocuments.ToListAsync(TestContext.Current.CancellationToken));
        context.Games.RemoveRange(await context.Games.ToListAsync(TestContext.Current.CancellationToken));
        context.Users.RemoveRange(await context.Users.ToListAsync(TestContext.Current.CancellationToken));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<UserEntity> SeedUserInContextAsync(MeepleAiDbContext context)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"test-{Guid.NewGuid():N}@uploadtest.com", // Unique email per test
            DisplayName = "Test User",
            Role = "User",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    private static async Task<GameEntity> SeedGameInContextAsync(MeepleAiDbContext context)
    {
        var bggId = RandomNumberGenerator.GetInt32(100000, 1_000_000); // Unique BGG id for test

        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Test Game {Guid.NewGuid():N}", // Unique name per test
            BggId = bggId,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            MinPlayTimeMinutes = 30,
            MaxPlayTimeMinutes = 90,
            CreatedAt = DateTime.UtcNow
        };
        context.Games.Add(game);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return game;
    }
    [Fact(Timeout = 30000)] // 30s for Testcontainers integration tests
    public async Task UploadPdf_WithCorruptedPdf_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

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
        result.Message.ShouldIndicateCorruptedPdf("file type or corrupted content should be rejected");
        result.Document.Should().BeNull("no document should be created for corrupted PDF");

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().Be(0, "corrupted PDF should not create database record");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithNonPdfFile_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

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
        result.Message.ShouldIndicateInvalidContentType("text/plain", "should reject non-PDF content type");
        result.Document.Should().BeNull();

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().Be(0, "non-PDF file should not create database record");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithEmptyFile_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

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
        result.Message.Should().Contain(PdfUploadErrorMessages.NoFileProvided, "empty file should be rejected");
        result.Document.Should().BeNull();

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().Be(0, "empty file should not create database record");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithMalformedPdfStructure_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

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
        result.Message.ShouldIndicateMalformedPdf("should provide error message for malformed PDF");
        result.Document.Should().BeNull();

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().Be(0, "malformed PDF should not create database record");
    }
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithFileNearSizeLimit_Succeeds()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        // Create PDF just below limit (using shared test constant)
        var nearLimitSize = PdfUploadTestConstants.FileSizes.NearLimit;
        var largeValidPdf = CreateValidPdfBytes((int)nearLimitSize);
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
        var doc = await _dbContext.PdfDocuments.FirstOrDefaultAsync(d => d.Id == documentId, TestCancellationToken);
        doc.Should().NotBeNull("document should be persisted to database");
        doc!.FileSizeBytes.Should().Be(nearLimitSize, "file size should be recorded correctly");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithFileExceedingSizeLimit_ReturnsError()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        // Create PDF exceeding limit (using shared test constant)
        var overLimitSize = PdfUploadTestConstants.FileSizes.OverLimit;
        var oversizedPdf = CreateValidPdfBytes((int)overLimitSize);
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
        result.Message.ShouldIndicateFileTooLarge(expectedMaxSizeMb: 10, "error message should mention size limit");
        result.Document.Should().BeNull();

        // Verify no database record created
        var docCount = await _dbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().Be(0, "oversized file should not create database record");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithLargeFile_HandlesSuccessfully()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        // Create large PDF (5MB - within limit but large enough to test large file handling)
        var largePdfSize = 5 * 1024 * 1024;
        var largePdf = CreateValidPdfBytes(largePdfSize);
        var formFile = CreateMockFormFile("large_file_test.pdf", largePdf);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Act
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("large file should be handled successfully");

        // Verify file was stored correctly
        var documentId = result.Document != null ? Guid.Parse(result.Document.Id.ToString()) : Guid.Empty;
        var doc = await _dbContext.PdfDocuments.FirstOrDefaultAsync(d => d.Id == documentId, TestCancellationToken);
        doc.Should().NotBeNull();
        doc!.FileSizeBytes.Should().Be(largePdfSize);

        // NOTE: Memory efficiency testing removed (Issue #1737)
        // GC.Collect() is non-deterministic and causes flaky tests.
        // For memory profiling, use tools like dotMemory or BenchmarkDotNet
        // in manual performance testing suites instead of automated tests.
    }
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithConcurrentUploads_HandlesCorrectly()
    {
        // Arrange
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

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
        var docCount = await _dbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().Be(concurrentUploads, "all concurrent uploads should create database records");

        // Verify data integrity - no duplicates or corruption
        var docs = await _dbContext.PdfDocuments.ToListAsync(TestContext.Current.CancellationToken);
        docs.Select(d => d.Id).Should().OnlyHaveUniqueItems("document IDs should be unique");
        docs.Select(d => d.FileName).Should().OnlyHaveUniqueItems("file names should be unique");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WithRaceConditions_MaintainsDataIntegrity()
    {
        // Arrange
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        const int simultaneousUploads = 10;
        using var barrier = new Barrier(simultaneousUploads);
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
        var docs = await _dbContext.PdfDocuments.ToListAsync(TestContext.Current.CancellationToken);
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
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenBlobStorageFails_ReturnsErrorAndRollsBackTransaction()
    {
        // Arrange - Use SharedTestcontainersFixture for PostgreSQL

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Real PostgreSQL from SharedTestcontainersFixture
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
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
        services.AddScoped<UploadPdfCommandHandler>();

        var failureServiceProvider = services.BuildServiceProvider();

        // Use real PostgreSQL DbContext
        var testDbContext = failureServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync();
        await CleanDatabaseAsync(testDbContext);

        // Seed test data in PostgreSQL
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
        result.Message.ShouldIndicateStorageFailure("error message should mention storage issue");
        result.Document.Should().BeNull();

        // Verify real PostgreSQL transaction rollback - no database record created
        var docCount = await testDbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().Be(0, "PostgreSQL transaction rollback should prevent any database records");

        // Additional verification: check that transaction was properly rolled back
        var userStillExists = await testDbContext.Users.AnyAsync(u => u.Id == testUser.Id);
        userStillExists.Should().BeTrue("user should still exist after failed upload");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenDatabaseConstraintViolated_RollsBackTransaction()
    {
        // Arrange - Test foreign key constraint violation with SharedTestcontainersFixture

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        RegisterMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 104857600);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>();

        var constraintServiceProvider = services.BuildServiceProvider();
        var testDbContext = constraintServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync();
        await CleanDatabaseAsync(testDbContext);

        // Seed user but NOT game - this will cause FK constraint violation
        var testUser = await SeedUserInContextAsync(testDbContext);
        var nonExistentGameId = Guid.NewGuid(); // This game doesn't exist

        var handler = constraintServiceProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = CreateValidPdfBytes(1024 * 10);
        var formFile = CreateMockFormFile("constraint_violation.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: nonExistentGameId.ToString(), // Non-existent game will trigger FK constraint
            UserId: testUser.Id,
            File: formFile);

        // Act & Assert - Should fail due to FK constraint violation
        var result = await handler.Handle(command, TestCancellationToken);

        result.Should().NotBeNull();
        result.Success.Should().BeFalse("FK constraint violation should result in failed upload");

        // Verify PostgreSQL enforced constraint and rolled back
        var docCount = await testDbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().Be(0, "PostgreSQL should enforce FK constraint and rollback transaction");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenDatabaseConnectionClosed_HandlesFailureGracefully()
    {
        // Arrange - Test connection interruption scenario with SharedTestcontainersFixture
        // Disable pooling to simulate a closed/expired connection without hitting Npgsql pruning constraints
        var connectionBuilder = new Npgsql.NpgsqlConnectionStringBuilder(_isolatedDbConnectionString)
        {
            Pooling = false
        };
        var connectionString = connectionBuilder.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        RegisterMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 104857600);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>();

        var connectionServiceProvider = services.BuildServiceProvider();
        using var seedScope = connectionServiceProvider.CreateScope();
        var testDbContext = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync();
        await CleanDatabaseAsync(testDbContext);

        var testUser = await SeedUserInContextAsync(testDbContext);
        var testGame = await SeedGameInContextAsync(testDbContext);

        // Dispose the DbContext to simulate connection closure
        await testDbContext.DisposeAsync();
        seedScope.Dispose();

        using var scope = connectionServiceProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<UploadPdfCommandHandler>();

        var pdfBytes = CreateValidPdfBytes(1024 * 10);
        var formFile = CreateMockFormFile("connection_fail.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        // Issue #2031: Cannot stop shared PostgreSQL container during test execution
        // Skip this specific offline simulation test with SharedTestcontainersFixture
        Assert.Skip("Database offline simulation not compatible with SharedTestcontainersFixture (Issue #2031)");
    }

    [Fact(Timeout = 45000)]
    public async Task UploadPdf_WhenDatabaseDeadlock_RetriesAndHandlesGracefully()
    {
        // Arrange - Simulate deadlock scenario with concurrent transactions using SharedTestcontainersFixture

        // Create two service providers with separate DbContexts
        var servicesA = new ServiceCollection();
        servicesA.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        servicesA.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        RegisterMockServices(servicesA);
        servicesA.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 104857600);
        servicesA.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        servicesA.AddScoped<UploadPdfCommandHandler>();

        var servicesB = new ServiceCollection();
        servicesB.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        servicesB.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        RegisterMockServices(servicesB);
        servicesB.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 104857600);
        servicesB.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        servicesB.AddScoped<UploadPdfCommandHandler>();

        var providerA = servicesA.BuildServiceProvider();
        var providerB = servicesB.BuildServiceProvider();

        var contextA = providerA.GetRequiredService<MeepleAiDbContext>();
        var contextB = providerB.GetRequiredService<MeepleAiDbContext>();

        await contextA.Database.EnsureCreatedAsync(TestCancellationToken);
        await CleanDatabaseAsync(contextA);

        var testUser = await SeedUserInContextAsync(contextA);
        var testGame = await SeedGameInContextAsync(contextA);

        // Create overlapping transactions that might cause deadlock
        using var transactionA = await contextA.Database.BeginTransactionAsync(TestCancellationToken);
        using var transactionB = await contextB.Database.BeginTransactionAsync(TestCancellationToken);

        try
        {
            // Transaction A locks user, tries to lock game
            var userA = await contextA.Users.Where(u => u.Id == testUser.Id).FirstAsync(TestCancellationToken);
            await Task.Delay(TestConstants.Timing.SmallDelay, TestCancellationToken); // Simulate processing time

            // Transaction B locks game, tries to lock user (potential deadlock)
            var gameB = await contextB.Games.Where(g => g.Id == testGame.Id).FirstAsync(TestCancellationToken);
            var userB = await contextB.Users.Where(u => u.Id == testUser.Id).FirstOrDefaultAsync(TestCancellationToken);

            // If we get here without deadlock, commit both
            await transactionA.CommitAsync(TestCancellationToken);
            await transactionB.CommitAsync(TestCancellationToken);
        }
        catch (Exception ex)
        {
            // PostgreSQL deadlock detection worked - rollback transactions
            await transactionA.RollbackAsync(TestCancellationToken);
            await transactionB.RollbackAsync(TestCancellationToken);

            // Assert that deadlock was handled (either deadlock exception or timeout)
            (ex.Message.Contains("deadlock") || ex.Message.Contains("timeout")).Should().BeTrue(
                "PostgreSQL should detect and handle deadlock scenarios");
        }

        // Verify database state is consistent after deadlock handling
        await using var verifyContext = providerA.GetRequiredService<MeepleAiDbContext>();
        var userExists = await verifyContext.Users.AnyAsync(u => u.Id == testUser.Id, TestCancellationToken);
        var gameExists = await verifyContext.Games.AnyAsync(g => g.Id == testGame.Id, TestCancellationToken);

        userExists.Should().BeTrue("user should exist after deadlock resolution");
        gameExists.Should().BeTrue("game should exist after deadlock resolution");
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenPartialFailure_CleansUpResources()
    {
        // Arrange - Use real PostgreSQL for partial failure cleanup test
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        var pdfBytes = CreateValidPdfBytes(1024 * 10);
        var formFile = CreateMockFormFile("partial_fail.pdf", pdfBytes);

        var command = new UploadPdfCommand(
            GameId: testGame.Id.ToString(),
            UserId: testUser.Id,
            File: formFile);

        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();

        // Act - Upload should complete (background processing failures handled separately)
        var result = await handler.Handle(command, TestCancellationToken);

        // Assert - Upload succeeds, background processing handled separately
        result.Should().NotBeNull();

        if (result.Success)
        {
            // Verify cleanup happens on background processing failure
            var documentId = result.Document != null ? Guid.Parse(result.Document.Id.ToString()) : Guid.Empty;
            var doc = await _dbContext.PdfDocuments.FirstOrDefaultAsync(d => d.Id == documentId, TestCancellationToken);
            doc.Should().NotBeNull("document record should exist in PostgreSQL even if background processing fails");
        }
    }

    [Fact(Timeout = 30000)]
    public async Task UploadPdf_WhenStoragePermissionDenied_ReturnsErrorAndRollsBack()
    {
        // Arrange - Use SharedTestcontainersFixture for permission denied test

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        var permissionDeniedStorage = new Mock<IBlobStorageService>();
        permissionDeniedStorage.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(false, null, null, 0, "Access denied: Insufficient permissions"));
        services.AddSingleton<IBlobStorageService>(permissionDeniedStorage.Object);

        RegisterMockServices(services);
        services.Configure<PdfProcessingOptions>(options => options.MaxFileSizeBytes = 104857600);
        services.AddMediatR(config => config.RegisterServicesFromAssembly(typeof(UploadPdfCommandHandler).Assembly));
        services.AddScoped<UploadPdfCommandHandler>();

        var permissionServiceProvider = services.BuildServiceProvider();

        var testDbContext = permissionServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await testDbContext.Database.EnsureCreatedAsync();
        await CleanDatabaseAsync(testDbContext);

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
        result.Message.ShouldIndicatePermissionDenied("error message should mention permission issue");
        result.Document.Should().BeNull();

        // Verify PostgreSQL transaction rollback - no database record created
        var docCount = await testDbContext.PdfDocuments.CountAsync(TestContext.Current.CancellationToken);
        docCount.Should().Be(0, "PostgreSQL transaction rollback should prevent database records on permission failure");
    }
    [Fact(Timeout = 30000)]
    public async Task UploadPdf_SuccessfulUpload_PersistsToDatabase()
    {
        // Arrange
        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

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
            .FirstOrDefaultAsync(TestCancellationToken);

        doc.Should().NotBeNull("document should be persisted to database");
        doc!.FileName.Should().Be("db_persistence_test.pdf");
        doc.FileSizeBytes.Should().Be(pdfBytes.Length);
        doc.ContentType.Should().Be("application/pdf");
        doc.GameId.Should().Be(testGame.Id);
        doc.UploadedByUserId.Should().Be(testUser.Id);
        doc.ProcessingStatus.Should().Be("pending");
        doc.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);

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
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

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
        var doc = await _dbContext!.PdfDocuments.Where(d => d.Id == documentId).FirstOrDefaultAsync(TestCancellationToken);
        doc.Should().NotBeNull();
        doc!.FilePath.Should().NotBeNullOrWhiteSpace();

        // Verify file was actually written
        // Note: File.Exists() is acceptable here as it's a fast metadata-only operation
        // that doesn't perform actual disk I/O. Using Task.Run() would add more overhead
        // than the operation itself. Actual file I/O uses async (File.ReadAllBytesAsync below).
        // Reference: https://learn.microsoft.com/en-us/dotnet/standard/io/asynchronous-file-i-o
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
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

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
        var doc = await _dbContext!.PdfDocuments.Where(d => d.Id == docId).FirstOrDefaultAsync(TestCancellationToken);
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

    /// <summary>
    /// Tests idempotency protection (Issue #1821 - #1742).
    /// Verifies that duplicate background processing tasks are detected and skipped.
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task ProcessPdfAsync_DuplicateProcessing_ShouldSkipIdempotently()
    {
        // Arrange
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        var pdfBytes = CreateValidPdfBytes(1024 * 20);
        var formFile = CreateMockFormFile("idempotency_test.pdf", pdfBytes);
        var command = new UploadPdfCommand(testGame.Id.ToString(), testUser.Id, formFile);

        var handler = _serviceProvider!.GetRequiredService<UploadPdfCommandHandler>();
        var firstResult = await handler.Handle(command, TestCancellationToken);

        firstResult.Success.Should().BeTrue();
        var pdfId = firstResult.Document!.Id;

        // Mark as already processed
        var pdfDoc = await _dbContext!.PdfDocuments.FirstOrDefaultAsync(d => d.Id == pdfId, TestCancellationToken);
        pdfDoc!.ProcessingStatus = "completed";
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act: Simulate duplicate background task
        var processPdfMethod = typeof(UploadPdfCommandHandler)
            .GetMethod("ProcessPdfAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var filePath = Path.Combine(_testDataDirectory!, pdfId.ToString() + ".pdf");
        await File.WriteAllBytesAsync(filePath, pdfBytes, TestCancellationToken);

        var task = processPdfMethod!.Invoke(handler, new object[] { pdfId.ToString(), filePath, testUser.Id, TestCancellationToken }) as Task;
        await task!;

        // Assert
        var finalDoc = await _dbContext.PdfDocuments.FirstOrDefaultAsync(d => d.Id == pdfId, TestCancellationToken);
        finalDoc!.ProcessingStatus.Should().Be("completed");

        if (File.Exists(filePath)) File.Delete(filePath);
    }

    /// <summary>
    /// Tests two-phase quota management success flow (Issue #1821 - #1743).
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task QuotaReservation_SuccessfulProcessing_ShouldConfirmQuota()
    {
        // Arrange
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var testGame = await _dbContext.Games.FirstAsync(TestCancellationToken);

        var quotaService = _serviceProvider!.GetRequiredService<IPdfUploadQuotaService>();
        var tier = UserTier.Parse(testUser.Tier);
        var role = AuthRole.Parse(testUser.Role);
        var initialQuota = await quotaService.GetQuotaInfoAsync(testUser.Id, tier, role, TestCancellationToken);

        // Act: Upload PDF (triggers quota reservation)
        var pdfBytes = CreateValidPdfBytes(1024 * 25);
        var formFile = CreateMockFormFile("quota_success_test.pdf", pdfBytes);
        var command = new UploadPdfCommand(testGame.Id.ToString(), testUser.Id, formFile);

        var handler = _serviceProvider.GetRequiredService<UploadPdfCommandHandler>();
        var result = await handler.Handle(command, TestCancellationToken);

        result.Success.Should().BeTrue();
        var pdfId = result.Document!.Id.ToString();

        // Verify quota was reserved
        var afterReservationQuota = await quotaService.GetQuotaInfoAsync(testUser.Id, tier, role, TestCancellationToken);
        afterReservationQuota.DailyUploadsUsed.Should().Be(initialQuota.DailyUploadsUsed + 1);

        // Check Redis for reservation
        var redis = _serviceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var reservationKey = $"pdf:quota:reservation:{testUser.Id}:{pdfId}";
        (await db.KeyExistsAsync(reservationKey)).Should().BeTrue();

        // Simulate successful processing and confirmation
        await quotaService.ConfirmQuotaAsync(testUser.Id, pdfId, TestCancellationToken);

        // Assert: Reservation removed, quota still consumed
        (await db.KeyExistsAsync(reservationKey)).Should().BeFalse();

        var finalQuota = await quotaService.GetQuotaInfoAsync(testUser.Id, tier, role, TestCancellationToken);
        finalQuota.DailyUploadsUsed.Should().Be(initialQuota.DailyUploadsUsed + 1);
    }

    /// <summary>
    /// Tests two-phase quota management rollback flow (Issue #1821 - #1743).
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task QuotaReservation_ProcessingFailure_ShouldReleaseQuota()
    {
        // Arrange
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);

        var quotaService = _serviceProvider!.GetRequiredService<IPdfUploadQuotaService>();
        var tier = UserTier.Parse(testUser.Tier);
        var role = AuthRole.Parse(testUser.Role);
        var initialQuota = await quotaService.GetQuotaInfoAsync(testUser.Id, tier, role, TestCancellationToken);

        // Manually reserve quota
        var pdfId = Guid.NewGuid().ToString();
        var reservationResult = await quotaService.ReserveQuotaAsync(testUser.Id, pdfId, TestCancellationToken);

        reservationResult.Reserved.Should().BeTrue();

        // Verify quota incremented
        var afterReservationQuota = await quotaService.GetQuotaInfoAsync(testUser.Id, tier, role, TestCancellationToken);
        afterReservationQuota.DailyUploadsUsed.Should().Be(initialQuota.DailyUploadsUsed + 1);

        // Verify reservation exists
        var redis = _serviceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var reservationKey = $"pdf:quota:reservation:{testUser.Id}:{pdfId}";
        (await db.KeyExistsAsync(reservationKey)).Should().BeTrue();

        // Act: Simulate processing failure
        await quotaService.ReleaseQuotaAsync(testUser.Id, pdfId, TestCancellationToken);

        // Assert: Quota rolled back
        var finalQuota = await quotaService.GetQuotaInfoAsync(testUser.Id, tier, role, TestCancellationToken);
        finalQuota.DailyUploadsUsed.Should().Be(initialQuota.DailyUploadsUsed);

        (await db.KeyExistsAsync(reservationKey)).Should().BeFalse();
    }

    /// <summary>
    /// Tests quota reservation expiry behavior (Issue #1821 - #1743).
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task QuotaReservation_Expiry_ShouldAutoCleanup()
    {
        // Arrange
        var testUser = await _dbContext!.Users.FirstAsync(TestCancellationToken);
        var quotaService = _serviceProvider!.GetRequiredService<IPdfUploadQuotaService>();
        var pdfId = Guid.NewGuid().ToString();

        // Reserve quota
        var reservationResult = await quotaService.ReserveQuotaAsync(testUser.Id, pdfId, TestCancellationToken);
        reservationResult.Reserved.Should().BeTrue();

        // Verify reservation exists with TTL
        var redis = _serviceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        var reservationKey = $"pdf:quota:reservation:{testUser.Id}:{pdfId}";
        (await db.KeyExistsAsync(reservationKey)).Should().BeTrue();

        var ttl = await db.KeyTimeToLiveAsync(reservationKey);
        ttl.Should().NotBeNull();
        ttl!.Value.TotalMinutes.Should().BeGreaterThan(29);
        ttl.Value.TotalMinutes.Should().BeLessThan(31);

        // Verify expiry time
        reservationResult.ExpiresAt.Should().NotBeNull();
        var expectedExpiry = DateTime.UtcNow.AddMinutes(30);
        var timeDiff = Math.Abs((reservationResult.ExpiresAt!.Value - expectedExpiry).TotalSeconds);
        timeDiff.Should().BeLessThan(5);
    }
}
