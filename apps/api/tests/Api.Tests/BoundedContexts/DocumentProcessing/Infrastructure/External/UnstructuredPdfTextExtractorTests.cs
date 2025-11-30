using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.External;

public class UnstructuredPdfTextExtractorTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<UnstructuredPdfTextExtractor>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly PdfTextProcessingDomainService _domainService;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UnstructuredPdfTextExtractorTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<UnstructuredPdfTextExtractor>>();
        _mockConfiguration = new Mock<IConfiguration>();

        // Setup domain service with mock configuration
        var mockConfigForDomain = new Mock<IConfiguration>();
        _domainService = new PdfTextProcessingDomainService(mockConfigForDomain.Object);

        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
    }

    private UnstructuredPdfTextExtractor CreateExtractor()
    {
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://test-unstructured:8001")
        };

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("UnstructuredService"))
            .Returns(httpClient);

        return new UnstructuredPdfTextExtractor(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            _domainService);
    }

    private Stream CreateTestPdfStream()
    {
        // Minimal PDF header
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\nTest content\n%%EOF");
        return new MemoryStream(pdfBytes);
    }

    private string CreateSuccessResponse(
        string text = "Test extracted text",
        double qualityScore = 0.85,
        int pageCount = 1)
    {
        var response = new
        {
            text = text,
            chunks = new[]
            {
                new
                {
                    text = text,
                    page_number = 1,
                    element_type = "Paragraph",
                    metadata = new Dictionary<string, object>()
                }
            },
            quality_score = qualityScore,
            page_count = pageCount,
            metadata = new
            {
                extraction_duration_ms = 1250,
                strategy_used = "fast",
                language = "ita",
                detected_tables = 0,
                detected_structures = new[] { "Paragraph" },
                quality_breakdown = new
                {
                    text_coverage_score = 0.40,
                    structure_detection_score = 0.18,
                    table_detection_score = 0.15,
                    page_coverage_score = 0.20
                }
            }
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }

    [Fact]
    public async Task ExtractTextAsync_SuccessfulExtraction_ReturnsSuccessResult()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse())
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().NotBeEmpty();
        result.PageCount.Should().Be(1);
        result.Quality.Should().Be(ExtractionQuality.High);
        result.OcrTriggered.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractTextAsync_HighQualityScore_MapsToHighQuality()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(qualityScore: 0.90))
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Quality.Should().Be(ExtractionQuality.High);
    }

    [Fact]
    public async Task ExtractTextAsync_MediumQualityScore_MapsToMediumQuality()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(qualityScore: 0.65))
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Quality.Should().Be(ExtractionQuality.Medium);
    }

    [Fact]
    public async Task ExtractTextAsync_LowQualityScore_MapsToLowQuality()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(qualityScore: 0.45))
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Quality.Should().Be(ExtractionQuality.Low);
    }

    [Fact]
    public async Task ExtractTextAsync_VeryLowQualityScore_MapsToVeryLowQuality()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(qualityScore: 0.25))
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Quality.Should().Be(ExtractionQuality.VeryLow);
    }

    [Fact]
    public async Task ExtractTextAsync_ServiceUnavailable_ReturnsFailureResult()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Failed to connect");
    }

    [Fact]
    public async Task ExtractTextAsync_UserCancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();
        var cts = new CancellationTokenSource();

        // Cancel the token to simulate user cancellation
        cts.Cancel();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException(null, null, cts.Token));

        // Act & Assert - user cancellation should propagate the exception
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);
        var act = () => extractor.ExtractTextAsync(pdfStream, ct: linkedCts.Token);
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task ExtractTextAsync_BadRequest_ReturnsFailureWithStatusCode()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("{\"error\": {\"message\": \"Invalid PDF\"}}")
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractTextAsync_FileTooLarge_ReturnsFailureResult()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.RequestEntityTooLarge,
                Content = new StringContent("{}")
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractTextAsync_InvalidJsonResponse_ReturnsFailureResult()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Invalid JSON{{{")
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid JSON");
    }

    [Fact]
    public async Task ExtractTextAsync_EmptyResponse_ReturnsFailureResult()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("")
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractTextAsync_MultiPagePdf_ReturnsCorrectPageCount()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(
                    text: "Page 1 text\n\nPage 2 text\n\nPage 3 text",
                    qualityScore: 0.88,
                    pageCount: 3))
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.PageCount.Should().Be(3);
        result.CharacterCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExtractTextAsync_TextNormalization_CallsDomainService()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();
        var rawText = "  Test   with    extra   spaces  ";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(text: rawText))
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        // Domain service normalizes the text (removes extra spaces)
        result.ExtractedText.Should().NotBe(rawText);
    }

    [Fact]
    public async Task ExtractTextAsync_CancellationToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        // Act & Assert
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);
        var act = () => extractor.ExtractTextAsync(pdfStream, ct: linkedCts.Token);
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task ExtractTextAsync_ServiceError500_ReturnsFailure()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("{\"error\": {\"message\": \"Internal error\"}}")
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractPagedTextAsync_SuccessfulExtraction_ReturnsPageChunks()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(
                    text: "Page 1 content\n\nPage 2 content",
                    pageCount: 2))
            });

        // Act
        var result = await extractor.ExtractPagedTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.TotalPages.Should().Be(2);
        result.PageChunks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExtractPagedTextAsync_ServiceError_ReturnsFailure()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("{}")
            });

        // Act
        var result = await extractor.ExtractPagedTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractTextAsync_LogsRequestId_ForTraceability()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse())
            });

        // Act
        await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("RequestId")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(0.95, ExtractionQuality.High)]
    [InlineData(0.85, ExtractionQuality.High)]
    [InlineData(0.75, ExtractionQuality.Medium)]
    [InlineData(0.65, ExtractionQuality.Medium)]
    [InlineData(0.55, ExtractionQuality.Low)]
    [InlineData(0.45, ExtractionQuality.Low)]
    [InlineData(0.35, ExtractionQuality.VeryLow)]
    [InlineData(0.10, ExtractionQuality.VeryLow)]
    public async Task ExtractTextAsync_QualityScoreMapping_ReturnsCorrectEnum(
        double qualityScore,
        ExtractionQuality expectedQuality)
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(qualityScore: qualityScore))
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Quality.Should().Be(expectedQuality);
    }

    [Fact]
    public async Task ExtractTextAsync_NullResponse_ReturnsFailureResult()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("null")
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid response");
    }

    [Fact]
    public async Task ExtractTextAsync_EmptyText_ReturnsSuccessWithEmptyText()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(text: "", qualityScore: 0.0))
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.ExtractedText.Should().Be(string.Empty);
        result.Quality.Should().Be(ExtractionQuality.VeryLow);
    }

    [Fact]
    public async Task ExtractTextAsync_LargeText_HandlesCorrectly()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();
        var largeText = new string('A', 100000); // 100K characters

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateSuccessResponse(
                    text: largeText,
                    qualityScore: 0.92,
                    pageCount: 50))
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.CharacterCount.Should().BeGreaterThanOrEqualTo(100000);
        result.PageCount.Should().Be(50);
    }

    [Fact]
    public async Task ExtractTextAsync_WithMetadata_ParsesCorrectly()
    {
        // Arrange
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();

        var responseWithMetadata = CreateSuccessResponse(
            text: "Test text with metadata",
            qualityScore: 0.88,
            pageCount: 5);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseWithMetadata)
            });

        // Act
        var result = await extractor.ExtractTextAsync(pdfStream, ct: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.PageCount.Should().Be(5);
        result.Quality.Should().Be(ExtractionQuality.High);
    }
}