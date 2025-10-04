using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;

namespace Api.Tests;

public class PdfStorageServiceTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();
    private readonly List<MeepleAiDbContext> _dbContexts = new();

    public PdfStorageServiceTests()
    {
        // Configure QuestPDF for testing (community license)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Test implementation of IBackgroundTaskService that executes tasks synchronously
    /// </summary>
    private class SynchronousBackgroundTaskService : IBackgroundTaskService
    {
        public void Execute(Func<Task> task)
        {
            // Execute task synchronously (blocks until complete)
            // Use Task.Run to avoid potential synchronization context issues
            Task.Run(task).Wait();
        }
    }

    public void Dispose()
    {
        // Clean up all DbContexts
        foreach (var db in _dbContexts)
        {
            try
            {
                db.Database.CloseConnection();
                db.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up temporary directories
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private (PdfStorageService service, MeepleAiDbContext dbContext) CreateService()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var dbContext = new MeepleAiDbContext(options);
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();
        _dbContexts.Add(dbContext);

        // Setup mocks
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var configMock = new Mock<IConfiguration>();
        var loggerMock = new Mock<ILogger<PdfStorageService>>();

        var mockLogger = new Mock<ILogger<PdfTextExtractionService>>();
        var textExtractionServiceMock = new Mock<PdfTextExtractionService>(mockLogger.Object);

        var tableExtractionLoggerMock = new Mock<ILogger<PdfTableExtractionService>>();
        var tableExtractionServiceMock = new Mock<PdfTableExtractionService>(tableExtractionLoggerMock.Object);

        // Setup background task service that doesn't execute tasks (for unit tests)
        var backgroundTaskServiceMock = new Mock<IBackgroundTaskService>();
        backgroundTaskServiceMock.Setup(s => s.Execute(It.IsAny<Func<Task>>()));

        // Setup test storage path
        var testStoragePath = Path.Combine(Path.GetTempPath(), $"pdf_test_{Guid.NewGuid()}");
        _tempDirectories.Add(testStoragePath);
        configMock.Setup(c => c["PDF_STORAGE_PATH"]).Returns(testStoragePath);

        // Setup service scope factory for background tasks
        // Each scope should get its own DbContext to avoid concurrency issues
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(() =>
        {
            var newOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
                .UseSqlite(dbContext.Database.GetConnectionString())
                .Options;
            var newDbContext = new MeepleAiDbContext(newOptions);

            var scopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();

            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(MeepleAiDbContext)))
                .Returns(newDbContext);

            scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

            return scopeMock.Object;
        });

        var service = new PdfStorageService(
            dbContext,
            scopeFactoryMock.Object,
            configMock.Object,
            loggerMock.Object,
            textExtractionServiceMock.Object,
            tableExtractionServiceMock.Object,
            backgroundTaskServiceMock.Object);

        return (service, dbContext);
    }

    private Mock<IFormFile> CreateMockFormFile(
        string fileName,
        string contentType,
        long length,
        string? content = null)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(length);

        if (content != null)
        {
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(async (Stream targetStream, CancellationToken ct) =>
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                    await targetStream.WriteAsync(bytes, 0, bytes.Length, ct);
                });
        }
        else
        {
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        return fileMock;
    }

    private async Task<GameEntity> CreateTestGameAsync(MeepleAiDbContext dbContext, string tenantId, string gameId, string? userId = null)
    {
        // Check if tenant already exists
        var existingTenant = await dbContext.Tenants.FindAsync(tenantId);
        if (existingTenant == null)
        {
            var tenant = new TenantEntity
            {
                Id = tenantId,
                Name = "Test Tenant",
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Tenants.Add(tenant);
        }

        var game = new GameEntity
        {
            Id = gameId,
            TenantId = tenantId,
            Name = $"Test Game {gameId}", // Unique name per game
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Games.Add(game);

        // Create user if userId is provided
        if (userId != null)
        {
            var existingUser = await dbContext.Users.FindAsync(userId);
            if (existingUser == null)
            {
                var tenant = await dbContext.Tenants.FindAsync(tenantId);
                var user = new UserEntity
                {
                    Id = userId,
                    TenantId = tenantId,
                    Tenant = tenant!,
                    Email = $"{userId}@test.com",
                    PasswordHash = "test-hash",
                    CreatedAt = DateTime.UtcNow
                };
                dbContext.Users.Add(user);
            }
        }

        await dbContext.SaveChangesAsync();
        return game;
    }

    // === Validation Tests ===

    [Fact]
    public async Task UploadPdfAsync_ReturnsFailure_WhenFileIsNull()
    {
        // Arrange
        var (service, _) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No file provided", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_ReturnsFailure_WhenFileIsEmpty()
    {
        // Arrange
        var (service, _) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        var fileMock = CreateMockFormFile("test.pdf", "application/pdf", 0);

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No file provided", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_ReturnsFailure_WhenFileSizeExceedsMaximum()
    {
        // Arrange
        var (service, _) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        var fileMock = CreateMockFormFile("test.pdf", "application/pdf", 51 * 1024 * 1024); // 51 MB

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("exceeds maximum allowed size", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_ReturnsFailure_WhenContentTypeIsInvalid()
    {
        // Arrange
        var (service, _) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        var fileMock = CreateMockFormFile("test.txt", "text/plain", 1024);

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid file type", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_ReturnsFailure_WhenFileNameIsEmpty()
    {
        // Arrange
        var (service, _) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        var fileMock = CreateMockFormFile("", "application/pdf", 1024);

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid file name", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_ReturnsFailure_WhenFileNameIsWhitespace()
    {
        // Arrange
        var (service, _) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        var fileMock = CreateMockFormFile("   ", "application/pdf", 1024);

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid file name", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_ReturnsFailure_WhenGameDoesNotExist()
    {
        // Arrange
        var (service, _) = CreateService();
        var tenantId = "tenant1";
        var gameId = "nonexistent-game";
        var userId = "user1";
        var fileMock = CreateMockFormFile("test.pdf", "application/pdf", 1024, "test content");

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Game not found or access denied", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public async Task UploadPdfAsync_ReturnsFailure_WhenGameBelongsToDifferentTenant()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        await CreateTestGameAsync(dbContext, "tenant1", "game1");
        var fileMock = CreateMockFormFile("test.pdf", "application/pdf", 1024, "test content");

        // Act - try to access with different tenant
        var result = await service.UploadPdfAsync("tenant2", "game1", "user1", fileMock.Object);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Game not found or access denied", result.Message);
        Assert.Null(result.Document);
    }

    // === Successful Upload Tests ===

    [Fact]
    public async Task UploadPdfAsync_SuccessfullyUploadsPdf_WhenAllValidationsPass()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        await CreateTestGameAsync(dbContext, tenantId, gameId, userId);
        var fileMock = CreateMockFormFile("test.pdf", "application/pdf", 1024, "test content");

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.True(result.Success, $"Upload failed: {result.Message}");
        Assert.Equal("PDF uploaded successfully", result.Message);
        Assert.NotNull(result.Document);
        Assert.NotNull(result.Document.Id);
        Assert.Equal("test.pdf", result.Document.FileName);
        Assert.Equal(1024, result.Document.FileSizeBytes);
        Assert.Equal(userId, result.Document.UploadedByUserId);
    }

    [Fact]
    public async Task UploadPdfAsync_CreatesDatabaseRecord_WhenUploadSucceeds()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        await CreateTestGameAsync(dbContext, tenantId, gameId, userId);
        var fileMock = CreateMockFormFile("test.pdf", "application/pdf", 1024, "test content");

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Wait for background tasks to start
        await Task.Delay(50);

        // Assert
        var pdfDoc = await dbContext.PdfDocuments.FirstOrDefaultAsync(p => p.Id == result.Document!.Id);
        Assert.NotNull(pdfDoc);
        Assert.Equal(tenantId, pdfDoc.TenantId);
        Assert.Equal(gameId, pdfDoc.GameId);
        Assert.Equal("test.pdf", pdfDoc.FileName);
        Assert.Equal("pending", pdfDoc.ProcessingStatus);
    }

    [Fact]
    public async Task UploadPdfAsync_SavesFileToCorrectPath()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        await CreateTestGameAsync(dbContext, tenantId, gameId, userId);
        var fileMock = CreateMockFormFile("test.pdf", "application/pdf", 1024, "test file content");

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        var pdfDoc = await dbContext.PdfDocuments.FirstOrDefaultAsync(p => p.Id == result.Document!.Id);
        Assert.NotNull(pdfDoc);
        Assert.True(File.Exists(pdfDoc.FilePath), $"File should exist at {pdfDoc.FilePath}");

        // Verify file contains the content
        var content = await File.ReadAllTextAsync(pdfDoc.FilePath);
        Assert.Equal("test file content", content);
    }

    [Fact]
    public async Task UploadPdfAsync_SanitizesFileName()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        await CreateTestGameAsync(dbContext, tenantId, gameId, userId);
        var fileMock = CreateMockFormFile("test<>file?.pdf", "application/pdf", 1024, "test content");

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        // Sanitized name should not contain invalid characters
        Assert.DoesNotContain("<", result.Document.FileName);
        Assert.DoesNotContain(">", result.Document.FileName);
        Assert.DoesNotContain("?", result.Document.FileName);
    }

    [Fact]
    public async Task UploadPdfAsync_TruncatesLongFileName()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        await CreateTestGameAsync(dbContext, tenantId, gameId, userId);
        var longFileName = new string('a', 250) + ".pdf";
        var fileMock = CreateMockFormFile(longFileName, "application/pdf", 1024, "test content");

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Document);
        Assert.True(result.Document.FileName.Length <= 200,
            $"Sanitized filename should be <= 200 chars, was {result.Document.FileName.Length}");
    }

    // === GetPdfsByGameAsync Tests ===

    [Fact]
    public async Task GetPdfsByGameAsync_ReturnsEmptyList_WhenNoPdfsExist()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        await CreateTestGameAsync(dbContext, tenantId, gameId);

        // Act
        var result = await service.GetPdfsByGameAsync(tenantId, gameId);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetPdfsByGameAsync_ReturnsPdfs_ForCorrectGame()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        await CreateTestGameAsync(dbContext, tenantId, gameId, userId);

        // Upload a PDF
        var fileMock = CreateMockFormFile("test.pdf", "application/pdf", 1024, "test content");
        await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Wait for background tasks to start
        await Task.Delay(50);

        // Act
        var result = await service.GetPdfsByGameAsync(tenantId, gameId);

        // Assert
        Assert.Single(result);
        Assert.Equal("test.pdf", result[0].FileName);
        Assert.Equal(1024, result[0].FileSizeBytes);
        Assert.Equal(userId, result[0].UploadedByUserId);
    }

    [Fact]
    public async Task GetPdfsByGameAsync_ReturnsOnlyPdfsForSpecificGame()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var userId = "user1";
        await CreateTestGameAsync(dbContext, tenantId, "game1", userId);
        await CreateTestGameAsync(dbContext, tenantId, "game2");

        var file1Mock = CreateMockFormFile("test1.pdf", "application/pdf", 1024, "content1");
        var file2Mock = CreateMockFormFile("test2.pdf", "application/pdf", 2048, "content2");

        await service.UploadPdfAsync(tenantId, "game1", userId, file1Mock.Object);
        await service.UploadPdfAsync(tenantId, "game2", userId, file2Mock.Object);

        // Act
        var result = await service.GetPdfsByGameAsync(tenantId, "game1");

        // Assert
        Assert.Single(result);
        Assert.Equal("test1.pdf", result[0].FileName);
    }

    [Fact]
    public async Task GetPdfsByGameAsync_ReturnsOnlyPdfsForSpecificTenant()
    {
        // Arrange
        var (service1, dbContext1) = CreateService();
        var (service2, dbContext2) = CreateService();

        var userId = "user1";
        await CreateTestGameAsync(dbContext1, "tenant1", "game1", userId);
        await CreateTestGameAsync(dbContext2, "tenant2", "game2", userId);

        var file1Mock = CreateMockFormFile("test1.pdf", "application/pdf", 1024, "content1");
        var file2Mock = CreateMockFormFile("test2.pdf", "application/pdf", 2048, "content2");

        await service1.UploadPdfAsync("tenant1", "game1", userId, file1Mock.Object);
        await service2.UploadPdfAsync("tenant2", "game2", userId, file2Mock.Object);

        // Act
        var result = await service1.GetPdfsByGameAsync("tenant1", "game1");

        // Assert
        Assert.Single(result);
        Assert.Equal("test1.pdf", result[0].FileName);
    }

    [Fact]
    public async Task GetPdfsByGameAsync_ReturnsOrderedByUploadedAtDescending()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        await CreateTestGameAsync(dbContext, tenantId, gameId, userId);

        // Upload multiple PDFs with small delays
        var file1Mock = CreateMockFormFile("first.pdf", "application/pdf", 1024, "content1");
        await service.UploadPdfAsync(tenantId, gameId, userId, file1Mock.Object);

        await Task.Delay(10);

        var file2Mock = CreateMockFormFile("second.pdf", "application/pdf", 2048, "content2");
        await service.UploadPdfAsync(tenantId, gameId, userId, file2Mock.Object);

        await Task.Delay(10);

        var file3Mock = CreateMockFormFile("third.pdf", "application/pdf", 3072, "content3");
        await service.UploadPdfAsync(tenantId, gameId, userId, file3Mock.Object);

        // Act
        var result = await service.GetPdfsByGameAsync(tenantId, gameId);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("third.pdf", result[0].FileName); // Most recent first
        Assert.Equal("second.pdf", result[1].FileName);
        Assert.Equal("first.pdf", result[2].FileName);
    }

    // === Error Handling Tests ===

    [Fact]
    public async Task UploadPdfAsync_ReturnsFailure_WhenExceptionOccursDuringSave()
    {
        // Arrange
        var (service, dbContext) = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        await CreateTestGameAsync(dbContext, tenantId, gameId, userId);

        // Create a file mock that throws when copying
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.pdf");
        fileMock.Setup(f => f.ContentType).Returns("application/pdf");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk full"));

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to upload PDF", result.Message);
    }

    // === Record Structure Tests ===

    [Fact]
    public void PdfUploadResult_Success_HasCorrectProperties()
    {
        // Arrange
        var doc = new PdfDocumentDto
        {
            Id = "test-id",
            FileName = "test.pdf",
            FileSizeBytes = 1024,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = "user1"
        };

        // Act
        var result = new PdfUploadResult(true, "Success", doc);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Success", result.Message);
        Assert.NotNull(result.Document);
        Assert.Equal("test-id", result.Document.Id);
    }

    [Fact]
    public void PdfUploadResult_Failure_HasNullDocument()
    {
        // Act
        var result = new PdfUploadResult(false, "Error", null);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Error", result.Message);
        Assert.Null(result.Document);
    }

    [Fact]
    public void PdfDocumentDto_InitializesCorrectly()
    {
        // Arrange
        var uploadTime = DateTime.UtcNow;

        // Act
        var dto = new PdfDocumentDto
        {
            Id = "pdf-123",
            FileName = "rules.pdf",
            FileSizeBytes = 5242880,
            UploadedAt = uploadTime,
            UploadedByUserId = "user-456"
        };

        // Assert
        Assert.Equal("pdf-123", dto.Id);
        Assert.Equal("rules.pdf", dto.FileName);
        Assert.Equal(5242880, dto.FileSizeBytes);
        Assert.Equal(uploadTime, dto.UploadedAt);
        Assert.Equal("user-456", dto.UploadedByUserId);
    }

    // === Background Processing Tests ===

    private (PdfStorageService service, MeepleAiDbContext dbContext) CreateServiceWithRealExtraction()
    {
        // Setup in-memory database with shared connection
        var connectionString = $"DataSource=InMemoryTest{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var dbContext = new MeepleAiDbContext(options);
        dbContext.Database.OpenConnection();
        dbContext.Database.EnsureCreated();
        _dbContexts.Add(dbContext);

        // Setup service scope factory for background tasks
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(() =>
        {
            // Use the same connection string so background tasks use the same in-memory database
            var newOptions = new DbContextOptionsBuilder<MeepleAiDbContext>()
                .UseSqlite(connectionString)
                .Options;
            var newDbContext = new MeepleAiDbContext(newOptions);
            newDbContext.Database.OpenConnection();
            _dbContexts.Add(newDbContext);

            var scopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();

            // Setup mocks for IndexVectorsAsync dependencies
            // We don't want IndexVectorsAsync to run in these tests, so we provide minimal mocks
            // that will cause it to fail gracefully if triggered
            var embeddingServiceMock = new Mock<IEmbeddingService>();
            var qdrantServiceMock = new Mock<IQdrantService>();

            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(MeepleAiDbContext)))
                .Returns(newDbContext);
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(TextChunkingService)))
                .Returns((TextChunkingService?)null);
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IEmbeddingService)))
                .Returns(embeddingServiceMock.Object);
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(QdrantService)))
                .Returns(qdrantServiceMock.Object);

            scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
            return scopeMock.Object;
        });

        var configMock = new Mock<IConfiguration>();
        var testStoragePath = Path.Combine(Path.GetTempPath(), $"pdf_test_{Guid.NewGuid()}");
        _tempDirectories.Add(testStoragePath);
        configMock.Setup(c => c["PDF_STORAGE_PATH"]).Returns(testStoragePath);

        var loggerMock = new Mock<ILogger<PdfStorageService>>();

        // Create real PdfTextExtractionService
        var extractionLoggerMock = new Mock<ILogger<PdfTextExtractionService>>();
        var textExtractionService = new PdfTextExtractionService(extractionLoggerMock.Object);

        // Create real PdfTableExtractionService
        var tableExtractionLoggerMock = new Mock<ILogger<PdfTableExtractionService>>();
        var tableExtractionService = new PdfTableExtractionService(tableExtractionLoggerMock.Object);

        // Create synchronous background task service for testing
        var backgroundTaskService = new SynchronousBackgroundTaskService();

        var service = new PdfStorageService(
            dbContext,
            scopeFactoryMock.Object,
            configMock.Object,
            loggerMock.Object,
            textExtractionService,
            tableExtractionService,
            backgroundTaskService);

        return (service, dbContext);
    }

    private byte[] CreateSimplePdfBytes()
    {
        // Create a simple PDF using QuestPDF
        using var stream = new MemoryStream();
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("Test PDF for background processing");
            });
        }).GeneratePdf(stream);
        return stream.ToArray();
    }







}
