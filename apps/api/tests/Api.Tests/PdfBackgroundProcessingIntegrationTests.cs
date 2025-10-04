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

/// <summary>
/// Integration tests for PDF background processing using TestContainers with PostgreSQL
/// </summary>
public class PdfBackgroundProcessingIntegrationTests : PostgresIntegrationTestBase
{
    private readonly List<string> _tempDirectories = new();

    public PdfBackgroundProcessingIntegrationTests()
    {
        // Configure QuestPDF for testing
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public override async Task DisposeAsync()
    {
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

        await base.DisposeAsync();
    }

    private PdfStorageService CreateService()
    {
        // Create services that will be used by background tasks
        var extractionLoggerMock = new Mock<ILogger<PdfTextExtractionService>>();
        var textExtractionService = new PdfTextExtractionService(extractionLoggerMock.Object);

        var tableExtractionLoggerMock = new Mock<ILogger<PdfTableExtractionService>>();
        var tableExtractionService = new PdfTableExtractionService(tableExtractionLoggerMock.Object);

        // Setup service scope factory for background tasks
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(() =>
        {
            var scopedDbContext = CreateScopedDbContext();

            var scopeMock = new Mock<IServiceScope>();
            var serviceProviderMock = new Mock<IServiceProvider>();

            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(Api.Infrastructure.MeepleAiDbContext)))
                .Returns(scopedDbContext);

            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(PdfTextExtractionService)))
                .Returns(textExtractionService);

            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(PdfTableExtractionService)))
                .Returns(tableExtractionService);

            scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
            return scopeMock.Object;
        });

        var configMock = new Mock<IConfiguration>();
        var testStoragePath = Path.Combine(Path.GetTempPath(), $"pdf_test_{Guid.NewGuid()}");
        _tempDirectories.Add(testStoragePath);
        configMock.Setup(c => c["PDF_STORAGE_PATH"]).Returns(testStoragePath);

        var loggerMock = new Mock<ILogger<PdfStorageService>>();

        // Use real BackgroundTaskService
        var backgroundLoggerMock = new Mock<ILogger<BackgroundTaskService>>();
        var backgroundTaskService = new BackgroundTaskService(backgroundLoggerMock.Object);

        return new PdfStorageService(
            DbContext,
            scopeFactoryMock.Object,
            configMock.Object,
            loggerMock.Object,
            textExtractionService,
            tableExtractionService,
            backgroundTaskService);
    }

    private async Task<GameEntity> CreateTestGameAsync(string tenantId, string gameId, string? userId = null)
    {
        // Check if tenant already exists
        var existingTenant = await DbContext.Tenants.FindAsync(tenantId);
        if (existingTenant == null)
        {
            var tenant = new TenantEntity
            {
                Id = tenantId,
                Name = "Test Tenant",
                CreatedAt = DateTime.UtcNow
            };
            DbContext.Tenants.Add(tenant);
        }

        var game = new GameEntity
        {
            Id = gameId,
            TenantId = tenantId,
            Name = $"Test Game {gameId}",
            CreatedAt = DateTime.UtcNow
        };
        DbContext.Games.Add(game);

        // Create user if userId is provided
        if (userId != null)
        {
            var existingUser = await DbContext.Users.FindAsync(userId);
            if (existingUser == null)
            {
                var tenant = await DbContext.Tenants.FindAsync(tenantId);
                var user = new UserEntity
                {
                    Id = userId,
                    TenantId = tenantId,
                    Tenant = tenant!,
                    Email = $"{userId}@test.com",
                    PasswordHash = "test-hash",
                    CreatedAt = DateTime.UtcNow
                };
                DbContext.Users.Add(user);
            }
        }

        await DbContext.SaveChangesAsync();
        return game;
    }

    private byte[] CreateSimplePdfBytes()
    {
        using var stream = new MemoryStream();
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("Integration Test PDF Content");
            });
        }).GeneratePdf(stream);
        return stream.ToArray();
    }

    private byte[] CreateMultiPagePdfBytes()
    {
        using var stream = new MemoryStream();
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("Page 1 content");
            });
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("Page 2 content");
            });
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("Page 3 content");
            });
        }).GeneratePdf(stream);
        return stream.ToArray();
    }

    private Mock<IFormFile> CreateMockFormFile(string fileName, string contentType, byte[] content)
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.Length).Returns(content.Length);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (Stream targetStream, CancellationToken ct) =>
            {
                await targetStream.WriteAsync(content, 0, content.Length, ct);
            });
        return fileMock;
    }

    private async Task<PdfDocumentEntity?> WaitForProcessingCompletionAsync(string pdfId, int maxAttempts = 20, int delayMs = 500)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            // Create new DbContext to get fresh data
            using var freshContext = CreateScopedDbContext();
            var pdfDoc = await freshContext.PdfDocuments.FindAsync(pdfId);

            if (pdfDoc != null && pdfDoc.ProcessingStatus != "pending" && pdfDoc.ProcessingStatus != "processing")
            {
                return pdfDoc;
            }

            await Task.Delay(delayMs);
        }

        return null;
    }

    [Fact]
    public async Task BackgroundExtraction_CompletesSuccessfully_WithRealPostgreSQL()
    {
        // Arrange
        var service = CreateService();
        var tenantId = "tenant1";
        var gameId = "game1";
        var userId = "user1";
        await CreateTestGameAsync(tenantId, gameId, userId);

        var pdfBytes = CreateSimplePdfBytes();
        var fileMock = CreateMockFormFile("test.pdf", "application/pdf", pdfBytes);

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Assert - upload succeeded
        Assert.True(result.Success, $"Upload failed: {result.Message}");
        Assert.NotNull(result.Document);

        // Wait for background processing to complete
        var completedPdf = await WaitForProcessingCompletionAsync(result.Document.Id);

        // Assert - background processing completed successfully
        Assert.NotNull(completedPdf);
        Assert.True(
            completedPdf.ProcessingStatus == "completed",
            $"Expected status 'completed' but got '{completedPdf.ProcessingStatus}'. Error: {completedPdf.ProcessingError}");
        Assert.NotNull(completedPdf.ExtractedText);
        // Text extraction may have encoding quirks, so check for core content
        Assert.Contains("Test PDF Content", completedPdf.ExtractedText);
        Assert.True(completedPdf.PageCount > 0);
        Assert.True(completedPdf.CharacterCount > 0);
        Assert.NotNull(completedPdf.ProcessedAt);
    }

    [Fact]
    public async Task BackgroundExtraction_HandlesMultiPagePdf_WithRealPostgreSQL()
    {
        // Arrange
        var service = CreateService();
        var tenantId = "tenant2";
        var gameId = "game2";
        var userId = "user2";
        await CreateTestGameAsync(tenantId, gameId, userId);

        var pdfBytes = CreateMultiPagePdfBytes();
        var fileMock = CreateMockFormFile("multipage.pdf", "application/pdf", pdfBytes);

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Wait for background processing
        var completedPdf = await WaitForProcessingCompletionAsync(result.Document!.Id);

        // Assert
        Assert.NotNull(completedPdf);
        Assert.True(
            completedPdf.ProcessingStatus == "completed",
            $"Expected status 'completed' but got '{completedPdf.ProcessingStatus}'. Error: {completedPdf.ProcessingError}");
        Assert.Equal(3, completedPdf.PageCount);
        Assert.Contains("Page 1 content", completedPdf.ExtractedText);
        Assert.Contains("Page 2 content", completedPdf.ExtractedText);
        Assert.Contains("Page 3 content", completedPdf.ExtractedText);
    }

    [Fact]
    public async Task BackgroundExtraction_UpdatesProcessedAtTimestamp_WithRealPostgreSQL()
    {
        // Arrange
        var service = CreateService();
        var tenantId = "tenant3";
        var gameId = "game3";
        var userId = "user3";
        await CreateTestGameAsync(tenantId, gameId, userId);

        var pdfBytes = CreateSimplePdfBytes();
        var fileMock = CreateMockFormFile("timestamp-test.pdf", "application/pdf", pdfBytes);
        var beforeUpload = DateTime.UtcNow;

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Wait for background processing
        var completedPdf = await WaitForProcessingCompletionAsync(result.Document!.Id);

        // Assert
        Assert.NotNull(completedPdf);
        Assert.NotNull(completedPdf.ProcessedAt);
        Assert.True(completedPdf.ProcessedAt >= beforeUpload);
        Assert.True(completedPdf.ProcessedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task BackgroundTaskService_ExecutesTasksInBackground()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<BackgroundTaskService>>();
        var backgroundService = new BackgroundTaskService(loggerMock.Object);
        var executed = false;

        // Act
        backgroundService.Execute(async () =>
        {
            await Task.Delay(100);
            executed = true;
        });

        // Wait for task to complete
        await Task.Delay(500);

        // Assert
        Assert.True(executed);
    }

    [Fact]
    public async Task BackgroundExtraction_HandlesEmptyPdf_WithRealPostgreSQL()
    {
        // Arrange
        var service = CreateService();
        var tenantId = "tenant4";
        var gameId = "game4";
        var userId = "user4";
        await CreateTestGameAsync(tenantId, gameId, userId);

        // Create empty PDF (no text content)
        using var stream = new MemoryStream();
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.Content().Text("");
            });
        }).GeneratePdf(stream);
        var pdfBytes = stream.ToArray();
        var fileMock = CreateMockFormFile("empty.pdf", "application/pdf", pdfBytes);

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Wait for background processing
        var completedPdf = await WaitForProcessingCompletionAsync(result.Document!.Id);

        // Assert
        Assert.NotNull(completedPdf);
        Assert.True(
            completedPdf.ProcessingStatus == "completed",
            $"Expected status 'completed' but got '{completedPdf.ProcessingStatus}'. Error: {completedPdf.ProcessingError}");
        Assert.NotNull(completedPdf.ExtractedText);
        // Empty PDF should have minimal or zero character count (whitespace may be extracted)
        Assert.True(completedPdf.CharacterCount <= 10,
            $"Expected minimal text (<=10 chars) but got {completedPdf.CharacterCount} characters: '{completedPdf.ExtractedText}'");
        // Empty PDFs may report 0 pages depending on PDF structure
        Assert.True(completedPdf.PageCount >= 0,
            $"PageCount should be non-negative but got {completedPdf.PageCount}");
    }

    [Fact]
    public async Task BackgroundExtraction_SetsStatusToFailed_WhenExtractionFails()
    {
        // Arrange
        var service = CreateService();
        var tenantId = "tenant5";
        var gameId = "game5";
        var userId = "user5";
        await CreateTestGameAsync(tenantId, gameId, userId);

        // Create a corrupt/invalid PDF file (just random bytes)
        var corruptPdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x00, 0x00 }; // Invalid PDF header
        var fileMock = CreateMockFormFile("corrupt.pdf", "application/pdf", corruptPdfBytes);

        // Act
        var result = await service.UploadPdfAsync(tenantId, gameId, userId, fileMock.Object);

        // Wait for background processing to attempt and fail
        var processedPdf = await WaitForProcessingCompletionAsync(result.Document!.Id);

        // Assert
        Assert.NotNull(processedPdf);
        // Should either fail or complete with error (depending on Docnet's error handling)
        Assert.True(
            processedPdf.ProcessingStatus == "failed" || !string.IsNullOrEmpty(processedPdf.ProcessingError),
            $"Expected processing to fail or have error, but status was '{processedPdf.ProcessingStatus}' with error: '{processedPdf.ProcessingError}'");
    }
}
