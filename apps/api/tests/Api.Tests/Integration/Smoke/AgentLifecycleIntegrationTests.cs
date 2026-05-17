using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using Microsoft.AspNetCore.Http;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Smoke;

/// <summary>
/// Integration tests for Agent CRUD lifecycle + soft-delete cascade.
/// Issue #904: SG3 — Agent lifecycle smoke tests.
///
/// Covers:
/// - SG3-T1: Draft → Testing → Published → Unpublish full lifecycle
/// - SG3-T2: Soft-delete agent without threads → 204 + agent hidden
/// - SG3-T3 (MOST IMPORTANT): Soft-delete agent with ChatThreads → cascade CloseThread
/// - SG3-T4: Restore soft-deleted agent → agent visible, threads remain closed
/// - SG3-T5: Soft-delete system-defined agent → SystemAgentProtectedException (403)
/// - SG3-T6: Create agent at free-tier quota → TierQuotaExceededException (402)
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Dependency", "PostgreSQL")]
public sealed class AgentLifecycleIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Stable test IDs — SG3-specific, no collision with SG2 (902) or SG4 (901)
    private static readonly Guid TestUserId = new("10000000-0000-0000-0000-000000000904");
    private static readonly Guid TestGameId = new("20000000-0000-0000-0000-000000000904");

    public AgentLifecycleIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_agentlifecycle_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        // Override the mock IAgentDefinitionRepository with the real implementation
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();
        // Register real ChatThreadRepository for cascade tests
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>();
        // ISharedGameRepository mock — needed by CreateUserAgentCommandHandler
        services.AddScoped(_ => Mock.Of<ISharedGameRepository>());

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations (retry on transient container startup delays)
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }

        // Seed required parent entities for FK constraints
        _dbContext.Users.Add(new UserEntity
        {
            Id = TestUserId,
            Email = "smoke-sg3-agent-lifecycle@test.meepleai",
            PasswordHash = "v1.test-hash",
            CreatedAt = DateTime.UtcNow,
        });
        _dbContext.Games.Add(new GameEntity
        {
            Id = TestGameId,
            Name = "SG3 Agent Lifecycle Test Game",
            CreatedAt = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* ignore cleanup errors */ }
        }

        if (_serviceProvider is IDisposable disposable)
            disposable.Dispose();
    }

    // ─── SG3-T1 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SG3-T1: Full lifecycle — Draft → Testing → Published → Unpublish → Draft.
    ///
    /// Tests state machine transitions via existing admin commands reused in user routing.
    /// StartTesting: Draft → Testing
    /// Publish: Testing → Published (also sets IsActive=true)
    /// Unpublish: Published → Draft (also sets IsActive=false)
    /// </summary>
    [Fact]
    public async Task Lifecycle_Draft_Testing_Published_Unpublish_FullFlow()
    {
        // Arrange — seed an agent in Draft
        var agent = BuildAgent("SG3-T1 Lifecycle Agent", systemDefined: false);
        _dbContext!.AgentDefinitions.Add(agent);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var agentId = agent.Id;
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Assert initial state
        agent.Status.Should().Be(AgentDefinitionStatus.Draft);
        agent.IsActive.Should().BeFalse();

        // Act — Draft → Testing
        await mediator.Send(new StartTestingAgentDefinitionCommand(agentId), TestCancellationToken);

        var afterTesting = await LoadAgent(agentId);
        afterTesting.Should().NotBeNull();
        afterTesting!.Status.Should().Be(AgentDefinitionStatus.Testing, "StartTesting should move Draft → Testing");

        // Act — Testing → Published
        await mediator.Send(new PublishAgentDefinitionCommand(agentId), TestCancellationToken);

        var afterPublish = await LoadAgent(agentId);
        afterPublish!.Status.Should().Be(AgentDefinitionStatus.Published);
        afterPublish.IsActive.Should().BeTrue("Publish should activate the agent");

        // Act — Published → Draft
        await mediator.Send(new UnpublishAgentDefinitionCommand(agentId), TestCancellationToken);

        var afterUnpublish = await LoadAgent(agentId);
        afterUnpublish!.Status.Should().Be(AgentDefinitionStatus.Draft);
        afterUnpublish.IsActive.Should().BeFalse("Unpublish should deactivate the agent");
    }

    // ─── SG3-T2 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SG3-T2: Soft-delete an agent without associated ChatThreads.
    ///
    /// Verifies the agent is hidden from normal queries after SoftDelete().
    /// IsDeleted global filter means GetByIdAsync returns null after delete.
    /// </summary>
    [Fact]
    public async Task SoftDelete_AgentWithoutThreads_AgentHiddenFromNormalQuery()
    {
        // Arrange
        var agent = BuildAgent("SG3-T2 Delete Agent (no threads)", systemDefined: false);
        _dbContext!.AgentDefinitions.Add(agent);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        var agentId = agent.Id;

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Pre-assert: agent is visible
        var before = await LoadAgent(agentId);
        before.Should().NotBeNull("agent should be visible before soft-delete");

        // Act
        var command = new SoftDeleteUserAgentCommand(UserId: TestUserId, AgentId: agentId);
        await mediator.Send(command, TestCancellationToken);

        // Assert — agent is now hidden (global EF query filter excludes IsDeleted=true)
        var afterDomain = await LoadAgent(agentId);
        afterDomain.Should().BeNull("soft-deleted agent must be filtered out by HasQueryFilter");

        // Assert — raw DB check confirms IsDeleted=true (IgnoreQueryFilters)
        var rawRow = await _dbContext.AgentDefinitions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, TestCancellationToken);
        rawRow.Should().NotBeNull("row must still exist in DB");
        rawRow!.IsDeleted.Should().BeTrue();
        rawRow.DeletedAt.Should().NotBeNull();
    }

    // ─── SG3-T3 (MOST IMPORTANT) ───────────────────────────────────────────────

    /// <summary>
    /// SG3-T3 (MOST IMPORTANT): Soft-delete an agent that has active ChatThreads
    /// cascades CloseThread() on all active threads.
    ///
    /// Setup:
    ///   1. Seed an agent
    ///   2. Seed 2 active ChatThreads linked to the agent (Status="active")
    ///   3. Seed 1 already-closed thread (Status="closed") → should NOT be re-closed
    ///   4. SoftDeleteUserAgentCommand
    ///   5. Verify the 2 active threads are now "closed"
    ///   6. Verify the pre-closed thread is still "closed" (unchanged)
    /// </summary>
    [Fact]
    public async Task SoftDelete_AgentWithChatThreads_CascadesCloseThread()
    {
        // Arrange
        var agent = BuildAgent("SG3-T3 Agent With Threads", systemDefined: false);
        _dbContext!.AgentDefinitions.Add(agent);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var agentId = agent.Id;

        // Seed 2 active threads + 1 already-closed thread
        var activeThread1Id = Guid.NewGuid();
        var activeThread2Id = Guid.NewGuid();
        var preClosedThreadId = Guid.NewGuid();

        _dbContext.ChatThreads.Add(BuildChatThreadEntity(activeThread1Id, agentId, status: "active", title: "SG3-T3 Active Thread 1"));
        _dbContext.ChatThreads.Add(BuildChatThreadEntity(activeThread2Id, agentId, status: "active", title: "SG3-T3 Active Thread 2"));
        _dbContext.ChatThreads.Add(BuildChatThreadEntity(preClosedThreadId, agentId, status: "closed", title: "SG3-T3 Pre-closed Thread"));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Pre-assert: 2 active + 1 closed
        var activesBefore = await _dbContext.ChatThreads.AsNoTracking()
            .CountAsync(t => t.AgentId == agentId && t.Status == "active", TestCancellationToken);
        activesBefore.Should().Be(2, "2 active threads were seeded");

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Act
        var command = new SoftDeleteUserAgentCommand(UserId: TestUserId, AgentId: agentId);
        await mediator.Send(command, TestCancellationToken);

        // Assert — both active threads are now closed (cascade)
        var activesAfter = await _dbContext.ChatThreads.AsNoTracking()
            .CountAsync(t => t.AgentId == agentId && t.Status == "active", TestCancellationToken);
        activesAfter.Should().Be(0,
            "SoftDeleteUserAgentCommand must cascade CloseThread() on all active threads linked to the agent");

        var closedAfter = await _dbContext.ChatThreads.AsNoTracking()
            .CountAsync(t => t.AgentId == agentId && t.Status == "closed", TestCancellationToken);
        closedAfter.Should().Be(3, "all 3 threads (2 newly closed + 1 pre-closed) should be closed");

        // Assert — agent is soft-deleted
        var rawAgent = await _dbContext.AgentDefinitions.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, TestCancellationToken);
        rawAgent!.IsDeleted.Should().BeTrue("agent must be soft-deleted");
    }

    // ─── SG3-T4 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SG3-T4: Restore a soft-deleted agent — agent becomes visible again,
    /// previously closed threads remain closed (by design).
    /// </summary>
    [Fact]
    public async Task Restore_SoftDeletedAgent_AgentVisibleAgain_ThreadsRemainClosed()
    {
        // Arrange — soft-delete an agent with an active thread first
        var agent = BuildAgent("SG3-T4 Restore Agent", systemDefined: false);
        _dbContext!.AgentDefinitions.Add(agent);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        var agentId = agent.Id;

        var threadId = Guid.NewGuid();
        _dbContext.ChatThreads.Add(BuildChatThreadEntity(threadId, agentId, status: "active", title: "SG3-T4 Thread"));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Soft-delete (cascade closes the thread)
        await mediator.Send(new SoftDeleteUserAgentCommand(UserId: TestUserId, AgentId: agentId), TestCancellationToken);

        // Verify deleted
        (await LoadAgent(agentId)).Should().BeNull("agent should be hidden after soft-delete");

        // Act — restore
        var restoreResult = await mediator.Send(new RestoreUserAgentCommand(UserId: TestUserId, AgentId: agentId), TestCancellationToken);

        // Assert — DTO returned successfully
        restoreResult.Should().NotBeNull();
        restoreResult.Id.Should().Be(agentId);
        restoreResult.Name.Should().Contain("SG3-T4 Restore Agent");

        // Assert — agent is visible again
        var afterRestore = await LoadAgent(agentId);
        afterRestore.Should().NotBeNull("restored agent must be visible again");
        afterRestore!.IsDeleted.Should().BeFalse();
        afterRestore.DeletedAt.Should().BeNull();

        // Assert — thread remains closed (not auto-reopened on restore)
        var thread = await _dbContext.ChatThreads.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == threadId, TestCancellationToken);
        thread.Should().NotBeNull();
        thread!.Status.Should().Be("closed",
            "ChatThreads closed during soft-delete must remain closed after restore (per spec)");
    }

    // ─── SG3-T5 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SG3-T5: Soft-delete a system-defined agent → throws SystemAgentProtectedException.
    ///
    /// System agents (IsSystemDefined=true) are seeded by the platform and must not
    /// be deleted by users. The domain's SoftDelete() throws InvalidOperationException,
    /// which the handler wraps into SystemAgentProtectedException (403).
    /// </summary>
    [Fact]
    public async Task SoftDelete_SystemDefinedAgent_Throws_SystemAgentProtectedException()
    {
        // Arrange — seed a system-defined agent using AgentDefinition.CreateSystem()
        var systemAgent = AgentDefinition.CreateSystem(
            name: "SG3-T5 System Agent Arbitro",
            description: "Test system agent",
            type: AgentType.Parse("Strategist"),
            config: AgentDefinitionConfig.Default(),
            typologySlug: "strategist");

        _dbContext!.AgentDefinitions.Add(systemAgent);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var agentId = systemAgent.Id;
        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Verify it's system-defined
        var loaded = await LoadAgent(agentId);
        loaded.Should().NotBeNull();
        loaded!.IsSystemDefined.Should().BeTrue("seeded via CreateSystem()");

        // Act & Assert — handler throws SystemAgentProtectedException (maps to 403 at endpoint)
        var ex = await Assert.ThrowsAsync<SystemAgentProtectedException>(
            () => mediator.Send(new SoftDeleteUserAgentCommand(UserId: TestUserId, AgentId: agentId), TestCancellationToken));

        ex.AgentId.Should().Be(agentId);
        ex.ErrorCode.Should().Be("SYSTEM_AGENT_PROTECTED");
        ex.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        // Assert — agent is still visible (not modified)
        (await LoadAgent(agentId)).Should().NotBeNull("system agent must NOT be soft-deleted on exception");
    }

    // ─── SG3-T6 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// SG3-T6: Create agent when user is at free-tier quota → throws TierQuotaExceededException (402).
    ///
    /// Free-tier allows MaxAgents=1. This test overrides the tier mock to return false
    /// for TierAction.CreateAgent, simulating a user at quota.
    ///
    /// NOTE: We cannot easily test the Redis-counter path (TierEnforcementService uses Redis),
    /// so we test the handler's quota check by mocking CanPerformAsync to return false
    /// (same pattern as SG2-T2 for RaptorRebuild).
    /// </summary>
    [Fact]
    public async Task CreateUserAgent_AtFreeTierQuota_ThrowsTierQuotaExceededException()
    {
        // Arrange — build a fresh service scope with a quota-exhausted tier mock
        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();
        // ISharedGameRepository mock — needed by CreateUserAgentCommandHandler
        services.AddScoped(_ => Mock.Of<ISharedGameRepository>());

        // Override tier mock: CreateAgent is blocked (quota reached), others allowed
        var quotaMock = new Mock<ITierEnforcementService>();
        quotaMock
            .Setup(t => t.CanPerformAsync(
                It.IsAny<Guid>(),
                TierAction.CreateAgent,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // QUOTA EXHAUSTED
        quotaMock
            .Setup(t => t.CanPerformAsync(
                It.IsAny<Guid>(),
                It.Is<TierAction>(a => a != TierAction.CreateAgent),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        quotaMock
            .Setup(t => t.GetLimitsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TierLimits.FreeTier); // MaxAgents=1

        services.AddScoped<ITierEnforcementService>(_ => quotaMock.Object);

        var quotaProvider = services.BuildServiceProvider();
        var mediator = quotaProvider.GetRequiredService<IMediator>();

        // Attempt to create an agent — should be blocked by quota
        var command = new CreateUserAgentCommand(
            UserId: TestUserId,
            GameId: TestGameId,
            AgentType: "RulesInterpreter",
            Name: "SG3-T6 Should Fail");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<TierQuotaExceededException>(
            () => mediator.Send(command, TestCancellationToken));

        ex.Resource.Should().Be("AgentSlots");
        ex.ErrorCode.Should().Be("AGENT_SLOT_QUOTA_EXCEEDED");
        ex.StatusCode.Should().Be(StatusCodes.Status402PaymentRequired);
        ex.MaxAllowed.Should().Be(1, "FreeTier.MaxAgents = 1");

        if (quotaProvider is IDisposable d) d.Dispose();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private AgentDefinition BuildAgent(string name, bool systemDefined)
    {
        if (systemDefined)
        {
            return AgentDefinition.CreateSystem(
                name: name,
                description: "SG3 test system agent",
                type: AgentType.Parse("Strategist"),
                config: AgentDefinitionConfig.Default(),
                typologySlug: "strategist");
        }

        var agent = AgentDefinition.Create(
            name: name,
            description: "SG3 integration test agent",
            type: AgentType.Parse("RulesInterpreter"),
            config: AgentDefinitionConfig.Default());

        return agent;
    }

    private Api.Infrastructure.Entities.ChatThreadEntity BuildChatThreadEntity(
        Guid id, Guid agentId, string status, string title)
    {
        return new Api.Infrastructure.Entities.ChatThreadEntity
        {
            Id = id,
            UserId = TestUserId,
            GameId = TestGameId,
            AgentId = agentId,
            Title = title,
            Status = status,
            MessagesJson = "[]",
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
        };
    }

    /// <summary>Loads agent from DB using the real repository (respects global IsDeleted filter).</summary>
    private async Task<AgentDefinition?> LoadAgent(Guid agentId)
    {
        var repo = _serviceProvider!.GetRequiredService<IAgentDefinitionRepository>();
        return await repo.GetByIdAsync(agentId, TestCancellationToken);
    }
}
