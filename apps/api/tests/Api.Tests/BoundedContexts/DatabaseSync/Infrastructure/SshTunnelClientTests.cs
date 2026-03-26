using System.Net;
using System.Text.Json;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;
using FluentAssertions;

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
        result.Status.Should().Be(TunnelState.Open);
        result.UptimeSeconds.Should().Be(120);
        result.Message.Should().Be("Tunnel open");
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsClosedStatus_WhenSidecarRespondsClosed()
    {
        SetupResponse(HttpMethod.Get, "/status", new { status = "closed", uptime_seconds = 0, message = (string?)null });

        var result = await CreateClient().GetStatusAsync();

        result.Status.Should().Be(TunnelState.Closed);
        result.UptimeSeconds.Should().Be(0);
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
        result.Status.Should().Be(TunnelState.Error);
        result.Message.Should().Contain("Connection refused");
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

        result.Status.Should().Be(TunnelState.Error);
    }

    [Fact]
    public async Task OpenAsync_ReturnsOpening_WhenSidecarAccepts()
    {
        SetupResponse(HttpMethod.Post, "/open", new { status = "opening", uptime_seconds = 0, message = "Connecting..." });

        var result = await CreateClient().OpenAsync();

        result.Status.Should().Be(TunnelState.Opening);
        result.Message.Should().Be("Connecting...");
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

        result.Status.Should().Be(TunnelState.Error);
    }

    [Fact]
    public async Task CloseAsync_ReturnsClosed()
    {
        SetupResponse(HttpMethod.Delete, "/close", new { status = "closed", uptime_seconds = 0, message = (string?)null });

        var result = await CreateClient().CloseAsync();

        result.Status.Should().Be(TunnelState.Closed);
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

        result.Status.Should().Be(TunnelState.Error);
        result.Message.Should().Contain("Timeout");
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

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization?.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization?.Parameter.Should().Be(AuthToken);
    }

    [Fact]
    public async Task GetStatusAsync_MapsUnknownStatus_ToError()
    {
        SetupResponse(HttpMethod.Get, "/status", new { status = "unknown_state", uptime_seconds = 0, message = "wat" });

        var result = await CreateClient().GetStatusAsync();

        result.Status.Should().Be(TunnelState.Error);
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
