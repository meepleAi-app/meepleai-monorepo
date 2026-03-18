using System.Net;
using System.Text.Json;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Infrastructure;

/// <summary>
/// Unit tests for SshTunnelClient – HTTP-based SSH tunnel sidecar communication.
/// Uses mocked HttpMessageHandler to verify request/response mapping.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SshTunnelClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:2222";
    private const string AuthToken = "test-token";

    public SshTunnelClientTests()
    {
        _httpClient = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri(BaseUrl) };
    }

    private SshTunnelClient CreateClient() =>
        new(_httpClient, AuthToken, NullLogger<SshTunnelClient>.Instance);

    [Fact]
    public async Task GetStatusAsync_ReturnsOpenStatus_WhenSidecarRespondsOpen()
    {
        // Arrange
        SetupResponse(HttpMethod.Get, "/status", new { status = "open", uptime_seconds = 120, message = "Tunnel open" });

        // Act
        var result = await CreateClient().GetStatusAsync();

        // Assert
        Assert.Equal(TunnelState.Open, result.Status);
        Assert.Equal(120, result.UptimeSeconds);
        Assert.Equal("Tunnel open", result.Message);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsClosedStatus_WhenSidecarRespondsClosed()
    {
        SetupResponse(HttpMethod.Get, "/status", new { status = "closed", uptime_seconds = 0, message = (string?)null });

        var result = await CreateClient().GetStatusAsync();

        Assert.Equal(TunnelState.Closed, result.Status);
        Assert.Equal(0, result.UptimeSeconds);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsError_WhenSidecarUnreachable()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var result = await CreateClient().GetStatusAsync();

        // Assert
        Assert.Equal(TunnelState.Error, result.Status);
        Assert.Contains("Connection refused", result.Message);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsError_WhenSidecarReturnsNon200()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await CreateClient().GetStatusAsync();

        Assert.Equal(TunnelState.Error, result.Status);
    }

    [Fact]
    public async Task OpenAsync_ReturnsOpening_WhenSidecarAccepts()
    {
        SetupResponse(HttpMethod.Post, "/open", new { status = "opening", uptime_seconds = 0, message = "Connecting..." });

        var result = await CreateClient().OpenAsync();

        Assert.Equal(TunnelState.Opening, result.Status);
        Assert.Equal("Connecting...", result.Message);
    }

    [Fact]
    public async Task OpenAsync_ReturnsError_WhenSidecarFails()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await CreateClient().OpenAsync();

        Assert.Equal(TunnelState.Error, result.Status);
    }

    [Fact]
    public async Task CloseAsync_ReturnsClosed()
    {
        SetupResponse(HttpMethod.Delete, "/close", new { status = "closed", uptime_seconds = 0, message = (string?)null });

        var result = await CreateClient().CloseAsync();

        Assert.Equal(TunnelState.Closed, result.Status);
    }

    [Fact]
    public async Task CloseAsync_ReturnsError_WhenSidecarFails()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var result = await CreateClient().CloseAsync();

        Assert.Equal(TunnelState.Error, result.Status);
        Assert.Contains("Timeout", result.Message);
    }

    [Fact]
    public async Task Requests_IncludeBearerAuthHeader()
    {
        HttpRequestMessage? capturedRequest = null;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { status = "closed", uptime_seconds = 0, message = "" }),
                    System.Text.Encoding.UTF8, "application/json")
            });

        await CreateClient().GetStatusAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal(AuthToken, capturedRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task GetStatusAsync_MapsUnknownStatus_ToError()
    {
        SetupResponse(HttpMethod.Get, "/status", new { status = "unknown_state", uptime_seconds = 0, message = "wat" });

        var result = await CreateClient().GetStatusAsync();

        Assert.Equal(TunnelState.Error, result.Status);
    }

    private void SetupResponse(HttpMethod method, string path, object body)
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == method && r.RequestUri!.AbsolutePath == path),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
    }
}
