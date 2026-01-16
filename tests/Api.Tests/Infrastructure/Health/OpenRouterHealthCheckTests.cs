using System.Net;
using Api.Infrastructure.Health.Checks;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.Infrastructure.Health;

/// <summary>
/// Unit tests for OpenRouterHealthCheck.
/// </summary>
public class OpenRouterHealthCheckTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<OpenRouterHealthCheck>> _loggerMock;

    public OpenRouterHealthCheckTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<OpenRouterHealthCheck>>();
    }

    [Fact]
    public async Task CheckHealthAsync_WithSuccessfulResponse_ReturnsHealthy()
    {
        // Arrange
        var httpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(httpMessageHandler) { BaseAddress = new Uri("https://openrouter.ai/") };

        _httpClientFactoryMock.Setup(f => f.CreateClient("openrouter")).Returns(httpClient);

        var healthCheck = new OpenRouterHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("OpenRouter API is accessible");
    }

    [Fact]
    public async Task CheckHealthAsync_WithUnauthorizedResponse_ReturnsDegraded()
    {
        // Arrange
        var httpMessageHandler = CreateMockHttpMessageHandler(HttpStatusCode.Unauthorized);
        var httpClient = new HttpClient(httpMessageHandler) { BaseAddress = new Uri("https://openrouter.ai/") };

        _httpClientFactoryMock.Setup(f => f.CreateClient("openrouter")).Returns(httpClient);

        var healthCheck = new OpenRouterHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task CheckHealthAsync_WithTimeout_ReturnsDegraded()
    {
        // Arrange
        var httpMessageHandler = CreateMockHttpMessageHandlerWithDelay(TimeSpan.FromSeconds(10));
        var httpClient = new HttpClient(httpMessageHandler) { BaseAddress = new Uri("https://openrouter.ai/") };

        _httpClientFactoryMock.Setup(f => f.CreateClient("openrouter")).Returns(httpClient);

        var healthCheck = new OpenRouterHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("Timeout");
    }

    [Fact]
    public async Task CheckHealthAsync_WithNetworkError_ReturnsUnhealthy()
    {
        // Arrange
        var httpMessageHandler = CreateMockHttpMessageHandlerWithException(new HttpRequestException("Network error"));
        var httpClient = new HttpClient(httpMessageHandler) { BaseAddress = new Uri("https://openrouter.ai/") };

        _httpClientFactoryMock.Setup(f => f.CreateClient("openrouter")).Returns(httpClient);

        var healthCheck = new OpenRouterHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("OpenRouter API unavailable");
        result.Exception.Should().NotBeNull();
    }

    private static HttpMessageHandler CreateMockHttpMessageHandler(HttpStatusCode statusCode)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent("{}")
            });

        return handlerMock.Object;
    }

    private static HttpMessageHandler CreateMockHttpMessageHandlerWithDelay(TimeSpan delay)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async () =>
            {
                await Task.Delay(delay);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        return handlerMock.Object;
    }

    private static HttpMessageHandler CreateMockHttpMessageHandlerWithException(Exception exception)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        return handlerMock.Object;
    }
}
