using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for DefaultAgentSeeder POC with RAG context injection.
/// Verifies end-to-end RAG workflow: Vector retrieval → Context injection → LLM response.
/// </summary>
[Trait("Category", "Integration")]
[Trait("BoundedContext", "KnowledgeBase")]
[Collection("E2E")]
internal sealed class DefaultAgentRagIntegrationTests : IClassFixture<Api.Tests.E2E.Infrastructure.E2EWebApplicationFactory>
{
    private readonly Api.Tests.E2E.Infrastructure.E2EWebApplicationFactory _factory;
    private readonly IServiceScope _scope;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;

    // Test constants
    private readonly Guid _agentId = Guid.Parse("49365068-d1db-4a66-aff5-f9fadca2763b");
    private readonly Guid _vectorDocId = Guid.Parse("8b78c72a-b5bc-454e-875b-22754a673c40"); // Azul
    private readonly Guid _testUserId = Guid.Parse("ce8ba2ac-bae5-4052-a6f7-6f71a6f5728e"); // Admin user

    public DefaultAgentRagIntegrationTests(Api.Tests.E2E.Infrastructure.E2EWebApplicationFactory factory)
    {
        _factory = factory;
        _scope = _factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task SendAgentMessage_WithRagContext_UsesRetrievedChunks()
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: _agentId,
            UserQuestion: "How do you score points in Azul?",
            UserId: _testUserId,
            ChatThreadId: null
        );

        // Act
        var events = new List<object>();
        await foreach (var @event in _mediator.CreateStream(command))
        {
            events.Add(@event);
        }

        // Assert
        events.Should().NotBeEmpty("Agent should produce response events");

        // Verify final answer event
        var answerEvent = events.LastOrDefault();
        answerEvent.Should().NotBeNull("Should have final answer event");

        // Extract response (event structure may vary)
        var response = answerEvent!.ToString();
        response.Should().NotBeNullOrWhiteSpace("Agent should provide answer");
        response.Should().NotBeNullOrWhiteSpace("Agent should provide answer");

        // Verify RAG characteristics
        response.Should().ContainAny(
            "tile", "pattern", "wall", "floor", "factory", // Azul-specific terms
            "Should contain game-specific terminology from RAG context"
        );

        // Professional tone check
        response.Should().NotContain(
            "I don't know",
            "I'm not sure",
            "Should not show uncertainty for documented rules"
        );
    }

    [Fact]
    public async Task SendAgentMessage_WithoutRagDocs_UsesGeneralKnowledge()
    {
        // Arrange - Clear documents from config temporarily
        var config = await _dbContext.Set<Api.Infrastructure.Entities.KnowledgeBase.AgentConfigurationEntity>()
            .FirstAsync(c => c.AgentId == _agentId);

        var originalDocs = config.SelectedDocumentIdsJson;
        config.SelectedDocumentIdsJson = "[]"; // Remove RAG docs
        await _dbContext.SaveChangesAsync();

        try
        {
            var command = new SendAgentMessageCommand(
                AgentId: _agentId,
                UserQuestion: "What is Azul?",
                UserId: _testUserId,
                ChatThreadId: null
            );

            // Act
            var events = new List<object>();
            await foreach (var @event in _mediator.CreateStream(command))
            {
                events.Add(@event);
            }

            // Assert
            events.Should().NotBeEmpty("Agent should produce events");

            var answerEvent = events.LastOrDefault();
            answerEvent.Should().NotBeNull("Should have final event");
            var response = answerEvent!.ToString();

            // Should indicate general knowledge mode
            response.Should().ContainAny(
                "general",
                "based on",
                "typically",
                "Should indicate source of knowledge when no RAG context"
            );
        }
        finally
        {
            // Restore configuration
            config.SelectedDocumentIdsJson = originalDocs;
            await _dbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SendAgentMessage_ComplexQuery_MaintainsProfessionalTone()
    {
        // Arrange
        var command = new SendAgentMessageCommand(
            AgentId: _agentId,
            UserQuestion: "What's the best strategy for the early game in Azul?",
            UserId: _testUserId,
            ChatThreadId: null
        );

        // Act
        var events = new List<object>();
        await foreach (var @event in _mediator.CreateStream(command))
        {
            events.Add(@event);
        }

        // Assert
        events.Should().NotBeEmpty("Agent should respond");

        var answerEvent = events.LastOrDefault();
        answerEvent.Should().NotBeNull("Should have response event");
        var response = answerEvent!.ToString();

        // Professional characteristics
        response.Should().NotContainAny(
            "lol", "omg", "btw", "😊", "😄",
            "Should maintain professional tone without casual language or emoji"
        );

        // Strategic analysis patterns
        response.Should().ContainAny(
            "strategy", "option", "consider", "recommend", "advantage",
            "Should provide strategic analysis vocabulary"
        );
    }

    [Fact]
    public async Task AgentConfiguration_HasRagPlaceholder_InSystemPrompt()
    {
        // Arrange & Act
        var config = await _dbContext.Set<Api.Infrastructure.Entities.KnowledgeBase.AgentConfigurationEntity>()
            .FirstAsync(c => c.AgentId == _agentId);

        // Assert
        config.SystemPromptOverride.Should().NotBeNullOrWhiteSpace();
        config.SystemPromptOverride.Should().Contain(
            "{RAG_CONTEXT}",
            "System prompt must have RAG context placeholder for injection"
        );

        // Verify professional structure
        config.SystemPromptOverride.Should().ContainAll(
            "ROLE & EXPERTISE",
            "KNOWLEDGE BASE INTEGRATION",
            "RESPONSE GUIDELINES",
            "Professional system prompt should have clear sections"
        );
    }

    [Fact]
    public async Task VectorDocument_IsLinkedToAgent_ViaConfiguration()
    {
        // Arrange & Act
        var config = await _dbContext.Set<Api.Infrastructure.Entities.KnowledgeBase.AgentConfigurationEntity>()
            .FirstAsync(c => c.AgentId == _agentId);

        // Assert
        config.SelectedDocumentIdsJson.Should().NotBeNullOrWhiteSpace();
        config.SelectedDocumentIdsJson.Should().Contain(_vectorDocId.ToString());

        // Verify it's current config
        config.IsCurrent.Should().BeTrue("Agent should have active configuration");
    }
}
