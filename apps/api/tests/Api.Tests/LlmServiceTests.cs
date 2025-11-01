using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

public class LlmServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<LlmService>> _loggerMock;

    public LlmServiceTests(ITestOutputHelper output)
    {
        _output = output;
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo("No user prompt provided");
        handler.Requests.Should().BeEmpty();
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
        result.Success.Should().BeTrue();
        result.Response.Should().BeEquivalentTo("Generated response");
        result.Usage.PromptTokens.Should().Be(12);
        result.Usage.CompletionTokens.Should().Be(8);
        result.Usage.TotalTokens.Should().Be(20);
        result.Metadata["model"].Should().Be("anthropic/claude-3.5-sonnet");
        result.Metadata["finish_reason"].Should().Be("stop");
        result.Metadata["response_id"].Should().Be("resp_123");

        var request = handler.Requests.Should().ContainSingle().Subject;
        AssertRequestHeaders(request);
        request.RequestUri!.ToString().Should().Be("https://openrouter.ai/api/v1/chat/completions");

        var body = handler.RequestBodies.Single();
        body.Should().NotBeNull();

        using var document = JsonDocument.Parse(body!);
        var root = document.RootElement;
        root.GetProperty("model").GetString().Should().Be("deepseek/deepseek-chat-v3.1");
        root.GetProperty("temperature").GetDouble().Should().Be(0.3);
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo("API error: InternalServerError");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("OpenRouter chat API error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        var request = handler.Requests.Should().ContainSingle().Subject;
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo("No response returned from API");

        var request = handler.Requests.Should().ContainSingle().Subject;
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo("Request timed out");

        var request = handler.Requests.Should().ContainSingle().Subject;
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
        request.Headers.TryGetValues("Authorization", out var authorizationValues).Should().BeTrue();
        authorizationValues.Should().ContainSingle().Which.Should().Be("Bearer test-api-key");

        request.Headers.TryGetValues("HTTP-Referer", out var refererValues).Should().BeTrue();
        refererValues.Should().ContainSingle().Which.Should().Be("https://meepleai.app");
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
        result.Should().BeNull();

        // Verify warning was logged for JSON parsing failure
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to parse LLM JSON response")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        var request = handler.Requests.Should().ContainSingle().Subject;
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
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeEquivalentTo("API error: BadRequest");

        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("OpenRouter chat API error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        var request = handler.Requests.Should().ContainSingle().Subject;
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
        tokens.Should().BeEmpty();

        var request = handler.Requests.Should().ContainSingle().Subject;
        AssertRequestHeaders(request);

        // Verify streaming was initiated
        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;
        root.GetProperty("stream").GetBoolean().Should().BeTrue();
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
        tokens.Should().NotBeEmpty();
        tokens.Count.Should().BeLessThan(4, "Expected partial tokens (< 4), got {0}", tokens.Count);

        var request = handler.Requests.Should().ContainSingle().Subject;
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
        result.Should().NotBeNull();
        result.Name.Should().BeEquivalentTo("Test Game");
        result.Players.Should().Be(2);
        result.Complexity.Should().BeEquivalentTo("Medium");

        var request = handler.Requests.Should().ContainSingle().Subject;
        AssertRequestHeaders(request);
    }

    /// <summary>
    /// TEST-02-P4: Tests that GenerateCompletionAsync works correctly when systemPrompt is null.
    /// Verifies the request payload only contains the user message without a system message.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_WithNullSystemPrompt_UsesOnlyUserPrompt()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                id = "resp_null_sys",
                model = "test-model",
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Response without system prompt" },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 5, completion_tokens = 3, total_tokens = 8 }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            });
        });

        var service = CreateService(handler);

        // Act
        var result = await service.GenerateCompletionAsync(null!, "user prompt");

        // Assert
        result.Success.Should().BeTrue();
        result.Response.Should().BeEquivalentTo("Response without system prompt");

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;
        var messages = root.GetProperty("messages");

        // Should only have user message, no system message
        messages.GetArrayLength().Should().Be(1);
        messages[0].GetProperty("role").GetString().Should().Be("user");
        messages[0].GetProperty("content").GetString().Should().Be("user prompt");
    }

    /// <summary>
    /// TEST-02-P4: Tests that GenerateCompletionStreamAsync skips malformed SSE chunks and yields valid tokens.
    /// Simulates a stream with invalid JSON interspersed with valid chunks.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionStreamAsync_WithMalformedSSE_SkipsInvalidChunks()
    {
        // Arrange
        var sseResponse = @"data: {""choices"":[{""delta"":{""content"":""Token1""}}]}

data: {invalid json without quotes

data: {""choices"":[{""delta"":{""content"":""Token2""}}]}

data: malformed

data: {""choices"":[{""delta"":{""content"":""Token3""}}]}

data: [DONE]

";

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
        // Should only yield valid tokens, skipping malformed chunks
        tokens.Count.Should().Be(3);
        tokens[0].Should().Be("Token1");
        tokens[1].Should().Be("Token2");
        tokens[2].Should().Be("Token3");

        // Verify warning was logged for malformed chunks (2 times)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to parse streaming chunk")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    /// <summary>
    /// TEST-02-P4: Tests that GenerateCompletionStreamAsync yields nothing when API returns HTTP error during streaming.
    /// Verifies graceful handling of 500 Internal Server Error.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionStreamAsync_WithHTTPError_YieldsNothingAndLogs()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"Internal server error\"}")
            }));

        var service = CreateService(handler);

        // Act
        var tokens = await ConvertAsyncEnumerableToList(
            service.GenerateCompletionStreamAsync("system", "user prompt"));

        // Assert
        tokens.Should().BeEmpty();

        // Verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("OpenRouter streaming API error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// TEST-02-P4: Tests that GenerateJsonAsync returns null when LLM returns success but empty content.
    /// Verifies graceful handling of edge case where API succeeds but produces no response text.
    /// </summary>
    [Fact]
    public async Task GenerateJsonAsync_WithNullLlmResponse_ReturnsNull()
    {
        // Arrange
        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                id = "resp_empty",
                model = "test-model",
                choices = new[]
                {
                    new
                    {
                        message = new { content = "" }, // Empty content
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 10, completion_tokens = 0, total_tokens = 10 }
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
        result.Should().BeNull();

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("LLM completion failed or returned empty response")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// TEST-02-P4: Tests that GenerateJsonAsync handles missing properties gracefully with default values.
    /// C#'s JSON deserializer (with PropertyNameCaseInsensitive) is tolerant and sets missing properties to defaults.
    /// </summary>
    [Fact]
    public async Task GenerateJsonAsync_WithMissingProperties_ReturnsObjectWithDefaults()
    {
        // Arrange
        var incompleteJson = "{\"wrongField\":\"value\",\"unexpectedType\":true}";

        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                id = "resp_partial",
                model = "test-model",
                choices = new[]
                {
                    new
                    {
                        message = new { content = incompleteJson },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
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
        result.Should().NotBeNull(); // Deserializer creates object with default values
        result.Name.Should().BeEquivalentTo(string.Empty);
        result.Players.Should().Be(0);
        result.Complexity.Should().BeEquivalentTo(string.Empty);
    }

    /// <summary>
    /// AI-15-ALT: Tests that model selection uses default model when alternative model is disabled.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_WithAlternativeModelDisabled_UsesDefaultModel()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key",
                ["LlmService:UseAlternativeModel"] = "false",
                ["LlmService:AlternativeModelTrafficPercentage"] = "0"
            })
            .Build();

        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                id = "resp_default",
                model = "deepseek/deepseek-chat-v3.1",
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Response from default model" },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            });
        });

        var service = CreateServiceWithConfig(handler, config);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        result.Success.Should().BeTrue();

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;
        root.GetProperty("model").GetString().Should().Be("deepseek/deepseek-chat-v3.1");
    }

    /// <summary>
    /// AI-15-ALT: Tests that model selection uses alternative model when feature flag is enabled.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_WithUseAlternativeModelEnabled_UsesAlternativeModel()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key",
                ["LlmService:AlternativeModelId"] = "openai/gpt-4o-mini",
                ["LlmService:UseAlternativeModel"] = "true",
                ["LlmService:AlternativeModelTrafficPercentage"] = "0"
            })
            .Build();

        var handler = new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                id = "resp_alternative",
                model = "openai/gpt-4o-mini",
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Response from alternative model" },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            });
        });

        var service = CreateServiceWithConfig(handler, config);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        result.Success.Should().BeTrue();

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;
        root.GetProperty("model").GetString().Should().Be("openai/gpt-4o-mini");
    }

    /// <summary>
    /// AI-15-ALT: Tests that A/B testing distributes traffic between models based on percentage.
    /// With 50% traffic, expect roughly half of requests to use alternative model.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_WithABTesting50Percent_DistributesTraffic()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key",
                ["LlmService:AlternativeModelId"] = "openai/gpt-4o-mini",
                ["LlmService:UseAlternativeModel"] = "false",
                ["LlmService:AlternativeModelTrafficPercentage"] = "50"
            })
            .Build();

        var requestedModels = new List<string>();
        var handler = new TestHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            var model = document.RootElement.GetProperty("model").GetString()!;
            requestedModels.Add(model);

            var payload = new
            {
                id = "resp_ab_test",
                model = model,
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Response" },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            };
        });

        var service = CreateServiceWithConfig(handler, config);

        // Act - Make 100 requests to test distribution
        for (int i = 0; i < 100; i++)
        {
            await service.GenerateCompletionAsync("system", $"prompt {i}");
        }

        // Assert
        var alternativeCount = requestedModels.Count(m => m == "openai/gpt-4o-mini");
        var defaultCount = requestedModels.Count(m => m == "deepseek/deepseek-chat-v3.1");

        // With 50% traffic and 100 requests, expect 40-60 alternative model requests (allowing variance)
        alternativeCount.Should().BeApproximately(30, TimeSpan.FromSeconds(5));
        defaultCount.Should().BeApproximately(30, TimeSpan.FromSeconds(5));
        requestedModels.Count.Should().Be(100);
    }

    /// <summary>
    /// AI-15-ALT: Tests that 0% traffic percentage always uses default model.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_With0PercentTraffic_AlwaysUsesDefaultModel()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key",
                ["LlmService:AlternativeModelId"] = "openai/gpt-4o-mini",
                ["LlmService:UseAlternativeModel"] = "false",
                ["LlmService:AlternativeModelTrafficPercentage"] = "0"
            })
            .Build();

        var requestedModels = new List<string>();
        var handler = new TestHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            var model = document.RootElement.GetProperty("model").GetString()!;
            requestedModels.Add(model);

            var payload = new
            {
                id = "resp_0_percent",
                model = model,
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Response" },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            };
        });

        var service = CreateServiceWithConfig(handler, config);

        // Act - Make 50 requests
        for (int i = 0; i < 50; i++)
        {
            await service.GenerateCompletionAsync("system", $"prompt {i}");
        }

        // Assert
        requestedModels.Should().OnlyContain(model => model == "deepseek/deepseek-chat-v3.1");
        requestedModels.Count.Should().Be(50);
    }

    /// <summary>
    /// AI-15-ALT: Tests that 100% traffic percentage always uses alternative model.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_With100PercentTraffic_AlwaysUsesAlternativeModel()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key",
                ["LlmService:AlternativeModelId"] = "openai/gpt-4o-mini",
                ["LlmService:UseAlternativeModel"] = "false",
                ["LlmService:AlternativeModelTrafficPercentage"] = "100"
            })
            .Build();

        var requestedModels = new List<string>();
        var handler = new TestHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            var model = document.RootElement.GetProperty("model").GetString()!;
            requestedModels.Add(model);

            var payload = new
            {
                id = "resp_100_percent",
                model = model,
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Response" },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            };
        });

        var service = CreateServiceWithConfig(handler, config);

        // Act - Make 50 requests
        for (int i = 0; i < 50; i++)
        {
            await service.GenerateCompletionAsync("system", $"prompt {i}");
        }

        // Assert
        requestedModels.Should().OnlyContain(model => model == "openai/gpt-4o-mini");
        requestedModels.Count.Should().Be(50);
    }

    /// <summary>
    /// AI-15-ALT: Tests that feature flag takes precedence over traffic percentage when both are set.
    /// Feature flag override ensures 100% alternative model usage even if A/B test is configured.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_WithBothFlagAndTraffic_FeatureFlagTakesPrecedence()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key",
                ["LlmService:AlternativeModelId"] = "openai/gpt-4o-mini",
                ["LlmService:UseAlternativeModel"] = "true",  // Feature flag enabled
                ["LlmService:AlternativeModelTrafficPercentage"] = "50"  // A/B test also configured
            })
            .Build();

        var requestedModels = new List<string>();
        var handler = new TestHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            using var document = JsonDocument.Parse(body);
            var model = document.RootElement.GetProperty("model").GetString()!;
            requestedModels.Add(model);

            var payload = new
            {
                id = "resp_precedence",
                model = model,
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Response" },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload))
            };
        });

        var service = CreateServiceWithConfig(handler, config);

        // Act - Make 20 requests to verify feature flag override is consistent
        for (int i = 0; i < 20; i++)
        {
            await service.GenerateCompletionAsync("system", $"prompt {i}");
        }

        // Assert - Feature flag should ALWAYS override A/B test, using alternative 100% of the time
        requestedModels.Should().OnlyContain(model => model == "openai/gpt-4o-mini");
        requestedModels.Count.Should().Be(20);
    }

    /// <summary>
    /// AI-15-ALT: Tests that streaming method also respects model selection.
    /// </summary>
    [Fact]
    public async Task GenerateCompletionStreamAsync_WithAlternativeModel_UsesCorrectModel()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key",
                ["LlmService:AlternativeModelId"] = "openai/gpt-4o-mini",
                ["LlmService:UseAlternativeModel"] = "true",
                ["LlmService:AlternativeModelTrafficPercentage"] = "0"
            })
            .Build();

        var sseResponse = @"data: {""choices"":[{""delta"":{""content"":""Token""}}]}

data: [DONE]

";

        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sseResponse, Encoding.UTF8, "text/event-stream")
            }));

        var service = CreateServiceWithConfig(handler, config);

        // Act
        var tokens = await ConvertAsyncEnumerableToList(
            service.GenerateCompletionStreamAsync("system", "user prompt"));

        // Assert
        tokens.Should().ContainSingle();
        tokens[0].Should().Be("Token");

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;
        root.GetProperty("model").GetString().Should().Be("openai/gpt-4o-mini");
        root.GetProperty("stream").GetBoolean().Should().BeTrue();
    }

    private LlmService CreateServiceWithConfig(TestHttpMessageHandler handler, IConfiguration config)
    {
        var httpClient = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(httpClient);
        return new LlmService(factoryMock.Object, config, _loggerMock.Object);
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
