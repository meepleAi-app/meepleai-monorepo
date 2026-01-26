using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Tests for KnowledgeBase domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 23
/// </summary>
[Trait("Category", "Unit")]
public sealed class KnowledgeBaseDomainEventsTests
{
    #region AgentCreatedEvent Tests

    [Fact]
    public void AgentCreatedEvent_SetsProperties()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentCreatedEvent(agentId, "RulesAgent", "Catan Rules Helper");

        // Assert
        evt.AgentId.Should().Be(agentId);
        evt.Type.Should().Be("RulesAgent");
        evt.Name.Should().Be("Catan Rules Helper");
    }

    #endregion

    #region AgentActivatedEvent Tests

    [Fact]
    public void AgentActivatedEvent_SetsProperties()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentActivatedEvent(agentId);

        // Assert
        evt.AgentId.Should().Be(agentId);
    }

    #endregion

    #region AgentDeactivatedEvent Tests

    [Fact]
    public void AgentDeactivatedEvent_SetsProperties()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentDeactivatedEvent(agentId);

        // Assert
        evt.AgentId.Should().Be(agentId);
    }

    #endregion

    #region AgentConfiguredEvent Tests

    [Fact]
    public void AgentConfiguredEvent_SetsProperties()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var config = "{\"temperature\": 0.7, \"maxTokens\": 1000}";

        // Act
        var evt = new AgentConfiguredEvent(agentId, config);

        // Assert
        evt.AgentId.Should().Be(agentId);
        evt.ConfigurationJson.Should().Be(config);
    }

    #endregion

    #region AgentInvokedEvent Tests

    [Fact]
    public void AgentInvokedEvent_SetsAllProperties()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentInvokedEvent(
            agentId,
            "How do I setup Catan?",
            1500,
            0.0025m,
            "gpt-4",
            "openai");

        // Assert
        evt.AgentId.Should().Be(agentId);
        evt.Input.Should().Be("How do I setup Catan?");
        evt.TokensUsed.Should().Be(1500);
        evt.EstimatedCost.Should().Be(0.0025m);
        evt.ModelId.Should().Be("gpt-4");
        evt.Provider.Should().Be("openai");
    }

    #endregion

    #region AgentConfigurationCreatedEvent Tests

    [Fact]
    public void AgentConfigurationCreatedEvent_SetsProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentConfigurationCreatedEvent(configId, agentId, "gpt-4-turbo", 1);

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.AgentId.Should().Be(agentId);
        evt.LlmModel.Should().Be("gpt-4-turbo");
        evt.AgentMode.Should().Be(1);
    }

    #endregion

    #region AgentConfigurationActivatedEvent Tests

    [Fact]
    public void AgentConfigurationActivatedEvent_SetsProperties()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentConfigurationActivatedEvent(configId, agentId);

        // Assert
        evt.ConfigurationId.Should().Be(configId);
        evt.AgentId.Should().Be(agentId);
    }

    #endregion

    #region ChatThreadCreatedEvent Tests

    [Fact]
    public void ChatThreadCreatedEvent_SetsProperties()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var evt = new ChatThreadCreatedEvent(threadId, gameId, userId);

        // Assert
        evt.ThreadId.Should().Be(threadId);
        evt.GameId.Should().Be(gameId);
        evt.UserId.Should().Be(userId);
    }

    #endregion

    #region MessageAddedEvent Tests

    [Fact]
    public void MessageAddedEvent_SetsProperties()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        var evt = new MessageAddedEvent(threadId, messageId, "user", 150);

        // Assert
        evt.ThreadId.Should().Be(threadId);
        evt.MessageId.Should().Be(messageId);
        evt.Role.Should().Be("user");
        evt.ContentLength.Should().Be(150);
    }

    #endregion

    #region MessageUpdatedEvent Tests

    [Fact]
    public void MessageUpdatedEvent_SetsProperties()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        var evt = new MessageUpdatedEvent(threadId, messageId, 200);

        // Assert
        evt.ThreadId.Should().Be(threadId);
        evt.MessageId.Should().Be(messageId);
        evt.NewContentLength.Should().Be(200);
    }

    #endregion

    #region MessageDeletedEvent Tests

    [Fact]
    public void MessageDeletedEvent_SetsProperties()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        var evt = new MessageDeletedEvent(threadId, messageId);

        // Assert
        evt.ThreadId.Should().Be(threadId);
        evt.MessageId.Should().Be(messageId);
    }

    #endregion

    #region ThreadClosedEvent Tests

    [Fact]
    public void ThreadClosedEvent_SetsProperties()
    {
        // Arrange
        var threadId = Guid.NewGuid();

        // Act
        var evt = new ThreadClosedEvent(threadId, 25);

        // Assert
        evt.ThreadId.Should().Be(threadId);
        evt.TotalMessages.Should().Be(25);
    }

    #endregion

    #region ThreadReopenedEvent Tests

    [Fact]
    public void ThreadReopenedEvent_SetsProperties()
    {
        // Arrange
        var threadId = Guid.NewGuid();

        // Act
        var evt = new ThreadReopenedEvent(threadId);

        // Assert
        evt.ThreadId.Should().Be(threadId);
    }

    #endregion

    #region VectorDocumentIndexedEvent Tests

    [Fact]
    public void VectorDocumentIndexedEvent_SetsProperties()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var evt = new VectorDocumentIndexedEvent(documentId, gameId, 42);

        // Assert
        evt.DocumentId.Should().Be(documentId);
        evt.GameId.Should().Be(gameId);
        evt.ChunkCount.Should().Be(42);
    }

    #endregion

    #region VectorDocumentMetadataUpdatedEvent Tests

    [Fact]
    public void VectorDocumentMetadataUpdatedEvent_WithMetadata_SetsProperties()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var metadata = "{\"version\": \"2.0\", \"pages\": 50}";

        // Act
        var evt = new VectorDocumentMetadataUpdatedEvent(documentId, metadata);

        // Assert
        evt.DocumentId.Should().Be(documentId);
        evt.NewMetadata.Should().Be(metadata);
    }

    [Fact]
    public void VectorDocumentMetadataUpdatedEvent_WithNullMetadata_SetsNullMetadata()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        var evt = new VectorDocumentMetadataUpdatedEvent(documentId, null);

        // Assert
        evt.DocumentId.Should().Be(documentId);
        evt.NewMetadata.Should().BeNull();
    }

    #endregion

    #region VectorDocumentSearchedEvent Tests

    [Fact]
    public void VectorDocumentSearchedEvent_SetsProperties()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        var evt = new VectorDocumentSearchedEvent(documentId, "How to trade resources?");

        // Assert
        evt.DocumentId.Should().Be(documentId);
        evt.Query.Should().Be("How to trade resources?");
    }

    #endregion
}
