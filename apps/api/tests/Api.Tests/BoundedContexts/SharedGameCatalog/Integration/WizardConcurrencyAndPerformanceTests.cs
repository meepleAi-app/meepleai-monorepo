using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Concurrency and performance tests for the wizard workflow.
/// Validates parallel session handling, race conditions, and large file processing.
/// Issue #4160: Backend - Wizard Integration Tests
/// </summary>
[Trait("Category", TestCategories.Performance)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4160")]
public class WizardConcurrencyAndPerformanceTests
{
    private readonly IOptions<PdfProcessingOptions> _pdfOptions;

    public WizardConcurrencyAndPerformanceTests()
    {
        _pdfOptions = Options.Create(new PdfProcessingOptions
        {
            MaxFileSizeBytes = 104_857_600,
            LargePdfThresholdBytes = 52_428_800
        });
    }

    #region Concurrent Upload Tests

    [Fact]
    public async Task ConcurrentUploads_10Sessions_AllSucceed()
    {
        const int sessionCount = 10;
        var tasks = new List<Task<TempPdfUploadResult>>();

        for (var i = 0; i < sessionCount; i++)
        {
            var sessionIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                var blobMock = new Mock<IBlobStorageService>();
                var fileName = $"game-{sessionIndex}.pdf";
                var fileId = $"file-{sessionIndex}";

                blobMock
                    .Setup(b => b.StoreAsync(It.IsAny<Stream>(), fileName, "wizard-temp", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new BlobStorageResult(
                        Success: true,
                        FileId: fileId,
                        FilePath: $"{fileId}_{sessionIndex}.pdf",
                        FileSizeBytes: 1_000_000));

                var handler = new UploadPdfForGameExtractionCommandHandler(
                    blobMock.Object,
                    NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
                    _pdfOptions);

                var mockFile = CreateValidMockPdfFile(fileName, 1_000_000);
                var userId = Guid.NewGuid();

                return await handler.Handle(
                    new UploadPdfForGameExtractionCommand(mockFile, userId),
                    CancellationToken.None);
            }));
        }

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(sessionCount);
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue("All concurrent uploads should succeed");
            r.FilePath.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task ConcurrentExtractAndEnrich_ParallelSessions_NoInterference()
    {
        const int sessionCount = 5;
        var tasks = new List<Task<EnrichedGameDto>>();

        for (var i = 0; i < sessionCount; i++)
        {
            var sessionIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                // Each session has its own mocks (no shared state)
                var blobMock = new Mock<IBlobStorageService>();
                var pdfExtractorMock = new Mock<IPdfTextExtractor>();
                var llmServiceMock = new Mock<ILlmService>();
                var bggApiClientMock = new Mock<IBggApiClient>();

                var fileId = $"file-session-{sessionIndex}";
                var gameTitle = $"Game {sessionIndex}";
                var bggId = 1000 + sessionIndex;

                // fileId format: handler extracts fileId by splitting filename on '_'
                blobMock
                    .Setup(b => b.RetrieveAsync(fileId, "wizard-temp", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new MemoryStream(new byte[100]));

                pdfExtractorMock
                    .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TextExtractionResult.CreateSuccess(
                        $"{gameTitle} Rules\nPlayers: 2-4",
                        pageCount: 5, characterCount: 3000,
                        ocrTriggered: false, quality: ExtractionQuality.High));

                llmServiceMock
                    .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GameMetadataDto
                    {
                        Title = gameTitle,
                        MinPlayers = 2,
                        MaxPlayers = 4,
                        PlayingTime = 30 + sessionIndex * 10
                    });

                bggApiClientMock
                    .Setup(b => b.GetGameDetailsAsync(bggId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new BggGameDetails
                    {
                        BggId = bggId,
                        Title = $"{gameTitle} (BGG)",
                        MinPlayers = 2,
                        MaxPlayers = 4,
                        PlayingTimeMinutes = 30 + sessionIndex * 10
                    });

                // Step 1: Extract (path format: {fileId}_{timestamp}.pdf)
                var extractHandler = new ExtractGameMetadataFromPdfQueryHandler(
                    blobMock.Object, pdfExtractorMock.Object,
                    llmServiceMock.Object,
                    NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

                var extractResult = await extractHandler.Handle(
                    new ExtractGameMetadataFromPdfQuery($"{fileId}_{sessionIndex}.pdf", Guid.NewGuid()),
                    CancellationToken.None);

                // Step 2: Enrich
                var enrichHandler = new EnrichGameMetadataFromBggCommandHandler(
                    bggApiClientMock.Object,
                    NullLogger<EnrichGameMetadataFromBggCommandHandler>.Instance);

                return await enrichHandler.Handle(
                    new EnrichGameMetadataFromBggCommand(extractResult, bggId, Guid.NewGuid()),
                    CancellationToken.None);
            }));
        }

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(sessionCount);
        for (var i = 0; i < sessionCount; i++)
        {
            results[i].BggEnrichmentSucceeded.Should().BeTrue($"Session {i} should succeed");
            results[i].Title.Should().Be($"Game {i} (BGG)", $"Session {i} should have correct title");
            results[i].BggId.Should().Be(1000 + i, $"Session {i} should have correct BggId");
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Upload_LargeFile_CompletesWithinTimeout()
    {
        var blobMock = new Mock<IBlobStorageService>();

        // Simulate a 40 MB file (under LargePdfThresholdBytes)
        const long fileSizeBytes = 40_000_000;

        blobMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(
                Success: true,
                FileId: "file-large",
                FilePath: "wizard-temp/file-large/big-rulebook.pdf",
                FileSizeBytes: fileSizeBytes));

        var handler = new UploadPdfForGameExtractionCommandHandler(
            blobMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockFile = CreateValidMockPdfFile("big-rulebook.pdf", fileSizeBytes);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var result = await handler.Handle(
            new UploadPdfForGameExtractionCommand(mockFile, Guid.NewGuid()),
            cts.Token);

        result.Success.Should().BeTrue("Large file upload should complete within 30s");
        result.FileSizeBytes.Should().Be(fileSizeBytes);
    }

    [Fact]
    public async Task ExtractAndEnrich_FullPipeline_CompletesWithinTimeout()
    {
        var blobMock = new Mock<IBlobStorageService>();
        var pdfExtractorMock = new Mock<IPdfTextExtractor>();
        var llmServiceMock = new Mock<ILlmService>();
        var bggApiClientMock = new Mock<IBggApiClient>();

        // fileId format: handler extracts fileId by splitting filename on '_'
        blobMock
            .Setup(b => b.RetrieveAsync("file-perf", "wizard-temp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[500]));

        pdfExtractorMock
            .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.CreateSuccess(
                "Performance Test Game\nPlayers: 1-6\nYear: 2024\nAge: 14+\nTime: 120 min",
                pageCount: 20, characterCount: 15000,
                ocrTriggered: false, quality: ExtractionQuality.High));

        llmServiceMock
            .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameMetadataDto
            {
                Title = "Performance Test Game",
                Year = 2024,
                MinPlayers = 1,
                MaxPlayers = 6,
                PlayingTime = 120,
                MinAge = 14,
                Description = "A game with many pages"
            });

        bggApiClientMock
            .Setup(b => b.GetGameDetailsAsync(500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BggGameDetails
            {
                BggId = 500,
                Title = "Performance Test Game",
                YearPublished = 2024,
                MinPlayers = 1,
                MaxPlayers = 6,
                PlayingTimeMinutes = 120,
                ComplexityRating = 3.5m,
                AverageRating = 7.8m
            });


        // Extract (path format: {fileId}_{timestamp}.pdf)
        var extractHandler = new ExtractGameMetadataFromPdfQueryHandler(
            blobMock.Object, pdfExtractorMock.Object,
            llmServiceMock.Object,
            NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

        var extractResult = await extractHandler.Handle(
            new ExtractGameMetadataFromPdfQuery("file-perf_20260216.pdf", Guid.NewGuid()),
            cts.Token);

        extractResult.Title.Should().Be("Performance Test Game");

        // Enrich
        var enrichHandler = new EnrichGameMetadataFromBggCommandHandler(
            bggApiClientMock.Object,
            NullLogger<EnrichGameMetadataFromBggCommandHandler>.Instance);

        var enrichResult = await enrichHandler.Handle(
            new EnrichGameMetadataFromBggCommand(extractResult, 500, Guid.NewGuid()),
            cts.Token);

        enrichResult.BggEnrichmentSucceeded.Should().BeTrue();
        enrichResult.Title.Should().Be("Performance Test Game");
        enrichResult.Conflicts.Should().BeEmpty("Matching data should have no conflicts");
    }

    [Fact]
    public async Task ConcurrentMixedOperations_UploadAndExtract_NoDeadlocks()
    {
        const int operationCount = 10;
        var tasks = new List<Task>();

        for (var i = 0; i < operationCount; i++)
        {
            var index = i;
            if (index % 2 == 0)
            {
                // Upload operation
                tasks.Add(Task.Run(async () =>
                {
                    var blobMock = new Mock<IBlobStorageService>();
                    var fileName = $"upload-{index}.pdf";

                    blobMock
                        .Setup(b => b.StoreAsync(It.IsAny<Stream>(), fileName, "wizard-temp", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new BlobStorageResult(
                            Success: true, FileId: $"file-{index}",
                            FilePath: $"file-{index}_{index}.pdf",
                            FileSizeBytes: 500_000));

                    var handler = new UploadPdfForGameExtractionCommandHandler(
                        blobMock.Object,
                        NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
                        _pdfOptions);

                    var mockFile = CreateValidMockPdfFile(fileName, 500_000);
                    var result = await handler.Handle(
                        new UploadPdfForGameExtractionCommand(mockFile, Guid.NewGuid()),
                        CancellationToken.None);

                    result.Success.Should().BeTrue($"Upload {index} should succeed");
                }));
            }
            else
            {
                // Extract operation (path format: {fileId}_{timestamp}.pdf)
                tasks.Add(Task.Run(async () =>
                {
                    var blobMock = new Mock<IBlobStorageService>();
                    var pdfExtractorMock = new Mock<IPdfTextExtractor>();
                    var llmServiceMock = new Mock<ILlmService>();

                    blobMock
                        .Setup(b => b.RetrieveAsync($"file-{index}", "wizard-temp", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new MemoryStream(new byte[100]));

                    pdfExtractorMock
                        .Setup(p => p.ExtractTextAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(TextExtractionResult.CreateSuccess(
                            "Text", 3, 1000, false, ExtractionQuality.Medium));

                    llmServiceMock
                        .Setup(l => l.GenerateJsonAsync<GameMetadataDto>(
                            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new GameMetadataDto { Title = $"Game {index}" });

                    var handler = new ExtractGameMetadataFromPdfQueryHandler(
                        blobMock.Object, pdfExtractorMock.Object,
                        llmServiceMock.Object,
                        NullLogger<ExtractGameMetadataFromPdfQueryHandler>.Instance);

                    var result = await handler.Handle(
                        new ExtractGameMetadataFromPdfQuery($"file-{index}_{index}.pdf", Guid.NewGuid()),
                        CancellationToken.None);

                    result.Title.Should().Be($"Game {index}", $"Extract {index} should return correct title");
                }));
            }
        }

        // All operations should complete without deadlocks within 30s
        var allCompleted = Task.WhenAll(tasks);
        var completedInTime = await Task.WhenAny(allCompleted, Task.Delay(TimeSpan.FromSeconds(30)));

        completedInTime.Should().Be(allCompleted, "All concurrent operations should complete without deadlocks");
        await allCompleted; // Propagate any exceptions
    }

    [Fact]
    public async Task Upload_CancellationRequested_RespectsToken()
    {
        var blobMock = new Mock<IBlobStorageService>();

        blobMock
            .Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), "wizard-temp", It.IsAny<CancellationToken>()))
            .Returns<Stream, string, string, CancellationToken>(async (_, _, _, ct) =>
            {
                await Task.Delay(5000, ct); // Simulate slow storage
                return new BlobStorageResult(true, "file-cancel", "path", 1000);
            });

        var handler = new UploadPdfForGameExtractionCommandHandler(
            blobMock.Object,
            NullLogger<UploadPdfForGameExtractionCommandHandler>.Instance,
            _pdfOptions);

        var mockFile = CreateValidMockPdfFile("slow-upload.pdf", 1_000_000);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = () => handler.Handle(
            new UploadPdfForGameExtractionCommand(mockFile, Guid.NewGuid()),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "Handler should respect cancellation token");
    }

    #endregion

    #region Helper Methods

    private static IFormFile CreateValidMockPdfFile(string fileName, long fileSize)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.SetupGet(f => f.FileName).Returns(fileName);
        mockFile.SetupGet(f => f.Length).Returns(fileSize);
        mockFile.SetupGet(f => f.ContentType).Returns("application/pdf");

        var pdfContent = "%PDF-1.4\n" +
                        "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
                        "2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n" +
                        "3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n" +
                        "xref\n0 4\n0000000000 65535 f\n" +
                        "0000000009 00000 n\n0000000056 00000 n\n0000000115 00000 n\n" +
                        "trailer<</Size 4/Root 1 0 R>>\nstartxref\n198\n%%EOF";

        var pdfBytes = System.Text.Encoding.ASCII.GetBytes(pdfContent);
        mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfBytes));

        return mockFile.Object;
    }

    #endregion
}
