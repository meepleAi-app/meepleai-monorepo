using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Tests for KnowledgeBase domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 21 PR#3
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

    [Fact]
    public void AgentCreatedEvent_WithDifferentTypes_SetsCorrectType()
    {
        // Arrange
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();

        // Act
        var evt1 = new AgentCreatedEvent(agentId1, "RulesAgent", "Rules Helper");
        var evt2 = new AgentCreatedEvent(agentId2, "StrategyAgent", "Strategy Guide");

        // Assert
        evt1.Type.Should().Be("RulesAgent");
        evt1.Name.Should().Be("Rules Helper");
        evt2.Type.Should().Be("StrategyAgent");
        evt2.Name.Should().Be("Strategy Guide");
    }

    [Fact]
    public void AgentCreatedEvent_WithEmptyName_SetsEmptyName()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentCreatedEvent(agentId, "TestAgent", "");

        // Assert
        evt.Name.Should().BeEmpty();
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

    [Fact]
    public void AgentActivatedEvent_WithDifferentIds_SetsCorrectId()
    {
        // Arrange
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();

        // Act
        var evt1 = new AgentActivatedEvent(agentId1);
        var evt2 = new AgentActivatedEvent(agentId2);

        // Assert
        evt1.AgentId.Should().Be(agentId1);
        evt2.AgentId.Should().Be(agentId2);
        evt1.AgentId.Should().NotBe(evt2.AgentId);
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

    [Fact]
    public void AgentDeactivatedEvent_WithDifferentIds_SetsCorrectId()
    {
        // Arrange
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();

        // Act
        var evt1 = new AgentDeactivatedEvent(agentId1);
        var evt2 = new AgentDeactivatedEvent(agentId2);

        // Assert
        evt1.AgentId.Should().Be(agentId1);
        evt2.AgentId.Should().Be(agentId2);
        evt1.AgentId.Should().NotBe(evt2.AgentId);
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

    [Fact]
    public void AgentConfiguredEvent_WithEmptyJson_SetsEmptyConfiguration()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentConfiguredEvent(agentId, "{}");

        // Assert
        evt.ConfigurationJson.Should().Be("{}");
    }

    [Fact]
    public void AgentConfiguredEvent_WithComplexJson_SetsComplexConfiguration()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var complexJson = "{\"model\":\"gpt-4\",\"settings\":{\"temperature\":0.8,\"topP\":0.9}}";

        // Act
        var evt = new AgentConfiguredEvent(agentId, complexJson);

        // Assert
        evt.ConfigurationJson.Should().Be(complexJson);
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

    [Fact]
    public void AgentInvokedEvent_WithZeroTokens_SetsZeroCost()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentInvokedEvent(
            agentId,
            "Quick question",
            0,
            0.0m,
            "gpt-3.5-turbo",
            "openai");

        // Assert
        evt.TokensUsed.Should().Be(0);
        evt.EstimatedCost.Should().Be(0.0m);
    }

    [Fact]
    public void AgentInvokedEvent_WithHighTokenUsage_SetsHighCost()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentInvokedEvent(
            agentId,
            "Complex multi-part question",
            10000,
            0.5m,
            "gpt-4-32k",
            "openai");

        // Assert
        evt.TokensUsed.Should().Be(10000);
        evt.EstimatedCost.Should().Be(0.5m);
    }

    [Fact]
    public void AgentInvokedEvent_WithDifferentProviders_SetsCorrectProvider()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var evt1 = new AgentInvokedEvent(agentId, "Test", 100, 0.01m, "gpt-4", "openai");
        var evt2 = new AgentInvokedEvent(agentId, "Test", 100, 0.01m, "claude-3", "anthropic");

        // Assert
        evt1.Provider.Should().Be("openai");
        evt1.ModelId.Should().Be("gpt-4");
        evt2.Provider.Should().Be("anthropic");
        evt2.ModelId.Should().Be("claude-3");
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

    [Fact]
    public void AgentConfigurationCreatedEvent_WithDifferentModels_SetsCorrectModel()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        // Act
        var evt1 = new AgentConfigurationCreatedEvent(configId, agentId, "gpt-4", 1);
        var evt2 = new AgentConfigurationCreatedEvent(configId, agentId, "claude-3", 2);

        // Assert
        evt1.LlmModel.Should().Be("gpt-4");
        evt1.AgentMode.Should().Be(1);
        evt2.LlmModel.Should().Be("claude-3");
        evt2.AgentMode.Should().Be(2);
    }

    [Fact]
    public void AgentConfigurationCreatedEvent_WithZeroMode_SetsMode()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        // Act
        var evt = new AgentConfigurationCreatedEvent(configId, agentId, "gpt-3.5-turbo", 0);

        // Assert
        evt.AgentMode.Should().Be(0);
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

    [Fact]
    public void AgentConfigurationActivatedEvent_WithDifferentIds_SetsCorrectValues()
    {
        // Arrange
        var configId1 = Guid.NewGuid();
        var agentId1 = Guid.NewGuid();
        var configId2 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();

        // Act
        var evt1 = new AgentConfigurationActivatedEvent(configId1, agentId1);
        var evt2 = new AgentConfigurationActivatedEvent(configId2, agentId2);

        // Assert
        evt1.ConfigurationId.Should().Be(configId1);
        evt1.AgentId.Should().Be(agentId1);
        evt2.ConfigurationId.Should().Be(configId2);
        evt2.AgentId.Should().Be(agentId2);
    }

    #endregion

    #region AgentSessionCreatedEvent Tests

    [Fact]
    public void AgentSessionCreatedEvent_SetsAllProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var agentDefinitionId = Guid.NewGuid();
        var gameSessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var evt = new AgentSessionCreatedEvent(sessionId, agentDefinitionId, gameSessionId, userId);

        // Assert
        evt.AgentSessionId.Should().Be(sessionId);
        evt.AgentDefinitionId.Should().Be(agentDefinitionId);
        evt.GameSessionId.Should().Be(gameSessionId);
        evt.UserId.Should().Be(userId);
    }

    [Fact]
    public void AgentSessionCreatedEvent_WithDifferentIds_SetsCorrectValues()
    {
        // Arrange
        var sessionId1 = Guid.NewGuid();
        var agentDefinitionId1 = Guid.NewGuid();
        var gameSessionId1 = Guid.NewGuid();
        var userId1 = Guid.NewGuid();

        var sessionId2 = Guid.NewGuid();
        var agentDefinitionId2 = Guid.NewGuid();
        var gameSessionId2 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        // Act
        var evt1 = new AgentSessionCreatedEvent(sessionId1, agentDefinitionId1, gameSessionId1, userId1);
        var evt2 = new AgentSessionCreatedEvent(sessionId2, agentDefinitionId2, gameSessionId2, userId2);

        // Assert
        evt1.AgentSessionId.Should().Be(sessionId1);
        evt1.AgentDefinitionId.Should().Be(agentDefinitionId1);
        evt2.AgentSessionId.Should().Be(sessionId2);
        evt2.AgentDefinitionId.Should().Be(agentDefinitionId2);
    }

    #endregion

    #region AgentSessionEndedEvent Tests

    [Fact]
    public void AgentSessionEndedEvent_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var duration = TimeSpan.FromMinutes(15);

        // Act
        var evt = new AgentSessionEndedEvent(sessionId, duration);

        // Assert
        evt.AgentSessionId.Should().Be(sessionId);
        evt.Duration.Should().Be(duration);
    }

    [Fact]
    public void AgentSessionEndedEvent_WithZeroDuration_SetsZero()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new AgentSessionEndedEvent(sessionId, TimeSpan.Zero);

        // Assert
        evt.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AgentSessionEndedEvent_WithLongDuration_SetsCorrectDuration()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var duration = TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(30));

        // Act
        var evt = new AgentSessionEndedEvent(sessionId, duration);

        // Assert
        evt.Duration.Should().Be(TimeSpan.FromMinutes(150));
    }

    #endregion

    #region AgentSessionStateUpdatedEvent Tests

    [Fact]
    public void AgentSessionStateUpdatedEvent_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var stateJson = "{\"currentStep\":3,\"totalSteps\":5}";

        // Act
        var evt = new AgentSessionStateUpdatedEvent(sessionId, stateJson);

        // Assert
        evt.AgentSessionId.Should().Be(sessionId);
        evt.NewStateJson.Should().Be(stateJson);
    }

    [Fact]
    public void AgentSessionStateUpdatedEvent_WithEmptyState_SetsEmptyJson()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new AgentSessionStateUpdatedEvent(sessionId, "{}");

        // Assert
        evt.NewStateJson.Should().Be("{}");
    }

    [Fact]
    public void AgentSessionStateUpdatedEvent_WithComplexState_SetsComplexJson()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var complexState = "{\"progress\":0.75,\"data\":{\"answers\":[1,2,3]}}";

        // Act
        var evt = new AgentSessionStateUpdatedEvent(sessionId, complexState);

        // Assert
        evt.NewStateJson.Should().Be(complexState);
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

    [Fact]
    public void ChatThreadCreatedEvent_WithDifferentIds_SetsCorrectValues()
    {
        // Arrange
        var threadId1 = Guid.NewGuid();
        var gameId1 = Guid.NewGuid();
        var userId1 = Guid.NewGuid();

        var threadId2 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        // Act
        var evt1 = new ChatThreadCreatedEvent(threadId1, gameId1, userId1);
        var evt2 = new ChatThreadCreatedEvent(threadId2, gameId2, userId2);

        // Assert
        evt1.ThreadId.Should().Be(threadId1);
        evt1.GameId.Should().Be(gameId1);
        evt2.ThreadId.Should().Be(threadId2);
        evt2.GameId.Should().Be(gameId2);
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

    [Fact]
    public void MessageAddedEvent_WithAssistantRole_SetsCorrectRole()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        var evt = new MessageAddedEvent(threadId, messageId, "assistant", 512);

        // Assert
        evt.Role.Should().Be("assistant");
        evt.ContentLength.Should().Be(512);
    }

    [Fact]
    public void MessageAddedEvent_WithZeroLength_SetsZero()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        var evt = new MessageAddedEvent(threadId, messageId, "user", 0);

        // Assert
        evt.ContentLength.Should().Be(0);
    }

    [Fact]
    public void MessageAddedEvent_WithLargeContent_SetsLargeLength()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        var evt = new MessageAddedEvent(threadId, messageId, "assistant", 10000);

        // Assert
        evt.ContentLength.Should().Be(10000);
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

    [Fact]
    public void MessageUpdatedEvent_WithZeroLength_SetsZero()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        var evt = new MessageUpdatedEvent(threadId, messageId, 0);

        // Assert
        evt.NewContentLength.Should().Be(0);
    }

    [Fact]
    public void MessageUpdatedEvent_WithIncreasedLength_SetsNewLength()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        var evt = new MessageUpdatedEvent(threadId, messageId, 1024);

        // Assert
        evt.NewContentLength.Should().Be(1024);
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

    [Fact]
    public void MessageDeletedEvent_WithDifferentIds_SetsCorrectValues()
    {
        // Arrange
        var threadId1 = Guid.NewGuid();
        var messageId1 = Guid.NewGuid();
        var threadId2 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();

        // Act
        var evt1 = new MessageDeletedEvent(threadId1, messageId1);
        var evt2 = new MessageDeletedEvent(threadId2, messageId2);

        // Assert
        evt1.ThreadId.Should().Be(threadId1);
        evt1.MessageId.Should().Be(messageId1);
        evt2.ThreadId.Should().Be(threadId2);
        evt2.MessageId.Should().Be(messageId2);
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

    [Fact]
    public void ThreadClosedEvent_WithZeroMessages_SetsZero()
    {
        // Arrange
        var threadId = Guid.NewGuid();

        // Act
        var evt = new ThreadClosedEvent(threadId, 0);

        // Assert
        evt.TotalMessages.Should().Be(0);
    }

    [Fact]
    public void ThreadClosedEvent_WithManyMessages_SetsCorrectCount()
    {
        // Arrange
        var threadId = Guid.NewGuid();

        // Act
        var evt = new ThreadClosedEvent(threadId, 100);

        // Assert
        evt.TotalMessages.Should().Be(100);
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

    [Fact]
    public void ThreadReopenedEvent_WithDifferentIds_SetsCorrectId()
    {
        // Arrange
        var threadId1 = Guid.NewGuid();
        var threadId2 = Guid.NewGuid();

        // Act
        var evt1 = new ThreadReopenedEvent(threadId1);
        var evt2 = new ThreadReopenedEvent(threadId2);

        // Assert
        evt1.ThreadId.Should().Be(threadId1);
        evt2.ThreadId.Should().Be(threadId2);
        evt1.ThreadId.Should().NotBe(evt2.ThreadId);
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

    [Fact]
    public void VectorDocumentIndexedEvent_WithZeroChunks_SetsZero()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var evt = new VectorDocumentIndexedEvent(documentId, gameId, 0);

        // Assert
        evt.ChunkCount.Should().Be(0);
    }

    [Fact]
    public void VectorDocumentIndexedEvent_WithManyChunks_SetsLargeCount()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var evt = new VectorDocumentIndexedEvent(documentId, gameId, 500);

        // Assert
        evt.ChunkCount.Should().Be(500);
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

    [Fact]
    public void VectorDocumentMetadataUpdatedEvent_WithEmptyMetadata_SetsEmpty()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        var evt = new VectorDocumentMetadataUpdatedEvent(documentId, "{}");

        // Assert
        evt.NewMetadata.Should().Be("{}");
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

    [Fact]
    public void VectorDocumentSearchedEvent_WithEmptyQuery_SetsEmptyQuery()
    {
        // Arrange
        var documentId = Guid.NewGuid();

        // Act
        var evt = new VectorDocumentSearchedEvent(documentId, "");

        // Assert
        evt.Query.Should().BeEmpty();
    }

    [Fact]
    public void VectorDocumentSearchedEvent_WithComplexQuery_SetsComplexQuery()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var complexQuery = "What are the advanced strategies for resource management in Terraforming Mars?";

        // Act
        var evt = new VectorDocumentSearchedEvent(documentId, complexQuery);

        // Assert
        evt.Query.Should().Be(complexQuery);
    }

    #endregion
}
