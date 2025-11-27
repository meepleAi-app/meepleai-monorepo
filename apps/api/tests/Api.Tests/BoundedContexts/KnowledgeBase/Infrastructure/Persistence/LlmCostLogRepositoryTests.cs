using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Integration tests for LlmCostLogRepository using Testcontainers with PostgreSQL.
/// ISSUE-960: BGAI-018 - Cost tracking persistence tests
/// </summary>
public class LlmCostLogRepositoryTests : IntegrationTestBase<LlmCostLogRepository>
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    protected override string DatabaseName => "meepleai_llmcost_test";

    protected override LlmCostLogRepository CreateRepository(MeepleAiDbContext dbContext)
        => new LlmCostLogRepository(dbContext, Mock.Of<ILogger<LlmCostLogRepository>>());

    [Fact]
    public async Task LogCost_SuccessfulRequest_StoresCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create test user for foreign key constraint
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@llmcost.com",
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var cost = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0003m
        };

        // Act
        await Repository.LogCostAsync(
            userId,
            "User",
            cost,
            "chat",
            success: true,
            errorMessage: null,
            latencyMs: 1500,
            ipAddress: "127.0.0.1",
            userAgent: "Test/1.0",
            ct: TestCancellationToken);

        // Assert
        var logs = await DbContext.LlmCostLogs.ToListAsync(TestCancellationToken);
        Assert.Single(logs);

        var log = logs[0];
        Assert.Equal(userId, log.UserId);
        Assert.Equal("User", log.UserRole);
        Assert.Equal("openai/gpt-4o-mini", log.ModelId);
        Assert.Equal("OpenRouter", log.Provider);
        Assert.Equal(1000, log.PromptTokens);
        Assert.Equal(500, log.CompletionTokens);
        Assert.Equal(1500, log.TotalTokens);
        Assert.Equal(0.00015m, log.InputCost);
        Assert.Equal(0.0003m, log.OutputCost);
        Assert.Equal(0.00045m, log.TotalCost);
        Assert.True(log.Success);
        Assert.Null(log.ErrorMessage);
        Assert.Equal(1500, log.LatencyMs);
    }

    [Fact]
    public async Task GetTotalCost_MultipleRequests_SumsCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Repository.LogCostAsync(null, "Anonymous", CreateCost(0.001m), "chat", true, null, 100, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "User", CreateCost(0.002m), "qa", true, null, 200, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "Admin", CreateCost(0.003m), "explain", true, null, 300, null, null, ct: TestCancellationToken);

        // Act
        var totalCost = await Repository.GetTotalCostAsync(today, today, ct: TestCancellationToken);

        // Assert
        Assert.Equal(0.006m, totalCost);
    }

    [Fact]
    public async Task GetCostsByProvider_GroupsCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Repository.LogCostAsync(null, "User", CreateCost(0.001m, "OpenRouter"), "chat", true, null, 100, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "User", CreateCost(0.002m, "OpenRouter"), "qa", true, null, 200, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "User", CreateCost(0m, "Ollama"), "chat", true, null, 150, null, null, ct: TestCancellationToken);

        // Act
        var costsByProvider = await Repository.GetCostsByProviderAsync(today, today, ct: TestCancellationToken);

        // Assert
        Assert.Equal(2, costsByProvider.Count);
        Assert.Equal(0.003m, costsByProvider["OpenRouter"]);
        Assert.Equal(0m, costsByProvider["Ollama"]);
    }

    [Fact]
    public async Task GetCostsByRole_GroupsCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Repository.LogCostAsync(null, "Anonymous", CreateCost(0.001m), "chat", true, null, 100, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "User", CreateCost(0.002m), "qa", true, null, 200, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "Admin", CreateCost(0.005m), "explain", true, null, 300, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "User", CreateCost(0.001m), "chat", true, null, 150, null, null, ct: TestCancellationToken);

        // Act
        var costsByRole = await Repository.GetCostsByRoleAsync(today, today, ct: TestCancellationToken);

        // Assert
        Assert.Equal(3, costsByRole.Count);
        Assert.Equal(0.001m, costsByRole["Anonymous"]);
        Assert.Equal(0.003m, costsByRole["User"]);
        Assert.Equal(0.005m, costsByRole["Admin"]);
    }

    [Fact]
    public async Task GetUserCost_FiltersCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Create test users for foreign key constraints
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var testUser1 = new UserEntity
        {
            Id = user1,
            Email = "user1@llmcost.com",
            DisplayName = "Test User 1",
            PasswordHash = "hash",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        var testUser2 = new UserEntity
        {
            Id = user2,
            Email = "user2@llmcost.com",
            DisplayName = "Test User 2",
            PasswordHash = "hash",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };
        DbContext.Users.Add(testUser1);
        DbContext.Users.Add(testUser2);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Repository.LogCostAsync(user1, "User", CreateCost(0.001m), "chat", true, null, 100, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(user1, "User", CreateCost(0.002m), "qa", true, null, 200, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(user2, "User", CreateCost(0.005m), "chat", true, null, 300, null, null, ct: TestCancellationToken);

        // Act
        var user1Cost = await Repository.GetUserCostAsync(user1, today, today, ct: TestCancellationToken);
        var user2Cost = await Repository.GetUserCostAsync(user2, today, today, ct: TestCancellationToken);

        // Assert
        Assert.Equal(0.003m, user1Cost);
        Assert.Equal(0.005m, user2Cost);
    }

    [Fact]
    public async Task GetDailyCost_ReturnsCorrectTotal()
    {
        // Arrange
        await ResetDatabaseAsync();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Repository.LogCostAsync(null, "User", CreateCost(0.010m), "chat", true, null, 100, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "Admin", CreateCost(0.025m), "qa", true, null, 200, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "User", CreateCost(0.015m), "explain", true, null, 150, null, null, ct: TestCancellationToken);

        // Act
        var dailyCost = await Repository.GetDailyCostAsync(today, ct: TestCancellationToken);

        // Assert
        Assert.Equal(0.050m, dailyCost);
    }

    [Fact]
    public async Task DateRangeFilter_WorksCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Repository.LogCostAsync(null, "User", CreateCost(0.001m), "chat", true, null, 100, null, null, ct: TestCancellationToken);
        await Repository.LogCostAsync(null, "User", CreateCost(0.002m), "qa", true, null, 200, null, null, ct: TestCancellationToken);

        // Act
        var totalCost = await Repository.GetTotalCostAsync(today, today, ct: TestCancellationToken);

        // Assert
        Assert.Equal(0.003m, totalCost);
    }

    private static LlmCostCalculation CreateCost(decimal totalCost, string provider = "OpenRouter")
    {
        // Simple helper - split cost evenly for testing
        var inputCost = totalCost * 0.4m;
        var outputCost = totalCost * 0.6m;
        var promptTokens = (int)(inputCost * 1_000_000 / 0.15m); // Rough token estimate
        var completionTokens = (int)(outputCost * 1_000_000 / 0.60m);

        return new LlmCostCalculation
        {
            ModelId = provider == "Ollama" ? "llama3:8b" : "openai/gpt-4o-mini",
            Provider = provider,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            InputCost = inputCost,
            OutputCost = outputCost
        };
    }
}

