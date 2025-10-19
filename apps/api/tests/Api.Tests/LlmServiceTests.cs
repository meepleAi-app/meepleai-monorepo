using System.Net;
using System.Net.Http;
using System.Text;
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
                id = "resp_123",
                model = "anthropic/claude-3.5-sonnet",
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = "Generated response"
                        },
                        finish_reason = "stop"
                    }
                },
                usage = new
                {
                    prompt_tokens = 12,
                    completion_tokens = 8,
                    total_tokens = 20
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
        Assert.Equal(12, result.Usage.PromptTokens);
        Assert.Equal(8, result.Usage.CompletionTokens);
        Assert.Equal(20, result.Usage.TotalTokens);
        Assert.Equal("anthropic/claude-3.5-sonnet", result.Metadata["model"]);
        Assert.Equal("stop", result.Metadata["finish_reason"]);
        Assert.Equal("resp_123", result.Metadata["response_id"]);

        var request = Assert.Single(handler.Requests);
        AssertRequestHeaders(request);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", request.RequestUri!.ToString());

        var body = handler.RequestBodies.Single();
        Assert.NotNull(body);

        using var document = JsonDocument.Parse(body!);
        var root = document.RootElement;
        Assert.Equal("deepseek/deepseek-chat-v3.1", root.GetProperty("model").GetString());
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

    /// <summary>
    /// Tests that GenerateJsonAsync gracefully handles malformed JSON by returning null and logging a warning.
    /// Verifies the error handling path when LLM returns invalid JSON that cannot be deserialized.
    /// </summary>
    [Fact]
    public async Task GenerateJsonAsync_WithMalformedJson_ReturnsNull()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                id = "resp_456",
                model = "test-model",
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = "{invalid json, missing quotes and brackets"
                        },
                        finish_reason = "stop"
                    }
                },
                usage = new
                {
                    prompt_tokens = 10,
                    completion_tokens = 5,
                    total_tokens = 15
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            });
        });

        var service = CreateService(handler);

        // Act
        var result = await service.GenerateJsonAsync<TestJsonModel>("system", "user prompt");

        // Assert
        Assert.Null(result);

        // Verify warning was logged for JSON parsing failure
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to parse LLM JSON response")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        var request = Assert.Single(handler.Requests);
        AssertRequestHeaders(request);
    }

    /// <summary>
    /// Tests that GenerateCompletionAsync returns failure when API responds with context_length_exceeded error.
    /// Simulates scenario where prompt + max_tokens exceeds model's context window.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_WithContextLengthExceeded_ReturnsFailure()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var errorPayload = new
            {
                error = new
                {
                    message = "This model's maximum context length is 8192 tokens. However, you requested 12000 tokens.",
                    type = "invalid_request_error",
                    code = "context_length_exceeded"
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(JsonSerializer.Serialize(errorPayload))
            });
        });

        var service = CreateService(handler);

        // Act
        var result = await service.GenerateCompletionAsync("system prompt", "user prompt with very long context...");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("API error: BadRequest", result.ErrorMessage);

        // Verify error was logged
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

    /// <summary>
    /// Tests that GenerateCompletionStreamAsync completes successfully when stream contains only [DONE] marker with no tokens.
    /// Verifies graceful handling of empty streaming responses.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionStreamAsync_WithEmptyStream_CompletesWithoutTokens()
    {
        // Arrange
        var sseResponse = "data: [DONE]\n\n";

        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sseResponse, Encoding.UTF8, "text/event-stream")
            }));

        var service = CreateService(handler);

        // Act
        var tokens = await ConvertAsyncEnumerableToList(
            service.GenerateCompletionStreamAsync("system", "user prompt"));

        // Assert
        Assert.Empty(tokens);

        var request = Assert.Single(handler.Requests);
        AssertRequestHeaders(request);

        // Verify streaming was initiated
        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;
        Assert.True(root.GetProperty("stream").GetBoolean());
    }

    /// <summary>
    /// Tests that GenerateCompletionStreamAsync stops gracefully when cancellation is requested mid-stream.
    /// Verifies proper handling of CancellationToken and that partial tokens are yielded before cancellation.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionStreamAsync_WithCancellation_StopsGracefully()
    {
        // Arrange
        var sseResponse = @"data: {""choices"":[{""delta"":{""content"":""Token1""}}]}

data: {""choices"":[{""delta"":{""content"":""Token2""}}]}

data: {""choices"":[{""delta"":{""content"":""Token3""}}]}

data: {""choices"":[{""delta"":{""content"":""Token4""}}]}

data: [DONE]

";

        var handler = new TestHttpMessageHandler(async (_, ct) =>
        {
            // Simulate slow stream with delays between chunks
            await Task.Delay(50, ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sseResponse, Encoding.UTF8, "text/event-stream")
            };
        });

        var service = CreateService(handler);
        var cts = new CancellationTokenSource();

        // Act
        var tokens = new List<string>();
        var enumerator = service.GenerateCompletionStreamAsync("system", "user prompt", cts.Token).GetAsyncEnumerator(cts.Token);

        try
        {
            // Read first token
            if (await enumerator.MoveNextAsync())
            {
                tokens.Add(enumerator.Current);
            }

            // Cancel before reading more tokens
            await cts.CancelAsync();

            // Attempt to read more (should stop gracefully)
            while (await enumerator.MoveNextAsync())
            {
                tokens.Add(enumerator.Current);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - cancellation may throw OperationCanceledException
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        // Assert
        // Should have at least one token before cancellation, but not all 4
        Assert.NotEmpty(tokens);
        Assert.True(tokens.Count < 4, $"Expected partial tokens (< 4), got {tokens.Count}");

        var request = Assert.Single(handler.Requests);
        AssertRequestHeaders(request);
    }

    /// <summary>
    /// Tests that GenerateJsonAsync successfully extracts JSON from markdown code block wrappers.
    /// Verifies CleanJsonResponse strips ```json ... ``` formatting that LLMs commonly add.
    /// </summary>
    [Fact]
    public async Task GenerateJsonAsync_WithMarkdownCodeBlock_ExtractsJson()
    {
        // Arrange
        var jsonObject = new
        {
            name = "Test Game",
            players = 2,
            complexity = "Medium"
        };

        var markdownWrappedJson = $"""
```json
{JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions { WriteIndented = true })}
```
""";

        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                id = "resp_789",
                model = "test-model",
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = markdownWrappedJson
                        },
                        finish_reason = "stop"
                    }
                },
                usage = new
                {
                    prompt_tokens = 15,
                    completion_tokens = 10,
                    total_tokens = 25
                }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            });
        });

        var service = CreateService(handler);

        // Act
        var result = await service.GenerateJsonAsync<TestJsonModel>("system", "user prompt");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Game", result.Name);
        Assert.Equal(2, result.Players);
        Assert.Equal("Medium", result.Complexity);

        var request = Assert.Single(handler.Requests);
        AssertRequestHeaders(request);
    }

    /// <summary>
    /// Helper method to convert IAsyncEnumerable to List for easier testing of streaming responses.
    /// </summary>
    private static async Task<List<T>> ConvertAsyncEnumerableToList<T>(IAsyncEnumerable<T> asyncEnumerable)
    {
        var result = new List<T>();
        await foreach (var item in asyncEnumerable)
        {
            result.Add(item);
        }
        return result;
    }

    /// <summary>
    /// Test model for JSON deserialization tests
    /// </summary>
    private sealed class TestJsonModel
    {
        public string Name { get; set; } = string.Empty;
        public int Players { get; set; }
        public string Complexity { get; set; } = string.Empty;
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
