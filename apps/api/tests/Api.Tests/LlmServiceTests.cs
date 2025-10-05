using System.Net;
using System.Net.Http;
using System.Text.Json;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class LlmServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<LlmService>> _loggerMock;

    public LlmServiceTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key"
            })
            .Build();

        _loggerMock = new Mock<ILogger<LlmService>>();
    }

    [Fact]
    public async Task GenerateCompletionAsync_ReturnsFailure_WhenUserPromptIsEmpty()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var service = CreateService(handler);

        // Act
        var result = await service.GenerateCompletionAsync("system", "   ");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No user prompt provided", result.ErrorMessage);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GenerateCompletionAsync_ReturnsSuccess_WhenApiRespondsWithChoice()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = "Generated response"
                        }
                    }
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            });
        });

        var service = CreateService(handler);

        // Act
        var result = await service.GenerateCompletionAsync("system prompt", "user prompt");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Generated response", result.Response);

        var request = Assert.Single(handler.Requests);
        AssertRequestHeaders(request);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", request.RequestUri!.ToString());

        var body = handler.RequestBodies.Single();
        Assert.NotNull(body);

        using var document = JsonDocument.Parse(body!);
        var root = document.RootElement;
        Assert.Equal("anthropic/claude-3.5-sonnet", root.GetProperty("model").GetString());
        Assert.Equal(0.3, root.GetProperty("temperature").GetDouble());
    }

    [Fact]
    public async Task GenerateCompletionAsync_ReturnsFailure_WhenApiRespondsWithError()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"boom\"}")
            }));

        var service = CreateService(handler);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("API error: InternalServerError", result.ErrorMessage);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("OpenRouter chat API error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        var request = Assert.Single(handler.Requests);
        AssertRequestHeaders(request);
    }

    [Fact]
    public async Task GenerateCompletionAsync_ReturnsFailure_WhenApiReturnsNoChoices()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[]}")
            }));

        var service = CreateService(handler);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("No response returned from API", result.ErrorMessage);

        var request = Assert.Single(handler.Requests);
        AssertRequestHeaders(request);
    }

    [Fact]
    public async Task GenerateCompletionAsync_ReturnsFailure_WhenRequestTimesOut()
    {
        // Arrange
        var handler = new TestHttpMessageHandler(async (_, _) =>
        {
            await Task.Yield();
            throw new TaskCanceledException("Request timed out");
        });

        var service = CreateService(handler);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Request timed out", result.ErrorMessage);

        var request = Assert.Single(handler.Requests);
        AssertRequestHeaders(request);
    }

    private LlmService CreateService(TestHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(httpClient);
        return new LlmService(factoryMock.Object, _configuration, _loggerMock.Object);
    }

    private void AssertRequestHeaders(HttpRequestMessage request)
    {
        Assert.True(request.Headers.TryGetValues("Authorization", out var authorizationValues));
        Assert.Equal("Bearer test-api-key", Assert.Single(authorizationValues));

        Assert.True(request.Headers.TryGetValues("HTTP-Referer", out var refererValues));
        Assert.Equal("https://meepleai.app", Assert.Single(refererValues));
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string?> RequestBodies { get; } = new();

        public TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.Content is not null)
            {
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }
            else
            {
                RequestBodies.Add(null);
            }

            return await _handler(request, cancellationToken);
        }
    }
}
