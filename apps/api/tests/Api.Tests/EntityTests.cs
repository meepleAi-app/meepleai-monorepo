using Api.Infrastructure.Entities;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Tests for entity classes to ensure proper property initialization and behavior
/// </summary>
public class EntityTests
{
    #region AgentEntity Tests

    [Fact]
    public void AgentEntity_DefaultConstructor_InitializesProperties()
    {
        // Act
        var agent = new AgentEntity();

        // Assert
        Assert.NotNull(agent.Chats);
        Assert.Empty(agent.Chats);
        Assert.NotEqual(default, agent.CreatedAt);
    }

    [Fact]
    public void AgentEntity_SetProperties_StoresValues()
    {
        // Arrange
        var agent = new AgentEntity();
        var testDate = DateTime.UtcNow;

        // Act
        agent.Id = "agent-123";
        agent.TenantId = "tenant-1";
        agent.GameId = "game-1";
        agent.Name = "Test Agent";
        agent.Kind = "qa";
        agent.CreatedAt = testDate;

        // Assert
        Assert.Equal("agent-123", agent.Id);
        Assert.Equal("tenant-1", agent.TenantId);
        Assert.Equal("game-1", agent.GameId);
        Assert.Equal("Test Agent", agent.Name);
        Assert.Equal("qa", agent.Kind);
        Assert.Equal(testDate, agent.CreatedAt);
    }

    [Fact]
    public void AgentEntity_NavigationProperties_CanBeSet()
    {
        // Arrange
        var agent = new AgentEntity();
        var tenant = new TenantEntity { Id = "tenant-1", Name = "Test Tenant" };
        var game = new GameEntity { Id = "game-1", Name = "Test Game", TenantId = "tenant-1" };

        // Act
        agent.Tenant = tenant;
        agent.Game = game;

        // Assert
        Assert.Equal(tenant, agent.Tenant);
        Assert.Equal(game, agent.Game);
    }

    [Fact]
    public void AgentEntity_ChatsCollection_CanAddItems()
    {
        // Arrange
        var agent = new AgentEntity { Id = "agent-1" };
        var chat = new ChatEntity { AgentId = "agent-1" };

        // Act
        agent.Chats.Add(chat);

        // Assert
        Assert.Single(agent.Chats);
        Assert.Contains(chat, agent.Chats);
    }

    #endregion

    #region ChatEntity Tests

    [Fact]
    public void ChatEntity_DefaultConstructor_InitializesProperties()
    {
        // Act
        var chat = new ChatEntity();

        // Assert
        Assert.NotEqual(Guid.Empty, chat.Id);
        Assert.NotNull(chat.Logs);
        Assert.Empty(chat.Logs);
        Assert.NotEqual(default, chat.StartedAt);
    }

    [Fact]
    public void ChatEntity_SetProperties_StoresValues()
    {
        // Arrange
        var chat = new ChatEntity();
        var testId = Guid.NewGuid();
        var testDate = DateTime.UtcNow;

        // Act
        chat.Id = testId;
        chat.TenantId = "tenant-1";
        chat.GameId = "game-1";
        chat.AgentId = "agent-1";
        chat.StartedAt = testDate;

        // Assert
        Assert.Equal(testId, chat.Id);
        Assert.Equal("tenant-1", chat.TenantId);
        Assert.Equal("game-1", chat.GameId);
        Assert.Equal("agent-1", chat.AgentId);
        Assert.Equal(testDate, chat.StartedAt);
    }

    [Fact]
    public void ChatEntity_NavigationProperties_CanBeSet()
    {
        // Arrange
        var chat = new ChatEntity();
        var tenant = new TenantEntity { Id = "tenant-1", Name = "Test Tenant" };
        var game = new GameEntity { Id = "game-1", Name = "Test Game", TenantId = "tenant-1" };
        var agent = new AgentEntity { Id = "agent-1", Name = "Test Agent" };

        // Act
        chat.Tenant = tenant;
        chat.Game = game;
        chat.Agent = agent;

        // Assert
        Assert.Equal(tenant, chat.Tenant);
        Assert.Equal(game, chat.Game);
        Assert.Equal(agent, chat.Agent);
    }

