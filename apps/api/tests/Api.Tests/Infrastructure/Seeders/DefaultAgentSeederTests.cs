using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

/// <summary>
/// Tests for DefaultAgentSeeder to ensure POC agent is created correctly.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class DefaultAgentSeederTests : IAsyncLifetime
{
    private MeepleAiDbContext _dbContext = default!;
    private ILogger _logger = default!;
    private Guid _adminUserId;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"DefaultAgentSeeder_Test_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        _logger = new LoggerFactory().CreateLogger<DefaultAgentSeederTests>();

        // Create admin user for CreatedBy reference
        _adminUserId = Guid.NewGuid();
        await _dbContext.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task SeedDefaultAgentAsync_CreatesAgentAndConfiguration()
    {
        // Act
        await DefaultAgentSeeder.SeedDefaultAgentAsync(_dbContext, _adminUserId, _logger);

        // Assert - Agent created
        var agent = await _dbContext.Set<AgentEntity>()
            .FirstOrDefaultAsync(a => a.Name == "MeepleAssistant POC");

        agent.Should().NotBeNull();
        agent!.Type.Should().Be("RAG");
        agent.StrategyName.Should().Be("SingleModel");
        agent.StrategyParametersJson.Should().Be("{}");
        agent.IsActive.Should().BeTrue();
        agent.InvocationCount.Should().Be(0);
        agent.LastInvokedAt.Should().BeNull();

        // Assert - Configuration created
        var config = await _dbContext.Set<AgentConfigurationEntity>()
            .FirstOrDefaultAsync(c => c.AgentId == agent.Id);

        config.Should().NotBeNull();
        config!.LlmProvider.Should().Be(0); // OpenRouter
        config.LlmModel.Should().Be("anthropic/claude-3-haiku");
        config.AgentMode.Should().Be(0); // Chat
        config.Temperature.Should().Be(0.3m);
        config.MaxTokens.Should().Be(2048);
        config.IsCurrent.Should().BeTrue();
        config.CreatedBy.Should().Be(_adminUserId);
        config.SystemPromptOverride.Should().NotBeNullOrEmpty();
        config.SystemPromptOverride.Should().Contain("MeepleAssistant");
        config.SystemPromptOverride.Should().Contain("{RAG_CONTEXT}");
        config.SelectedDocumentIdsJson.Should().Be("[]");
    }

    [Fact]
    public async Task SeedDefaultAgentAsync_IsIdempotent()
    {
        // Arrange - Seed once
        await DefaultAgentSeeder.SeedDefaultAgentAsync(_dbContext, _adminUserId, _logger);
        var firstAgent = await _dbContext.Set<AgentEntity>()
            .FirstAsync(a => a.Name == "MeepleAssistant POC");
        var firstAgentId = firstAgent.Id;

        // Act - Seed again
        await DefaultAgentSeeder.SeedDefaultAgentAsync(_dbContext, _adminUserId, _logger);

        // Assert - No duplicates
        var agents = await _dbContext.Set<AgentEntity>()
            .Where(a => a.Name == "MeepleAssistant POC")
            .ToListAsync();

        agents.Should().HaveCount(1);
        agents[0].Id.Should().Be(firstAgentId); // Same ID, not recreated

        var configs = await _dbContext.Set<AgentConfigurationEntity>()
            .Where(c => c.AgentId == firstAgentId)
            .ToListAsync();

        configs.Should().HaveCount(1); // Only one configuration
    }

    [Fact]
    public async Task SeedDefaultAgentAsync_SystemPromptIsValid()
    {
        // Act
        await DefaultAgentSeeder.SeedDefaultAgentAsync(_dbContext, _adminUserId, _logger);

        // Assert
        var config = await _dbContext.Set<AgentConfigurationEntity>()
            .FirstAsync();

        var prompt = config.SystemPromptOverride!;

        // Validate prompt structure
        prompt.Should().Contain("ROLE & EXPERTISE");
        prompt.Should().Contain("KNOWLEDGE BASE INTEGRATION");
        prompt.Should().Contain("RESPONSE GUIDELINES");
        prompt.Should().Contain("INTERACTION PATTERNS");
        prompt.Should().Contain("LIMITATIONS & BOUNDARIES");
        prompt.Should().Contain("OUTPUT FORMAT");

        // Validate RAG placeholder
        prompt.Should().Contain("{RAG_CONTEXT}");

        // Validate professional tone guidance
        prompt.Should().Contain("professional");
        prompt.Should().Contain("authoritative");

        // Validate uncertainty handling
        prompt.Should().Contain("I don't have complete information");

        // Validate length constraint (5000 chars limit)
        prompt.Length.Should().BeLessThan(5000);
    }

    [Fact]
    public async Task SeedDefaultAgentAsync_ConfigurationIsOptimizedForCost()
    {
        // Act
        await DefaultAgentSeeder.SeedDefaultAgentAsync(_dbContext, _adminUserId, _logger);

        // Assert
        var config = await _dbContext.Set<AgentConfigurationEntity>().FirstAsync();

        // Haiku is quasi-free
        config.LlmModel.Should().Contain("haiku");

        // Low temperature for deterministic, efficient responses
        config.Temperature.Should().BeLessThanOrEqualTo(0.5m);

        // Reasonable token limit (not excessive)
        config.MaxTokens.Should().BeLessThanOrEqualTo(4096);
    }

    [Fact]
    public async Task SeedDefaultAgentAsync_AgentIsReadyForToolCalling()
    {
        // Act
        await DefaultAgentSeeder.SeedDefaultAgentAsync(_dbContext, _adminUserId, _logger);

        // Assert
        var agent = await _dbContext.Set<AgentEntity>().FirstAsync();
        var config = await _dbContext.Set<AgentConfigurationEntity>().FirstAsync();

        // Agent type supports tool calling
        agent.Type.Should().Be("RAG");

        // Chat mode supports tool integration
        config.AgentMode.Should().Be(0); // Chat

        // Active and ready
        agent.IsActive.Should().BeTrue();
        config.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task SeedDefaultAgentAsync_ConfigurationSupportsRAGIntegration()
    {
        // Act
        await DefaultAgentSeeder.SeedDefaultAgentAsync(_dbContext, _adminUserId, _logger);

        // Assert
        var config = await _dbContext.Set<AgentConfigurationEntity>().FirstAsync();

        // Has document selection capability (empty for now)
        config.SelectedDocumentIdsJson.Should().NotBeNull();

        // System prompt has RAG placeholder
        config.SystemPromptOverride.Should().Contain("{RAG_CONTEXT}");

        // Can be upgraded to advanced RAG strategies
        var agent = await _dbContext.Set<AgentEntity>().FirstAsync();
        agent.StrategyName.Should().Be("SingleModel"); // Baseline, upgradeable
    }
}
