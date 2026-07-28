// apps/api/tests/Api.Tests/Unit/DocumentProcessing/UnstructuredExtractorHealthProbeTests.cs
using System.Net;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class UnstructuredExtractorHealthProbeTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<UnstructuredExtractorHealthProbe>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public UnstructuredExtractorHealthProbeTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<UnstructuredExtractorHealthProbe>>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
    }

    private UnstructuredExtractorHealthProbe CreateProbe()
    {
        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://test-unstructured:8001")
        };

        _mockHttpClientFactory
            .Setup(x => x.CreateClient("UnstructuredService"))
            .Returns(httpClient);

        return new UnstructuredExtractorHealthProbe(
            _mockHttpClientFactory.Object,
            _mockLogger.Object);
    }

    private void SetupResponse(HttpResponseMessage response)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void SetupThrows(Exception exception)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);
    }

    [Fact]
    public async Task IsHealthyAsync_NonEmptyElements_ReturnsTrue()
    {
        var probe = CreateProbe();
        SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(
                "{\"text\":\"hello\",\"chunks\":[],\"elements\":[{\"text\":\"Preparazione\",\"page_number\":1,\"category\":\"Title\"}],\"quality_score\":0.9,\"page_count\":1}")
        });

        var result = await probe.IsHealthyAsync(TestCancellationToken);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_EmptyElements_ReturnsFalse()
    {
        var probe = CreateProbe();
        SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(
                "{\"text\":\"hello\",\"chunks\":[],\"elements\":[],\"quality_score\":0.9,\"page_count\":1}")
        });

        var result = await probe.IsHealthyAsync(TestCancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_MissingElements_ReturnsFalse()
    {
        var probe = CreateProbe();
        SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(
                "{\"text\":\"hello\",\"chunks\":[],\"quality_score\":0.9,\"page_count\":1}")
        });

        var result = await probe.IsHealthyAsync(TestCancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_HandlerThrows_ReturnsFalse()
    {
        var probe = CreateProbe();
        SetupThrows(new HttpRequestException("Connection refused"));

        var result = await probe.IsHealthyAsync(TestCancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_ServiceError500_ReturnsFalse()
    {
        var probe = CreateProbe();
        SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("{\"error\":\"boom\"}")
        });

        var result = await probe.IsHealthyAsync(TestCancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_InvalidJson_ReturnsFalse()
    {
        var probe = CreateProbe();
        SetupResponse(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("not json{{{")
        });

        var result = await probe.IsHealthyAsync(TestCancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_TaskCanceledException_ReturnsFalse()
    {
        // Probe is a best-effort refuse-to-run gate, not a cancellation-propagating call:
        // any exception (including timeouts) is swallowed and surfaced as "unhealthy".
        var probe = CreateProbe();
        SetupThrows(new TaskCanceledException("Simulated timeout"));

        var result = await probe.IsHealthyAsync(TestCancellationToken);

        result.Should().BeFalse();
    }
}