    [Fact]
    public void ChatEntity_LogsCollection_CanAddItems()
    {
        // Arrange
        var chatId = Guid.NewGuid();
        var chat = new ChatEntity { Id = chatId };
        var log = new ChatLogEntity { ChatId = chatId };

        // Act
        chat.Logs.Add(log);

        // Assert
        Assert.Single(chat.Logs);
        Assert.Contains(log, chat.Logs);
    }

    #endregion

    #region ChatLogEntity Tests

    [Fact]
    public void ChatLogEntity_DefaultConstructor_InitializesProperties()
    {
        // Act
        var log = new ChatLogEntity();

        // Assert
        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(Guid.Empty, log.ChatId);
        Assert.Null(log.MetadataJson);
        Assert.NotEqual(default, log.CreatedAt);
    }

    [Fact]
    public void ChatLogEntity_SetProperties_StoresValues()
    {
        // Arrange
        var log = new ChatLogEntity();
        var testId = Guid.NewGuid();
        var testChatId = Guid.NewGuid();
        var testDate = DateTime.UtcNow;

        // Act
        log.Id = testId;
        log.TenantId = "tenant-1";
        log.ChatId = testChatId;
        log.Level = "info";
        log.Message = "Test message";
        log.MetadataJson = "{\"key\":\"value\"}";
        log.CreatedAt = testDate;

        // Assert
        Assert.Equal(testId, log.Id);
        Assert.Equal("tenant-1", log.TenantId);
        Assert.Equal(testChatId, log.ChatId);
        Assert.Equal("info", log.Level);
        Assert.Equal("Test message", log.Message);
        Assert.Equal("{\"key\":\"value\"}", log.MetadataJson);
        Assert.Equal(testDate, log.CreatedAt);
    }

    [Fact]
    public void ChatLogEntity_NavigationProperties_CanBeSet()
    {
        // Arrange
        var log = new ChatLogEntity();
        var tenant = new TenantEntity { Id = "tenant-1", Name = "Test Tenant" };
        var chat = new ChatEntity { Id = Guid.NewGuid() };

        // Act
        log.Tenant = tenant;
        log.Chat = chat;

        // Assert
        Assert.Equal(tenant, log.Tenant);
        Assert.Equal(chat, log.Chat);
    }

    [Fact]
    public void ChatLogEntity_MetadataJson_AllowsNull()
    {
        // Arrange & Act
        var log = new ChatLogEntity
        {
            Level = "info",
            Message = "Test",
            MetadataJson = null
        };

        // Assert
        Assert.Null(log.MetadataJson);
    }

    #endregion

    #region VectorDocumentEntity Tests

    [Fact]
    public void VectorDocumentEntity_DefaultConstructor_InitializesProperties()
    {
        // Act
        var vectorDoc = new VectorDocumentEntity();

        // Assert
        Assert.Equal("pending", vectorDoc.IndexingStatus);
        Assert.Equal("openai/text-embedding-3-small", vectorDoc.EmbeddingModel);
        Assert.Equal(1536, vectorDoc.EmbeddingDimensions);
        Assert.Null(vectorDoc.IndexedAt);
        Assert.Null(vectorDoc.IndexingError);
    }

