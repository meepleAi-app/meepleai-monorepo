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
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.External;

[Trait("Category", TestCategories.Unit)]
public class UnstructuredPdfTextExtractorTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<UnstructuredPdfTextExtractor>> _mockLogger;

    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UnstructuredPdfTextExtractorTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<UnstructuredPdfTextExtractor>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
    }

    private UnstructuredPdfTextExtractor CreateExtractor(IExtractionStrategySelector? strategySelector = null)
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
            strategySelector);
    }

    /// <summary>
    /// DC-2 (#3419): builds an extractor whose HTTP handler reads the <c>strategy</c> multipart form
    /// field posted to the Unstructured service, so a test can assert the exact value without fragile
    /// raw-body parsing. The handler returns a canned success response.
    /// </summary>
    private (UnstructuredPdfTextExtractor extractor, Func<string?> capturedStrategy) CreateExtractorCapturingStrategy(
        IExtractionStrategySelector? strategySelector)
    {
        string? strategy = null;
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://test-unstructured:8001")
        };

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("UnstructuredService"))
            .Returns(httpClient);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                if (req.Content is MultipartFormDataContent multipart)
                {
                    foreach (var part in multipart)
                    {
                        if (string.Equals(
                                part.Headers.ContentDisposition?.Name?.Trim('"'),
                                "strategy",
                                StringComparison.Ordinal))
                        {
                            strategy = await part.ReadAsStringAsync(ct).ConfigureAwait(false);
                        }
                    }
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(CreateSuccessResponse())
                };
            });

        var extractor = new UnstructuredPdfTextExtractor(
            _mockHttpClientFactory.Object,
            _mockLogger.Object,
            strategySelector);
        return (extractor, () => strategy);
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

    private string CreateResponseWithElements()
    {
        var response = new
        {
            text = "Preparazione\n\nDisponi le tessere.",
            chunks = new[] { new { text = "composite", page_number = 1, element_type = "CompositeElement", metadata = new Dictionary<string, object>() } },
            elements = new object[]
            {
                new { text = "Preparazione", page_number = 1, category = "Title" },
                new { text = "Disponi le tessere.", page_number = 1, category = "NarrativeText" },
                new { text = "", page_number = 2, category = "PageBreak" }
            },
            quality_score = 0.85,
            page_count = 1,
            metadata = new { extraction_duration_ms = 10, strategy_used = "fast", language = "ita", detected_tables = 0, detected_structures = new[] { "Title" }, quality_breakdown = new { text_coverage_score = 0.4, structure_detection_score = 0.2, table_detection_score = 0.0, page_coverage_score = 0.2 } }
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    }

    [Fact]
    [Trait("Issue", "3419")]
    public async Task ExtractPagedTextAsync_WhenSelectorHiRes_SendsHiResStrategyField()
    {
        // DC-2 (#3419): a table-heavy PDF routed to hi_res must post strategy=hi_res.
        var (extractor, capturedStrategy) = CreateExtractorCapturingStrategy(
            new ExtractionStrategySelector { Current = ExtractionStrategy.HiRes });

        await extractor.ExtractPagedTextAsync(CreateTestPdfStream(), cancellationToken: TestCancellationToken);

        capturedStrategy().Should().Be("hi_res");
    }

    [Fact]
    [Trait("Issue", "3419")]
    public async Task ExtractPagedTextAsync_WhenSelectorFast_SendsFastStrategyField()
    {
        var (extractor, capturedStrategy) = CreateExtractorCapturingStrategy(
            new ExtractionStrategySelector { Current = ExtractionStrategy.Fast });

        await extractor.ExtractPagedTextAsync(CreateTestPdfStream(), cancellationToken: TestCancellationToken);

        capturedStrategy().Should().Be("fast");
    }

    [Fact]
    [Trait("Issue", "3419")]
    public async Task ExtractTextAsync_WhenNoSelector_DefaultsToFastStrategyField()
    {
        // Backward-compat: manual construction (DevTools + integration tests) passes no selector →
        // the extractor must fall back to the historical hard-coded "fast".
        var (extractor, capturedStrategy) = CreateExtractorCapturingStrategy(strategySelector: null);

        await extractor.ExtractTextAsync(CreateTestPdfStream(), cancellationToken: TestCancellationToken);

        capturedStrategy().Should().Be("fast");
    }

    [Fact]
    public async Task ExtractPagedTextAsync_PopulatesStructuredElements_SkippingEmpty()
    {
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(CreateResponseWithElements()) });

        var result = await extractor.ExtractPagedTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        result.Success.Should().BeTrue();
        result.StructuredElements.Should().NotBeNull();
        result.StructuredElements!.Select(e => e.ElementType).Should().Equal("Title", "NarrativeText");
        result.StructuredElements![0].Text.Should().Be("Preparazione");
        result.StructuredElements![0].PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task ExtractPagedTextAsync_NoElements_LeavesStructuredElementsNull_AndPageChunksIntact()
    {
        var extractor = CreateExtractor();
        var pdfStream = CreateTestPdfStream();
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(CreateSuccessResponse()) });

        var result = await extractor.ExtractPagedTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        result.Success.Should().BeTrue();
        result.StructuredElements.Should().BeNull();
        // regression pin: the ExtractPagedTextAsync rewrite must not change chunking.
        // CreateSuccessResponse() has pageCount=1 → exactly one page chunk starting at offset 0.
        result.PageChunks.Should().HaveCount(1);
        result.PageChunks[0].CharStartIndex.Should().Be(0);
        result.PageChunks[0].Text.Should().NotBeEmpty();
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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        using var cts = new CancellationTokenSource();

        // Cancel the token to simulate user cancellation
        await cts.CancelAsync();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException(null, null, cts.Token));

        // Act & Assert - user cancellation should propagate the exception
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);
        var act = () => extractor.ExtractTextAsync(pdfStream, cancellationToken: linkedCts.Token);
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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        // Act & Assert
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);
        var act = () => extractor.ExtractTextAsync(pdfStream, cancellationToken: linkedCts.Token);
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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractPagedTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractPagedTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

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
        var result = await extractor.ExtractTextAsync(pdfStream, cancellationToken: TestCancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.PageCount.Should().Be(5);
        result.Quality.Should().Be(ExtractionQuality.High);
    }
}
