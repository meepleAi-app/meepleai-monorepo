using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for /admin/stats endpoint response schema.
/// Verifies that the API response matches frontend expectations.
/// Issue #1695: Ensure feedback stats are properly serialized for admin dashboard.
/// </summary>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Administration")]
public class AdminStatsEndpointIntegrationTests : IAsyncLifetime
{
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private IMediator? _mediator;

    public async Task InitializeAsync()
    {
        // Setup in-memory database for fast integration tests
        _dbContext = DbContextHelper.CreateInMemoryDbContext();

        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(_dbContext);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(GetFeedbackStatsQuery).Assembly);
            cfg.RegisterServicesFromAssembly(typeof(GetAiRequestStatsQuery).Assembly);
        });

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Seed test data
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private async Task SeedTestDataAsync()
    {
        if (_dbContext == null) return;

        var userId = Guid.NewGuid();

        // Add AI request stats
        _dbContext.AiRequests.AddRange(
            new AiRequestEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Endpoint = "/api/v1/chat",
                Status = "Success",
                LatencyMs = 100,
                TokenCount = 50,
                PromptTokens = 30,
                CompletionTokens = 20,
                CreatedAt = DateTime.UtcNow
            },
            new AiRequestEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Endpoint = "/api/v1/search",
                Status = "Success",
                LatencyMs = 200,
                TokenCount = 75,
                PromptTokens = 45,
                CompletionTokens = 30,
                CreatedAt = DateTime.UtcNow
            }
        );

        // Add feedback data
        _dbContext.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/search",
                UserId = userId,
                Outcome = "not-helpful",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task AdminStats_ResponseSchema_MatchesFrontendExpectations()
    {
        // Arrange
        Assert.NotNull(_mediator);

        var aiStatsQuery = new GetAiRequestStatsQuery(null, null, null, null);
        var feedbackQuery = new GetFeedbackStatsQuery(null, null, null);

        // Act - Simulate what the endpoint does
        var aiStats = await _mediator.Send(aiStatsQuery);
        var feedbackStats = await _mediator.Send(feedbackQuery);

        // Simulate the endpoint response construction
        var response = new
        {
            aiStats.TotalRequests,
            aiStats.AvgLatencyMs,
            aiStats.TotalTokens,
            aiStats.SuccessRate,
            aiStats.EndpointCounts,
            totalFeedback = feedbackStats.TotalFeedbacks,
            feedbackCounts = feedbackStats.FeedbackByOutcome
        };

        // Serialize to JSON to verify the actual wire format
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var deserialized = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert - Verify response has expected fields with correct casing
        Assert.True(deserialized.TryGetProperty("totalRequests", out _), "Missing 'totalRequests' field");
        Assert.True(deserialized.TryGetProperty("avgLatencyMs", out _), "Missing 'avgLatencyMs' field");
        Assert.True(deserialized.TryGetProperty("totalTokens", out _), "Missing 'totalTokens' field");
        Assert.True(deserialized.TryGetProperty("successRate", out _), "Missing 'successRate' field");
        Assert.True(deserialized.TryGetProperty("endpointCounts", out _), "Missing 'endpointCounts' field");

        // Critical: Verify feedback fields match frontend schema (admin-client.tsx:188-189)
        Assert.True(deserialized.TryGetProperty("totalFeedback", out var totalFeedbackProp),
            "Missing 'totalFeedback' field - frontend expects this!");
        Assert.True(deserialized.TryGetProperty("feedbackCounts", out var feedbackCountsProp),
            "Missing 'feedbackCounts' field - frontend expects this!");

        // Verify totalFeedback is a number
        Assert.Equal(JsonValueKind.Number, totalFeedbackProp.ValueKind);
        Assert.Equal(3, totalFeedbackProp.GetInt32());

        // Verify feedbackCounts is an object with expected keys
        Assert.Equal(JsonValueKind.Object, feedbackCountsProp.ValueKind);
        Assert.True(feedbackCountsProp.TryGetProperty("helpful", out var helpfulProp),
            "feedbackCounts missing 'helpful' key");
        Assert.True(feedbackCountsProp.TryGetProperty("not-helpful", out var notHelpfulProp),
            "feedbackCounts missing 'not-helpful' key - frontend expects this exact format!");

        Assert.Equal(2, helpfulProp.GetInt32());
        Assert.Equal(1, notHelpfulProp.GetInt32());
    }

    [Fact]
    public async Task AdminStats_FeedbackCounts_UsesHyphenNotUnderscore()
    {
        // Arrange
        Assert.NotNull(_mediator);
        var query = new GetFeedbackStatsQuery(null, null, null);

        // Act
        var result = await _mediator.Send(query);

        // Assert - Verify outcome keys use hyphen, not underscore
        Assert.Equal(3, result.TotalFeedbacks);
        Assert.Equal(2, result.HelpfulCount);
        Assert.Equal(1, result.NotHelpfulCount);

        // Verify FeedbackByOutcome dictionary has "not-helpful" key
        Assert.True(result.FeedbackByOutcome.ContainsKey("helpful"),
            "FeedbackByOutcome should contain 'helpful' key");
        Assert.True(result.FeedbackByOutcome.ContainsKey("not-helpful"),
            "FeedbackByOutcome should contain 'not-helpful' key (with hyphen)");
        Assert.False(result.FeedbackByOutcome.ContainsKey("not_helpful"),
            "FeedbackByOutcome should NOT contain 'not_helpful' key (with underscore)");

        Assert.Equal(2, result.FeedbackByOutcome["helpful"]);
        Assert.Equal(1, result.FeedbackByOutcome["not-helpful"]);
    }

    [Fact]
    public async Task AdminStats_WithNoFeedback_ReturnsEmptyFeedbackCounts()
    {
        // Arrange - Create fresh context with no feedback
        await using var emptyContext = DbContextHelper.CreateInMemoryDbContext();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(emptyContext);
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(GetFeedbackStatsQuery).Assembly);
        });
        var emptyServiceProvider = services.BuildServiceProvider();
        var emptyMediator = emptyServiceProvider.GetRequiredService<IMediator>();

        var query = new GetFeedbackStatsQuery(null, null, null);

        // Act
        var result = await emptyMediator.Send(query);

        // Assert
        Assert.Equal(0, result.TotalFeedbacks);
        Assert.Empty(result.FeedbackByOutcome);
    }

    [Fact]
    public async Task AdminStats_JsonSerialization_ProducesCorrectCamelCase()
    {
        // Arrange
        Assert.NotNull(_mediator);
        var aiStatsQuery = new GetAiRequestStatsQuery(null, null, null, null);
        var feedbackQuery = new GetFeedbackStatsQuery(null, null, null);

        var aiStats = await _mediator.Send(aiStatsQuery);
        var feedbackStats = await _mediator.Send(feedbackQuery);

        var response = new
        {
            aiStats.TotalRequests,
            aiStats.AvgLatencyMs,
            aiStats.TotalTokens,
            aiStats.SuccessRate,
            aiStats.EndpointCounts,
            totalFeedback = feedbackStats.TotalFeedbacks,
            feedbackCounts = feedbackStats.FeedbackByOutcome
        };

        // Act - Serialize with camelCase (ASP.NET Core default)
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(response, options);

        // Assert - Verify JSON has correct field names
        Assert.Contains("\"totalRequests\":", json);
        Assert.Contains("\"avgLatencyMs\":", json);
        Assert.Contains("\"totalTokens\":", json);
        Assert.Contains("\"successRate\":", json);
        Assert.Contains("\"endpointCounts\":", json);
        Assert.Contains("\"totalFeedback\":", json);  // Singular, camelCase
        Assert.Contains("\"feedbackCounts\":", json);  // camelCase

        // Should NOT contain old PascalCase field names
        Assert.DoesNotContain("\"TotalFeedbacks\":", json);
        Assert.DoesNotContain("\"FeedbackByOutcome\":", json);
    }
}
