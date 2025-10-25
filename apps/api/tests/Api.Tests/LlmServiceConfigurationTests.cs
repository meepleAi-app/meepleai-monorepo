using System.Net;
using System.Text.Json;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// CONFIG-03: Unit tests for LlmService dynamic configuration integration.
/// Tests the fallback chain: Database → appsettings.json → hardcoded defaults.
/// </summary>
public class LlmServiceConfigurationTests
{
    private readonly Mock<ILogger<LlmService>> _loggerMock;
    private readonly Mock<IConfigurationService> _configServiceMock;
    private readonly IConfiguration _configuration;

    public LlmServiceConfigurationTests()
    {
        _loggerMock = new Mock<ILogger<LlmService>>();
        _configServiceMock = new Mock<IConfigurationService>();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key",
                // appsettings.json fallback values
                ["AI:Model"] = "appsettings-model",
                ["AI:Temperature"] = "0.7",
                ["AI:MaxTokens"] = "1000",
                ["AI:TimeoutSeconds"] = "120"
            })
            .Build();
    }

    #region Database Configuration Tests (Highest Priority)

    [Fact]
    public async Task GenerateCompletionAsync_UsesDatabaseConfiguration_WhenAvailable()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<string>("AI.Model", default, default))
            .ReturnsAsync("database-model");
        _configServiceMock.Setup(x => x.GetValueAsync<double?>("AI.Temperature", default, default))
            .ReturnsAsync(0.5);
        _configServiceMock.Setup(x => x.GetValueAsync<int?>("AI.MaxTokens", default, default))
            .ReturnsAsync(750);

        var handler = CreateSuccessHandler();
        var service = CreateService(handler, _configServiceMock.Object, _configuration);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.True(result.Success);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        Assert.Equal("database-model", root.GetProperty("model").GetString());
        Assert.Equal(0.5, root.GetProperty("temperature").GetDouble());
        Assert.Equal(750, root.GetProperty("max_tokens").GetInt32());

        // Verify database was queried
        _configServiceMock.Verify(x => x.GetValueAsync<string>("AI.Model", default, default), Times.Once);
        _configServiceMock.Verify(x => x.GetValueAsync<double?>("AI.Temperature", default, default), Times.Once);
        _configServiceMock.Verify(x => x.GetValueAsync<int?>("AI.MaxTokens", default, default), Times.Once);
    }

    [Fact]
    public async Task GenerateCompletionStreamAsync_UsesDatabaseConfiguration_WhenAvailable()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<string>("AI.Model", default, default))
            .ReturnsAsync("database-streaming-model");
        _configServiceMock.Setup(x => x.GetValueAsync<double?>("AI.Temperature", default, default))
            .ReturnsAsync(0.8);
        _configServiceMock.Setup(x => x.GetValueAsync<int?>("AI.MaxTokens", default, default))
            .ReturnsAsync(600);

        var handler = CreateStreamingSuccessHandler();
        var service = CreateService(handler, _configServiceMock.Object, _configuration);

        // Act
        var tokens = new List<string>();
        await foreach (var token in service.GenerateCompletionStreamAsync("system", "user prompt"))
        {
            tokens.Add(token);
        }

        // Assert
        Assert.NotEmpty(tokens);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        Assert.Equal("database-streaming-model", root.GetProperty("model").GetString());
        Assert.Equal(0.8, root.GetProperty("temperature").GetDouble());
        Assert.Equal(600, root.GetProperty("max_tokens").GetInt32());
        Assert.True(root.GetProperty("stream").GetBoolean());
    }

    #endregion

    #region appsettings.json Fallback Tests (Medium Priority)

    [Fact]
    public async Task GenerateCompletionAsync_UsesAppsettingsConfiguration_WhenDatabaseReturnsNull()
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<string>("AI.Model", default, default))
            .ReturnsAsync((string?)null);
        _configServiceMock.Setup(x => x.GetValueAsync<double?>("AI.Temperature", default, default))
            .ReturnsAsync((double?)null);
        _configServiceMock.Setup(x => x.GetValueAsync<int?>("AI.MaxTokens", default, default))
            .ReturnsAsync((int?)null);

        var handler = CreateSuccessHandler();
        var service = CreateService(handler, _configServiceMock.Object, _configuration);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.True(result.Success);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        Assert.Equal("appsettings-model", root.GetProperty("model").GetString());
        Assert.Equal(0.7, root.GetProperty("temperature").GetDouble());
        Assert.Equal(1000, root.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task GenerateCompletionAsync_UsesAppsettingsConfiguration_WhenConfigServiceIsNull()
    {
        // Arrange
        var handler = CreateSuccessHandler();
        var service = CreateService(handler, configService: null, _configuration);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.True(result.Success);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        Assert.Equal("appsettings-model", root.GetProperty("model").GetString());
        Assert.Equal(0.7, root.GetProperty("temperature").GetDouble());
        Assert.Equal(1000, root.GetProperty("max_tokens").GetInt32());
    }

    #endregion

    #region Hardcoded Defaults Tests (Lowest Priority)

    [Fact]
    public async Task GenerateCompletionAsync_UsesHardcodedDefaults_WhenNoDatabaseOrAppsettings()
    {
        // Arrange
        var configWithoutAiSettings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key"
            })
            .Build();

        var handler = CreateSuccessHandler();
        var service = CreateService(handler, configService: null, configWithoutAiSettings);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.True(result.Success);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        // Verify hardcoded defaults from LlmService
        Assert.Equal("deepseek/deepseek-chat-v3.1", root.GetProperty("model").GetString());
        Assert.Equal(0.3, root.GetProperty("temperature").GetDouble());
        Assert.Equal(500, root.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task GenerateCompletionStreamAsync_UsesHardcodedDefaults_WhenNoDatabaseOrAppsettings()
    {
        // Arrange
        var configWithoutAiSettings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key"
            })
            .Build();

        var handler = CreateStreamingSuccessHandler();
        var service = CreateService(handler, configService: null, configWithoutAiSettings);

        // Act
        var tokens = new List<string>();
        await foreach (var token in service.GenerateCompletionStreamAsync("system", "user prompt"))
        {
            tokens.Add(token);
        }

        // Assert
        Assert.NotEmpty(tokens);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        Assert.Equal("deepseek/deepseek-chat-v3.1", root.GetProperty("model").GetString());
        Assert.Equal(0.3, root.GetProperty("temperature").GetDouble());
        Assert.Equal(500, root.GetProperty("max_tokens").GetInt32());
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(-0.1)] // Below minimum
    [InlineData(2.1)]  // Above maximum
    [InlineData(3.0)]  // Way above maximum
    public async Task GenerateCompletionAsync_UsesDefaultTemperature_WhenDatabaseValueOutOfRange(double invalidTemp)
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<string>("AI.Model", default, default))
            .ReturnsAsync("test-model");
        _configServiceMock.Setup(x => x.GetValueAsync<double?>("AI.Temperature", default, default))
            .ReturnsAsync(invalidTemp);
        _configServiceMock.Setup(x => x.GetValueAsync<int?>("AI.MaxTokens", default, default))
            .ReturnsAsync(500);

        var handler = CreateSuccessHandler();
        var service = CreateService(handler, _configServiceMock.Object, _configuration);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.True(result.Success);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        // Should use hardcoded default (0.3) due to validation failure
        Assert.Equal(0.3, root.GetProperty("temperature").GetDouble());

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("temperature") && v.ToString()!.Contains("out of range")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0)]      // Zero
    [InlineData(-100)]   // Negative
    [InlineData(50000)]  // Above maximum (32000)
    public async Task GenerateCompletionAsync_ValidatesMaxTokens_AndCapsOrUsesDefault(int invalidMaxTokens)
    {
        // Arrange
        _configServiceMock.Setup(x => x.GetValueAsync<string>("AI.Model", default, default))
            .ReturnsAsync("test-model");
        _configServiceMock.Setup(x => x.GetValueAsync<double?>("AI.Temperature", default, default))
            .ReturnsAsync(0.3);
        _configServiceMock.Setup(x => x.GetValueAsync<int?>("AI.MaxTokens", default, default))
            .ReturnsAsync(invalidMaxTokens);

        var handler = CreateSuccessHandler();
        var service = CreateService(handler, _configServiceMock.Object, _configuration);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.True(result.Success);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        var actualMaxTokens = root.GetProperty("max_tokens").GetInt32();

        if (invalidMaxTokens <= 0)
        {
            // Should use default for invalid values
            Assert.Equal(500, actualMaxTokens);
        }
        else
        {
            // Should cap at upper bound
            Assert.Equal(32000, actualMaxTokens);
        }

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("max_tokens")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Mixed Fallback Tests (Realistic Scenarios)

    [Fact]
    public async Task GenerateCompletionAsync_MixesDatabaseAndAppsettings_WhenDatabasePartiallyAvailable()
    {
        // Arrange - Only model in database, temperature and maxTokens from appsettings
        _configServiceMock.Setup(x => x.GetValueAsync<string>("AI.Model", default, default))
            .ReturnsAsync("database-only-model");
        _configServiceMock.Setup(x => x.GetValueAsync<double?>("AI.Temperature", default, default))
            .ReturnsAsync((double?)null);
        _configServiceMock.Setup(x => x.GetValueAsync<int?>("AI.MaxTokens", default, default))
            .ReturnsAsync((int?)null);

        var handler = CreateSuccessHandler();
        var service = CreateService(handler, _configServiceMock.Object, _configuration);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.True(result.Success);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        Assert.Equal("database-only-model", root.GetProperty("model").GetString()); // From DB
        Assert.Equal(0.7, root.GetProperty("temperature").GetDouble());             // From appsettings
        Assert.Equal(1000, root.GetProperty("max_tokens").GetInt32());             // From appsettings
    }

    [Fact]
    public async Task GenerateCompletionAsync_MixesDatabaseAppsettingsAndDefaults_InComplexScenario()
    {
        // Arrange - Model from DB, temperature from appsettings (DB validation failed), maxTokens from default
        var configWithoutMaxTokens = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-api-key",
                ["AI:Model"] = "appsettings-fallback-model",
                ["AI:Temperature"] = "0.9"
                // AI:MaxTokens intentionally missing
            })
            .Build();

        _configServiceMock.Setup(x => x.GetValueAsync<string>("AI.Model", default, default))
            .ReturnsAsync("database-primary-model");
        _configServiceMock.Setup(x => x.GetValueAsync<double?>("AI.Temperature", default, default))
            .ReturnsAsync(5.0); // Invalid, will trigger validation fallback
        _configServiceMock.Setup(x => x.GetValueAsync<int?>("AI.MaxTokens", default, default))
            .ReturnsAsync((int?)null);

        var handler = CreateSuccessHandler();
        var service = CreateService(handler, _configServiceMock.Object, configWithoutMaxTokens);

        // Act
        var result = await service.GenerateCompletionAsync("system", "user prompt");

        // Assert
        Assert.True(result.Success);

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        Assert.Equal("database-primary-model", root.GetProperty("model").GetString()); // From DB
        Assert.Equal(0.3, root.GetProperty("temperature").GetDouble());               // From default (DB validation failed)
        Assert.Equal(500, root.GetProperty("max_tokens").GetInt32());                 // From default (not in appsettings)
    }

    #endregion

    #region Helper Methods

    private LlmService CreateService(
        TestHttpMessageHandler handler,
        IConfigurationService? configService = null,
        IConfiguration? configuration = null)
    {
        var httpClient = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(httpClient);

        return new LlmService(
            factoryMock.Object,
            configuration ?? _configuration,
            _loggerMock.Object,
            configService,
            configuration ?? _configuration);
    }

    private TestHttpMessageHandler CreateSuccessHandler()
    {
        return new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                id = "resp_123",
                model = "test-model-response",
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Generated response" },
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
    }

    private TestHttpMessageHandler CreateStreamingSuccessHandler()
    {
        return new TestHttpMessageHandler((_, _) =>
        {
            var sseResponse = @"data: {""id"":""resp_stream"",""choices"":[{""delta"":{""content"":""Token1""}}]}

data: {""id"":""resp_stream"",""choices"":[{""delta"":{""content"":""Token2""}}]}

data: [DONE]
";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sseResponse)
            });
        });
    }

    #endregion

    #region Test Infrastructure

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

    #endregion
}
