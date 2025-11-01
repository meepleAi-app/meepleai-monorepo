using System.Net;
using System.Net.Http;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// CONFIG-03: Integration tests for LlmService dynamic configuration with real database.
/// Tests the complete fallback chain using Testcontainers (PostgreSQL).
///
/// BDD Feature: Dynamic AI/LLM Configuration
/// As a system administrator
/// I want to configure AI/LLM parameters via database
/// So that I can tune model behavior without code changes or deployments
/// </summary>
public class LlmServiceConfigurationIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public LlmServiceConfigurationIntegrationTests(WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: LLM service uses database configuration for model selection
    ///   Given a configuration exists in the database for AI.Model
    ///   When GenerateCompletionAsync is called
    ///   Then the database model value is used
    ///   And appsettings and defaults are not consulted
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_UsesDatabaseModel_WhenConfigurationExists()
    {
        // Given: A configuration exists in the database for AI.Model
        using var scope = Factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        // Create test user for configuration ownership
        var testUser = await CreateTestUserAsync($"config-test-{TestRunId}@test.com");

        var testModel = "test-database-model-" + TestRunId;
        var configDto = await configService.CreateConfigurationAsync(
            new CreateConfigurationRequest(
                Key: "AI.Model",
                Value: $"\"{testModel}\"",
                ValueType: "String",
                Description: "Test AI model from database",
                Category: "AI/LLM",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Development"),
            testUser.Id);

        // When: GenerateCompletionAsync is called
        var handler = CreateSuccessHandler();
        var llmService = CreateLlmService(scope, handler);
        var result = await llmService.GenerateCompletionAsync("system", "test prompt");

        // Then: The database model value is used
        result.Success.Should().BeTrue();

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        root.GetProperty("model").GetString().Should().Be(testModel);

        // Cleanup happens automatically via IntegrationTestBase
    }

    /// <summary>
    /// Scenario: LLM service uses database configuration for temperature
    ///   Given a configuration exists in the database for AI.Temperature
    ///   When GenerateCompletionAsync is called
    ///   Then the database temperature value is used
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_UsesDatabaseTemperature_WhenConfigurationExists()
    {
        // Given: A configuration exists in the database for AI.Temperature
        using var scope = Factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        var testUser = await CreateTestUserAsync($"config-test-temp-{TestRunId}@test.com");
        var testTemperature = 0.75;

        await configService.CreateConfigurationAsync(
            new CreateConfigurationRequest(
                Key: "AI.Temperature",
                Value:testTemperature.ToString(),
                ValueType: "Double",
                Description: "Test AI temperature from database",
                Category: "AI/LLM",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Development"),
            testUser.Id);

        // When: GenerateCompletionAsync is called
        var handler = CreateSuccessHandler();
        var llmService = CreateLlmService(scope, handler);
        var result = await llmService.GenerateCompletionAsync("system", "test prompt");

        // Then: The database temperature value is used
        result.Success.Should().BeTrue();

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        root.GetProperty("temperature").GetDouble().Should().Be(testTemperature);

        // Cleanup happens automatically via IntegrationTestBase
    }

    /// <summary>
    /// Scenario: LLM service uses database configuration for max_tokens
    ///   Given a configuration exists in the database for AI.MaxTokens
    ///   When GenerateCompletionAsync is called
    ///   Then the database max_tokens value is used
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_UsesDatabaseMaxTokens_WhenConfigurationExists()
    {
        // Given: A configuration exists in the database for AI.MaxTokens
        using var scope = Factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        var testUser = await CreateTestUserAsync($"config-test-maxtokens-{TestRunId}@test.com");
        var testMaxTokens = 1500;

        await configService.CreateConfigurationAsync(
            new CreateConfigurationRequest(
                Key: "AI.MaxTokens",
                Value:testMaxTokens.ToString(),
                ValueType: "Integer",
                Description: "Test AI max_tokens from database",
                Category: "AI/LLM",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Development"),
            testUser.Id);

        // When: GenerateCompletionAsync is called
        var handler = CreateSuccessHandler();
        var llmService = CreateLlmService(scope, handler);
        var result = await llmService.GenerateCompletionAsync("system", "test prompt");

        // Then: The database max_tokens value is used
        result.Success.Should().BeTrue();

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        root.GetProperty("max_tokens").GetInt32().Should().Be(testMaxTokens);

        // Cleanup happens automatically via IntegrationTestBase
    }

    /// <summary>
    /// Scenario: LLM service falls back to hardcoded defaults when no configuration exists
    ///   Given no configuration exists in the database
    ///   When GenerateCompletionAsync is called
    ///   Then hardcoded default values are used
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_UsesHardcodedDefaults_WhenNoDatabaseConfiguration()
    {
        // Given: No configuration exists in the database (clean database state)
        using var scope = Factory.Services.CreateScope();

        // When: GenerateCompletionAsync is called
        var handler = CreateSuccessHandler();
        var llmService = CreateLlmService(scope, handler);
        var result = await llmService.GenerateCompletionAsync("system", "test prompt");

        // Then: Hardcoded default values are used
        result.Success.Should().BeTrue();

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        // Verify hardcoded defaults from LlmService
        root.GetProperty("model").GetString().Should().Be("deepseek/deepseek-chat-v3.1");
        root.GetProperty("temperature").GetDouble().Should().Be(0.3);
        root.GetProperty("max_tokens").GetInt32().Should().Be(500);
    }

    /// <summary>
    /// Scenario: LLM service validates and rejects invalid temperature from database
    ///   Given a configuration exists with an invalid temperature value
    ///   When GenerateCompletionAsync is called
    ///   Then the hardcoded default temperature is used
    ///   And a warning is logged
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_RejectsInvalidTemperature_FromDatabase()
    {
        // Given: A configuration exists with an invalid temperature value
        using var scope = Factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        var testUser = await CreateTestUserAsync($"config-test-invalid-temp-{TestRunId}@test.com");
        var invalidTemperature = 5.0; // Outside valid range (0.0-2.0)

        await configService.CreateConfigurationAsync(
            new CreateConfigurationRequest(
                Key: "AI.Temperature",
                Value:invalidTemperature.ToString(),
                ValueType: "Double",
                Description: "Invalid temperature for testing",
                Category: "AI/LLM",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Development"),
            testUser.Id);

        // When: GenerateCompletionAsync is called
        var handler = CreateSuccessHandler();
        var llmService = CreateLlmService(scope, handler);
        var result = await llmService.GenerateCompletionAsync("system", "test prompt");

        // Then: The hardcoded default temperature is used
        result.Success.Should().BeTrue();

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        root.GetProperty("temperature").GetDouble().Should().Be(0.3); // Default value

        // Cleanup happens automatically via IntegrationTestBase
    }

    /// <summary>
    /// Scenario: LLM service caps max_tokens at upper bound from database
    ///   Given a configuration exists with max_tokens exceeding the upper bound
    ///   When GenerateCompletionAsync is called
    ///   Then max_tokens is capped at the maximum allowed value
    /// </summary>
    [Fact]
    public async Task GenerateCompletionAsync_CapsMaxTokens_WhenExceedingUpperBound()
    {
        // Given: A configuration exists with max_tokens exceeding the upper bound
        using var scope = Factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        var testUser = await CreateTestUserAsync($"config-test-excessive-{TestRunId}@test.com");
        var excessiveMaxTokens = 50000; // Above upper bound (32000)

        await configService.CreateConfigurationAsync(
            new CreateConfigurationRequest(
                Key: "AI.MaxTokens",
                Value:excessiveMaxTokens.ToString(),
                ValueType: "Integer",
                Description: "Excessive max_tokens for testing",
                Category: "AI/LLM",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Development"),
            testUser.Id);

        // When: GenerateCompletionAsync is called
        var handler = CreateSuccessHandler();
        var llmService = CreateLlmService(scope, handler);
        var result = await llmService.GenerateCompletionAsync("system", "test prompt");

        // Then: max_tokens is capped at the maximum allowed value
        result.Success.Should().BeTrue();

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        root.GetProperty("max_tokens").GetInt32().Should().Be(32000); // Capped value

        // Cleanup happens automatically via IntegrationTestBase
    }

    /// <summary>
    /// Scenario: LLM streaming uses database configuration
    ///   Given a configuration exists in the database for all AI parameters
    ///   When GenerateCompletionStreamAsync is called
    ///   Then all database values are used in the streaming request
    /// </summary>
    [Fact]
    public async Task GenerateCompletionStreamAsync_UsesDatabaseConfiguration_ForAllParameters()
    {
        // Given: A configuration exists in the database for all AI parameters
        using var scope = Factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        var testUser = await CreateTestUserAsync($"config-test-stream-{TestRunId}@test.com");
        var testModel = "stream-test-model-" + TestRunId;
        var testTemperature = 0.85;
        var testMaxTokens = 800;

        await configService.CreateConfigurationAsync(
            new CreateConfigurationRequest(
                Key: "AI.Model",
                Value:$"\"{testModel}\"",
                ValueType: "String",
                Description: "Test streaming model",
                Category: "AI/LLM",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Development"),
            testUser.Id);

        await configService.CreateConfigurationAsync(
            new CreateConfigurationRequest(
                Key: "AI.Temperature",
                Value:testTemperature.ToString(),
                ValueType: "Double",
                Description: "Test streaming temperature",
                Category: "AI/LLM",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Development"),
            testUser.Id);

        await configService.CreateConfigurationAsync(
            new CreateConfigurationRequest(
                Key: "AI.MaxTokens",
                Value:testMaxTokens.ToString(),
                ValueType: "Integer",
                Description: "Test streaming max_tokens",
                Category: "AI/LLM",
                IsActive: true,
                RequiresRestart: false,
                Environment: "Development"),
            testUser.Id);

        // When: GenerateCompletionStreamAsync is called
        var handler = CreateStreamingSuccessHandler();
        var llmService = CreateLlmService(scope, handler);

        var tokens = new List<string>();
        await foreach (var token in llmService.GenerateCompletionStreamAsync("system", "test prompt"))
        {
            tokens.Add(token);
        }

        // Then: All database values are used in the streaming request
        tokens.Should().NotBeEmpty();

        var requestBody = handler.RequestBodies.Single();
        using var document = JsonDocument.Parse(requestBody!);
        var root = document.RootElement;

        root.GetProperty("model").GetString().Should().Be(testModel);
        root.GetProperty("temperature").GetDouble().Should().Be(testTemperature);
        root.GetProperty("max_tokens").GetInt32().Should().Be(testMaxTokens);
        root.GetProperty("stream").GetBoolean().Should().BeTrue();

        // Cleanup happens automatically via IntegrationTestBase
    }

    /// <summary>
    /// Scenario: Migration seeds default configurations correctly
    ///   Given the migration has been applied
    ///   When querying the database for seeded configurations
    ///   Then default AI/LLM configurations exist for Production and Development
    /// </summary>
    [Fact]
    public async Task Migration_SeedsDefaultConfigurations_ForProductionAndDevelopment()
    {
        // Given: The migration has been applied (automatic via WebApplicationFactory)
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // When: Querying the database for seeded configurations
        var prodConfigs = await dbContext.SystemConfigurations
            .Where(c => c.Category == "AI/LLM" && c.Environment == "Production")
            .ToListAsync();

        var devConfigs = await dbContext.SystemConfigurations
            .Where(c => c.Category == "AI/LLM" && c.Environment == "Development")
            .ToListAsync();

        // Then: Default AI/LLM configurations exist for Production and Development
        // Note: May include seeded configs from migration plus any test configs
        prodConfigs.Should().NotBeEmpty();
        devConfigs.Should().NotBeEmpty();

        // Verify expected keys exist (from migration seed data)
        var expectedKeys = new[] { "AI.Model", "AI.Temperature", "AI.MaxTokens", "AI.TimeoutSeconds" };

        foreach (var key in expectedKeys)
        {
            c => c.Key == key.Should().Contain(prodConfigs);
            c => c.Key == key.Should().Contain(devConfigs);
        }
    }

    #region Helper Methods

    private LlmService CreateLlmService(IServiceScope scope, TestHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("OpenRouter")).Returns(httpClient);

        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LlmService>>();
        var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

        return new LlmService(
            factoryMock.Object,
            config,
            logger,
            configService,
            config);
    }

    private TestHttpMessageHandler CreateSuccessHandler()
    {
        return new TestHttpMessageHandler((_, _) =>
        {
            var payload = new
            {
                id = "resp_integration_test",
                model = "test-model",
                choices = new[]
                {
                    new
                    {
                        message = new { content = "Integration test response" },
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
