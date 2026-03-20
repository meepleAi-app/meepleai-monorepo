using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Integration tests for LlmRequestLogRepository using shared PostgreSQL Testcontainer.
/// Issue #5076: Phase 1 test suite — verifies persistence, 30-day retention, and cleanup.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5076")]
public sealed class LlmRequestLogRepositoryTests : SharedDatabaseTestBase<LlmRequestLogRepository>
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public LlmRequestLogRepositoryTests(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    protected override LlmRequestLogRepository CreateRepository(MeepleAiDbContext dbContext)
        => new(dbContext, Mock.Of<ILogger<LlmRequestLogRepository>>());

    // ─── LogRequestAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task LogRequestAsync_ValidData_PersistsAllFields()
    {
        // Arrange
        await ResetDatabaseAsync();
        var before = DateTime.UtcNow;

        // Act
        await Repository.LogRequestAsync(
            modelId: "openai/gpt-4o",
            provider: "openrouter",
            source: RequestSource.RagPipeline,
            userId: null,
            userRole: null,
            promptTokens: 500,
            completionTokens: 200,
            costUsd: 0.0025m,
            latencyMs: 480,
            success: true,
            errorMessage: null,
            isStreaming: false,
            isFreeModel: false,
            sessionId: "test-session",
            userRegion: null,
            cancellationToken: TestCancellationToken);

        // Assert
        var logs = await DbContext.LlmRequestLogs.ToListAsync(TestCancellationToken);
        logs.Should().ContainSingle();

        var log = logs[0];
        log.ModelId.Should().Be("openai/gpt-4o");
        log.Provider.Should().Be("openrouter");
        log.RequestSource.Should().Be(nameof(RequestSource.RagPipeline));
        log.PromptTokens.Should().Be(500);
        log.CompletionTokens.Should().Be(200);
        log.TotalTokens.Should().Be(700);
        log.CostUsd.Should().Be(0.0025m);
        log.LatencyMs.Should().Be(480);
        Assert.True(log.Success);
        Assert.Null(log.ErrorMessage);
        Assert.False(log.IsStreaming);
        Assert.False(log.IsFreeModel);
        log.SessionId.Should().Be("test-session");
        Assert.True(log.RequestedAt >= before && log.RequestedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task LogRequestAsync_SetsExpiresAt30DaysAfterRequestedAt()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        await Repository.LogRequestAsync(
            "model", "provider", RequestSource.Manual,
            null, null, 0, 0, 0m, 0, false, null, false, false, null,
            userRegion: null, cancellationToken: TestCancellationToken);

        // Assert
        var log = await DbContext.LlmRequestLogs.SingleAsync(TestCancellationToken);
        var expectedExpiry = log.RequestedAt.AddDays(30);
        Assert.True(Math.Abs((log.ExpiresAt - expectedExpiry).TotalSeconds) < 2,
            $"ExpiresAt {log.ExpiresAt} should be ~30 days after RequestedAt {log.RequestedAt}");
    }

    [Fact]
    public async Task LogRequestAsync_ErrorRequest_StoresErrorMessage()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        await Repository.LogRequestAsync(
            "model", "provider", RequestSource.AgentTask,
            null, null, 100, 0, 0m, 100, success: false,
            errorMessage: "Rate limit exceeded",
            isStreaming: false, isFreeModel: false, sessionId: null,
            userRegion: null, cancellationToken: TestCancellationToken);

        // Assert
        var log = await DbContext.LlmRequestLogs.SingleAsync(TestCancellationToken);
        Assert.False(log.Success);
        log.ErrorMessage.Should().Be("Rate limit exceeded");
    }

    [Theory]
    [InlineData(RequestSource.Manual)]
    [InlineData(RequestSource.RagPipeline)]
    [InlineData(RequestSource.AutomatedTest)]
    [InlineData(RequestSource.AgentTask)]
    [InlineData(RequestSource.AdminOperation)]
    public async Task LogRequestAsync_AllRequestSources_PersistedAsString(RequestSource source)
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        await Repository.LogRequestAsync(
            "model", "provider", source,
            null, null, 0, 0, 0m, 0, true, null, false, false, null,
            userRegion: null, cancellationToken: TestCancellationToken);

        // Assert
        var log = await DbContext.LlmRequestLogs.SingleAsync(TestCancellationToken);
        log.RequestSource.Should().Be(source.ToString());
    }

    // ─── DeleteExpiredAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteExpiredAsync_ExpiredEntries_DeletesThem()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Insert 2 expired + 1 still-active entry directly
        var now = DateTime.UtcNow;
        DbContext.LlmRequestLogs.AddRange(
            new Api.Infrastructure.Entities.LlmRequestLogEntity
            {
                ModelId = "m",
                Provider = "p",
                RequestedAt = now.AddDays(-31),
                ExpiresAt = now.AddDays(-1) // EXPIRED
            },
            new Api.Infrastructure.Entities.LlmRequestLogEntity
            {
                ModelId = "m",
                Provider = "p",
                RequestedAt = now.AddDays(-35),
                ExpiresAt = now.AddHours(-1) // EXPIRED
            },
            new Api.Infrastructure.Entities.LlmRequestLogEntity
            {
                ModelId = "m",
                Provider = "p",
                RequestedAt = now,
                ExpiresAt = now.AddDays(30) // ACTIVE
            });
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var deleted = await Repository.DeleteExpiredAsync(now, TestCancellationToken);

        // Assert
        deleted.Should().Be(2);
        var remaining = await DbContext.LlmRequestLogs.CountAsync(TestCancellationToken);
        remaining.Should().Be(1);
    }

    [Fact]
    public async Task DeleteExpiredAsync_NoExpiredEntries_ReturnsZero()
    {
        // Arrange
        await ResetDatabaseAsync();

        var now = DateTime.UtcNow;
        DbContext.LlmRequestLogs.Add(new Api.Infrastructure.Entities.LlmRequestLogEntity
        {
            ModelId = "m",
            Provider = "p",
            RequestedAt = now,
            ExpiresAt = now.AddDays(30)
        });
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var deleted = await Repository.DeleteExpiredAsync(now, TestCancellationToken);

        // Assert
        deleted.Should().Be(0);
    }

    [Fact]
    public async Task DeleteExpiredAsync_EmptyTable_ReturnsZero()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var deleted = await Repository.DeleteExpiredAsync(DateTime.UtcNow, TestCancellationToken);

        // Assert
        deleted.Should().Be(0);
    }

    // ─── GetActiveAiUserCountAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetActiveAiUserCountAsync_ReturnsDistinctUserCount()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        // User1: 2 requests, User2: 1 request, System: 1 request (null userId)
        await Repository.LogRequestAsync("model", "openrouter", RequestSource.Manual,
            userId1, "user", 100, 50, 0.01m, 200, true, null, false, false, null, null, TestCancellationToken);
        await Repository.LogRequestAsync("model", "openrouter", RequestSource.RagPipeline,
            userId1, "user", 200, 100, 0.02m, 300, true, null, false, false, null, null, TestCancellationToken);
        await Repository.LogRequestAsync("model", "ollama", RequestSource.Manual,
            userId2, "admin", 50, 25, 0m, 100, true, null, false, true, null, null, TestCancellationToken);
        await Repository.LogRequestAsync("model", "ollama", RequestSource.AgentTask,
            null, null, 50, 25, 0m, 100, true, null, false, true, null, null, TestCancellationToken);

        // Act
        var count = await Repository.GetActiveAiUserCountAsync(
            DateTime.UtcNow.AddDays(-30), TestCancellationToken);

        // Assert — 2 distinct users (null userId excluded)
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetActiveAiUserCountAsync_ExcludesAnonymizedRecords()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();

        await Repository.LogRequestAsync("model", "openrouter", RequestSource.Manual,
            userId, "user", 100, 50, 0.01m, 200, true, null, false, false, null, null, TestCancellationToken);

        // Pseudonymize all records
        await Repository.PseudonymizeOldLogsAsync(
            DateTime.UtcNow.AddMinutes(1), "test-salt", TestCancellationToken);

        // Act
        var count = await Repository.GetActiveAiUserCountAsync(
            DateTime.UtcNow.AddDays(-30), TestCancellationToken);

        // Assert — anonymized records excluded
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveAiUserCountAsync_EmptyTable_ReturnsZero()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var count = await Repository.GetActiveAiUserCountAsync(
            DateTime.UtcNow.AddDays(-30), TestCancellationToken);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetActiveAiUserCountAsync_ExcludesOldRecords()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();

        await Repository.LogRequestAsync("model", "openrouter", RequestSource.Manual,
            userId, "user", 100, 50, 0.01m, 200, true, null, false, false, null, null, TestCancellationToken);

        // Act — query with future 'from' date (all records are "old")
        var count = await Repository.GetActiveAiUserCountAsync(
            DateTime.UtcNow.AddMinutes(1), TestCancellationToken);

        // Assert
        count.Should().Be(0);
    }
}
