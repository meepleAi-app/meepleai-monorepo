using System.Net;
using System.Text;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// Tests for PrometheusHttpClient implementation.
/// Issue #893: Prometheus HTTP API client tests with mocked HTTP responses.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PrometheusHttpClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<PrometheusHttpClient>> _mockLogger;
    private readonly PrometheusOptions _options;
    private readonly PrometheusHttpClient _client;

    public PrometheusHttpClientTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://prometheus:9090")
        };

        _options = new PrometheusOptions
        {
            BaseUrl = "http://prometheus:9090",
            TimeoutSeconds = 30
        };

        _mockLogger = new Mock<ILogger<PrometheusHttpClient>>();
        _client = new PrometheusHttpClient(_httpClient, Options.Create(_options), _mockLogger.Object);
    }

    [Fact]
    public async Task QueryRangeAsync_WithValidQuery_ReturnsTimeSeries()
    {
        // Arrange
        var promqlQuery = "sum(increase(meepleai_llm_cost_usd[1d])) by (model_id)";
        var start = DateTime.UtcNow.AddHours(-24);
        var end = DateTime.UtcNow;
        var step = "1h";

        var prometheusResponse = @"{
            ""status"": ""success"",
            ""data"": {
                ""resultType"": ""matrix"",
                ""result"": [
                    {
                        ""metric"": { ""model_id"": ""gpt-4"" },
                        ""values"": [
                            [1733760000, ""10.5""],
                            [1733763600, ""12.3""]
                        ]
                    }
                ]
            }
        }";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(prometheusResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.QueryRangeAsync(promqlQuery, start, end, step);

        // Assert
        result.Should().NotBeNull();
        result.ResultType.Should().Be("matrix");
        result.TimeSeries.Should().ContainSingle();

        var ts = result.TimeSeries.ElementAt(0);
        ts.Metric["model_id"].Should().Be("gpt-4");
        ts.Values.Count.Should().Be(2);
        ts.Values.ElementAt(0).Value.Should().Be(10.5);
    }

    [Fact]
    public async Task QueryInstantAsync_WithValidQuery_ReturnsSingleValue()
    {
        // Arrange
        var promqlQuery = "up";
        var time = DateTime.UtcNow;

        var prometheusResponse = @"{
            ""status"": ""success"",
            ""data"": {
                ""resultType"": ""vector"",
                ""result"": [
                    {
                        ""metric"": { ""__name__"": ""test_metric"", ""instance"": ""localhost:9090"", ""job"": ""prometheus"" },
                        ""value"": [1733760000, ""1""]
                    }
                ]
            }
        }";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(prometheusResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.QueryInstantAsync(promqlQuery, time);

        // Assert
        result.Should().NotBeNull();
        result.ResultType.Should().Be("vector");
        result.TimeSeries.Should().ContainSingle();

        var ts = result.TimeSeries.ElementAt(0);
        ts.Metric["__name__"].Should().Be("test_metric");
        ts.Values.Should().ContainSingle();
        ts.Values.ElementAt(0).Value.Should().Be(1.0);
    }

    [Fact]
    public async Task QueryRangeAsync_WithErrorResponse_ThrowsInvalidOperationException()
    {
        // Arrange
        var query = "invalid_query";
        var start = DateTime.UtcNow.AddHours(-1);
        var end = DateTime.UtcNow;

        var prometheusResponse = @"{
            ""status"": ""error"",
            ""error"": ""parse error: unexpected character: '!'"",
            ""errorType"": ""bad_data""
        }";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK, // Prometheus returns 200 even for query errors
                Content = new StringContent(prometheusResponse, Encoding.UTF8, "application/json")
            });

        // Act & Assert
        var act = () =>
            _client.QueryRangeAsync(query, start, end, "5m");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task QueryRangeAsync_WithHttpError_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable
            });

        // Act & Assert
        var act = () =>
            _client.QueryRangeAsync("up", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, "5m");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task QueryRangeAsync_WithInvalidTimeRange_ThrowsArgumentException()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddHours(-1); // End before start

        // Act & Assert
        var act = () =>
            _client.QueryRangeAsync("up", start, end, "5m");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task QueryRangeAsync_WithEmptyQuery_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () =>
            _client.QueryRangeAsync("", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, "5m");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("5x")]
    [InlineData("5")]
    [InlineData("m5")]
    [InlineData("")]
    public async Task QueryRangeAsync_WithInvalidStepFormat_ThrowsArgumentException(string invalidStep)
    {
        // Arrange
        var start = DateTime.UtcNow.AddHours(-1);
        var end = DateTime.UtcNow;

        // Act & Assert
        var act = () =>
            _client.QueryRangeAsync("up", start, end, invalidStep);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("5m")]
    [InlineData("1h")]
    [InlineData("30s")]
    [InlineData("1d")]
    [InlineData("2w")]
    [InlineData("1y")]
    public async Task QueryRangeAsync_WithValidStepFormat_AcceptsStep(string validStep)
    {
        // Arrange
        var prometheusResponse = @"{
            ""status"": ""success"",
            ""data"": {
                ""resultType"": ""matrix"",
                ""result"": []
            }
        }";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(prometheusResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.QueryRangeAsync("up", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, validStep);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task QueryInstantAsync_WithDefaultTime_UsesCurrentTime()
    {
        // Arrange
        var prometheusResponse = @"{
            ""status"": ""success"",
            ""data"": {
                ""resultType"": ""vector"",
                ""result"": []
            }
        }";

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(prometheusResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.QueryInstantAsync("up");

        // Assert
        result.Should().NotBeNull();
        result.ResultType.Should().Be("vector");
    }
}
