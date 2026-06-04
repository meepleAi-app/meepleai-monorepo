using System.Net;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Unit tests for <see cref="PrometheusLabelsClient"/> — verifies that
/// network/parse errors are translated to null so the upstream handler can
/// substitute the static fallback list. Issue #1840 SP5 F4-C7.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1840")]
public class PrometheusLabelsClientTests
{
    private static PrometheusLabelsClient CreateClient(Mock<HttpMessageHandler> handler)
    {
        var http = new HttpClient(handler.Object);
        return new PrometheusLabelsClient(
            http,
            Options.Create(new PrometheusOptions { BaseUrl = "http://prometheus:9090", TimeoutSeconds = 5 }),
            new Mock<ILogger<PrometheusLabelsClient>>().Object);
    }

    private static Mock<HttpMessageHandler> JsonHandler(HttpStatusCode statusCode, string json)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(json),
            });
        return handler;
    }

    [Fact]
    public async Task GetMetricNamesAsync_WithSuccessResponse_ReturnsLabels()
    {
        var json = """
            {
              "status": "success",
              "data": ["metric_a", "metric_b", "meepleai_chat_p95_ms"]
            }
            """;
        var client = CreateClient(JsonHandler(HttpStatusCode.OK, json));

        var result = await client.GetMetricNamesAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result!.Should().BeEquivalentTo(new[] { "metric_a", "metric_b", "meepleai_chat_p95_ms" });
    }

    [Fact]
    public async Task GetMetricNamesAsync_OnNon200_ReturnsNull()
    {
        var client = CreateClient(JsonHandler(HttpStatusCode.ServiceUnavailable, "{}"));

        var result = await client.GetMetricNamesAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMetricNamesAsync_OnMalformedJson_ReturnsNull()
    {
        var client = CreateClient(JsonHandler(HttpStatusCode.OK, "{not-json"));

        var result = await client.GetMetricNamesAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMetricNamesAsync_OnPrometheusErrorStatus_ReturnsNull()
    {
        var json = """{"status":"error","data":null,"error":"oops"}""";
        var client = CreateClient(JsonHandler(HttpStatusCode.OK, json));

        var result = await client.GetMetricNamesAsync(CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMetricNamesAsync_OnNetworkException_ReturnsNull()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var client = CreateClient(handler);

        var result = await client.GetMetricNamesAsync(CancellationToken.None);

        result.Should().BeNull();
    }
}
