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
using Api.Infrastructure.Entities.SharedGameCatalog;
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
/// - SG3-T2..T6: retired with the user-owned agent routes (#4138), see the note in the body
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
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = TestGameId,
            Title = "SG3 Agent Lifecycle Test Game",
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

    // Issue #4138: SG3-T2..T6 stood here. T2-T5 exercised SoftDeleteUserAgentCommand and
    // RestoreUserAgentCommand, retired with DELETE /agents/{id} and POST /agents/{id}/restore:
    // those routes let any authenticated user delete or restore any non-system agent, and
    // user-owned agents no longer exist. T6 exercised the per-user agent quota, retired by #4160.
    // Admins delete agents through DELETE /admin/agent-definitions/{id}.

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private AgentDefinition BuildAgent(string name, bool systemDefined)
    {
        if (systemDefined)
        {
            return AgentDefinition.CreateSystem(
                name: name,
                description: "SG3 test system agent",
                config: AgentDefinitionConfig.Default(),
                typologySlug: "strategist");
        }

        var agent = AgentDefinition.Create(
            name: name,
            description: "SG3 integration test agent",
            config: AgentDefinitionConfig.Default());

        return agent;
    }

    /// <summary>Loads agent from DB using the real repository (respects global IsDeleted filter).</summary>
    private async Task<AgentDefinition?> LoadAgent(Guid agentId)
    {
        var repo = _serviceProvider!.GetRequiredService<IAgentDefinitionRepository>();
        return await repo.GetByIdAsync(agentId, TestCancellationToken);
    }
}