    [Fact]
    public void VectorDocumentEntity_SetProperties_StoresValues()
    {
        // Arrange
        var vectorDoc = new VectorDocumentEntity();
        var testDate = DateTime.UtcNow;

        // Act
        vectorDoc.Id = "vec-1";
        vectorDoc.TenantId = "tenant-1";
        vectorDoc.GameId = "game-1";
        vectorDoc.PdfDocumentId = "pdf-1";
        vectorDoc.ChunkCount = 50;
        vectorDoc.TotalCharacters = 5000;
        vectorDoc.IndexingStatus = "completed";
        vectorDoc.IndexedAt = testDate;
        vectorDoc.IndexingError = null;
        vectorDoc.EmbeddingModel = "custom-model";
        vectorDoc.EmbeddingDimensions = 768;

        // Assert
        Assert.Equal("vec-1", vectorDoc.Id);
        Assert.Equal("tenant-1", vectorDoc.TenantId);
        Assert.Equal("game-1", vectorDoc.GameId);
        Assert.Equal("pdf-1", vectorDoc.PdfDocumentId);
        Assert.Equal(50, vectorDoc.ChunkCount);
        Assert.Equal(5000, vectorDoc.TotalCharacters);
        Assert.Equal("completed", vectorDoc.IndexingStatus);
        Assert.Equal(testDate, vectorDoc.IndexedAt);
        Assert.Null(vectorDoc.IndexingError);
        Assert.Equal("custom-model", vectorDoc.EmbeddingModel);
        Assert.Equal(768, vectorDoc.EmbeddingDimensions);
    }

    [Fact]
    public void VectorDocumentEntity_NavigationProperties_CanBeSet()
    {
        // Arrange
        var vectorDoc = new VectorDocumentEntity();
        var tenant = new TenantEntity { Id = "tenant-1", Name = "Test Tenant" };
        var game = new GameEntity { Id = "game-1", Name = "Test Game", TenantId = "tenant-1" };
        var pdfDoc = new PdfDocumentEntity
        {
            Id = "pdf-1",
            TenantId = "tenant-1",
            GameId = "game-1",
            FileName = "test.pdf"
        };

        // Act
        vectorDoc.Tenant = tenant;
        vectorDoc.Game = game;
        vectorDoc.PdfDocument = pdfDoc;

        // Assert
        Assert.Equal(tenant, vectorDoc.Tenant);
        Assert.Equal(game, vectorDoc.Game);
        Assert.Equal(pdfDoc, vectorDoc.PdfDocument);
    }

    [Fact]
    public void VectorDocumentEntity_StatusProgression_CanBeTracked()
    {
        // Arrange
        var vectorDoc = new VectorDocumentEntity
        {
            Id = "vec-1",
            IndexingStatus = "pending"
        };

        // Act & Assert - Pending
        Assert.Equal("pending", vectorDoc.IndexingStatus);
        Assert.Null(vectorDoc.IndexedAt);

        // Act - Processing
        vectorDoc.IndexingStatus = "processing";
        Assert.Equal("processing", vectorDoc.IndexingStatus);

        // Act - Completed
        vectorDoc.IndexingStatus = "completed";
        vectorDoc.IndexedAt = DateTime.UtcNow;
        Assert.Equal("completed", vectorDoc.IndexingStatus);
        Assert.NotNull(vectorDoc.IndexedAt);
    }

    [Fact]
    public void VectorDocumentEntity_FailureState_CanStoreError()
    {
        // Arrange & Act
        var vectorDoc = new VectorDocumentEntity
        {
            Id = "vec-1",
            IndexingStatus = "failed",
            IndexingError = "Embedding API error"
        };

        // Assert
        Assert.Equal("failed", vectorDoc.IndexingStatus);
        Assert.Equal("Embedding API error", vectorDoc.IndexingError);
        Assert.Null(vectorDoc.IndexedAt);
    }

    [Fact]
    public void VectorDocumentEntity_ChunkMetrics_CanBeTracked()
    {
        // Arrange & Act
        var vectorDoc = new VectorDocumentEntity
        {
            Id = "vec-1",
            ChunkCount = 100,
            TotalCharacters = 15000
        };

        // Assert
        Assert.Equal(100, vectorDoc.ChunkCount);
        Assert.Equal(15000, vectorDoc.TotalCharacters);
    }

    #endregion
}
