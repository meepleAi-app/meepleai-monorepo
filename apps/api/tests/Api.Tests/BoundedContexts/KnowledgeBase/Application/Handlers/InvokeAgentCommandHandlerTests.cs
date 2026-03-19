using Api.Infrastructure.Entities;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Integration tests for InvokeAgentCommandHandler.
/// Tests the complete agent invocation flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class InvokeAgentCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _mockAgentRepo;
    private readonly Mock<IEmbeddingRepository> _mockEmbeddingRepo;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly QualityTrackingDomainService _qualityTrackingService;
    private readonly Mock<ILogger<InvokeAgentCommandHandler>> _mockLogger;
    private readonly InvokeAgentCommandHandler _handler;

    public InvokeAgentCommandHandlerTests()
    {
        _mockAgentRepo = new Mock<IAgentRepository>();
        _mockEmbeddingRepo = new Mock<IEmbeddingRepository>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _qualityTrackingService = new QualityTrackingDomainService();
        _mockLogger = new Mock<ILogger<InvokeAgentCommandHandler>>();

        _handler = new InvokeAgentCommandHandler(
            _mockAgentRepo.Object,
            _mockEmbeddingRepo.Object,
            _mockEmbeddingService.Object,
            _qualityTrackingService,
            CreatePermissiveRagAccessServiceMock(),
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessfulResponse()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agent = CreateAgent(agentId, "Test Agent", AgentType.RagAgent);
        var queryVector = new Vector(new[] { 0.1f, 0.2f, 0.3f });
        var embeddings = CreateTestEmbeddings(gameId, queryVector);

        _mockAgentRepo.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _mockEmbeddingService.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Embeddings = new List<float[]> { queryVector.Values.ToArray() } });
        _mockEmbeddingRepo.Setup(r => r.SearchByVectorAsync(
            gameId,
            It.IsAny<Vector>(),
            It.IsAny<int>(),
            It.IsAny<double>(),
            It.IsAny<IReadOnlyList<Guid>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);
        _mockAgentRepo.Setup(r => r.UpdateAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new InvokeAgentCommand(
            AgentId: agentId,
            Query: "What are the rules for Catan?",
            GameId: gameId
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(agentId, result.AgentId);
        Assert.Equal("Test Agent", result.AgentName);
        Assert.Equal(AgentType.RagAgent.Value, result.AgentType);
        Assert.Equal("What are the rules for Catan?", result.Query);
        Assert.True(result.Confidence >= 0.0 && result.Confidence <= 1.0);
        Assert.NotEmpty(result.Results);

        // Verify agent was updated
        _mockAgentRepo.Verify(r => r.UpdateAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _mockAgentRepo.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var command = new InvokeAgentCommand(
            AgentId: agentId,
            Query: "test query"
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WithInactiveAgent_ThrowsInvalidOperationException()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = CreateAgent(agentId, "Inactive Agent", AgentType.RagAgent);
        agent.Deactivate();

        _mockAgentRepo.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new InvokeAgentCommand(
            AgentId: agentId,
            Query: "test query"
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("not active", ex.Message.ToLower());
    }

    [Fact]
    public async Task Handle_WithEmbeddingGenerationFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = CreateAgent(agentId, "Test Agent", AgentType.RagAgent);

        _mockAgentRepo.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _mockEmbeddingService.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmbeddingResult)null!);

        var command = new InvokeAgentCommand(
            AgentId: agentId,
            Query: "test query"
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("embedding", ex.Message.ToLower());
    }

    [Fact]
    public async Task Handle_WithNoCandidateEmbeddings_ReturnsEmptyResponse()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agent = CreateAgent(agentId, "Test Agent", AgentType.RagAgent);
        var queryVector = new Vector(new[] { 0.1f, 0.2f, 0.3f });

        _mockAgentRepo.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _mockEmbeddingService.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Embeddings = new List<float[]> { queryVector.Values.ToArray() } });
        _mockEmbeddingRepo.Setup(r => r.SearchByVectorAsync(
            gameId,
            It.IsAny<Vector>(),
            It.IsAny<int>(),
            It.IsAny<double>(),
            It.IsAny<IReadOnlyList<Guid>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding>());

        var command = new InvokeAgentCommand(
            AgentId: agentId,
            Query: "test query",
            GameId: gameId
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ResultCount);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal("Low", result.QualityLevel);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task Handle_WithNoGameId_ReturnsEmptyResponse()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agent = CreateAgent(agentId, "Test Agent", AgentType.RagAgent);
        var queryVector = new Vector(new[] { 0.1f, 0.2f, 0.3f });

        _mockAgentRepo.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _mockEmbeddingService.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Embeddings = new List<float[]> { queryVector.Values.ToArray() } });

        var command = new InvokeAgentCommand(
            AgentId: agentId,
            Query: "test query",
            GameId: null  // No game context
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ResultCount);
        Assert.Equal("Low", result.QualityLevel);
    }

    [Fact]
    public async Task Handle_IncrementsAgentInvocationCount()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agent = CreateAgent(agentId, "Test Agent", AgentType.RagAgent);
        var initialCount = agent.InvocationCount;
        var queryVector = new Vector(new[] { 0.1f, 0.2f, 0.3f });
        var embeddings = CreateTestEmbeddings(gameId, queryVector);

        _mockAgentRepo.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _mockEmbeddingService.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Embeddings = new List<float[]> { queryVector.Values.ToArray() } });
        _mockEmbeddingRepo.Setup(r => r.SearchByVectorAsync(
            gameId,
            It.IsAny<Vector>(),
            It.IsAny<int>(),
            It.IsAny<double>(),
            It.IsAny<IReadOnlyList<Guid>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);
        _mockAgentRepo.Setup(r => r.UpdateAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new InvokeAgentCommand(
            AgentId: agentId,
            Query: "test query",
            GameId: gameId
        );

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(initialCount + 1, agent.InvocationCount);
        Assert.NotNull(agent.LastInvokedAt);
    }

    [Fact]
    public async Task Handle_WithHighQualityResults_SetsHighQualityLevel()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agent = CreateAgent(agentId, "Test Agent", AgentType.RagAgent);
        var queryVector = new Vector(new[] { 0.9f, 0.9f, 0.9f });  // High similarity
        var embeddings = CreateTestEmbeddings(gameId, queryVector);

        _mockAgentRepo.Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _mockEmbeddingService.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Embeddings = new List<float[]> { queryVector.Values.ToArray() } });
        _mockEmbeddingRepo.Setup(r => r.SearchByVectorAsync(
            gameId,
            It.IsAny<Vector>(),
            It.IsAny<int>(),
            It.IsAny<double>(),
            It.IsAny<IReadOnlyList<Guid>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);
        _mockAgentRepo.Setup(r => r.UpdateAsync(It.IsAny<Agent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new InvokeAgentCommand(
            AgentId: agentId,
            Query: "test query",
            GameId: gameId
        );

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        // With high similarity vectors, we should get high confidence
        Assert.True(result.Confidence >= 0.80 || result.QualityLevel == "High");
    }

    // Helper methods
    private static Agent CreateAgent(Guid id, string name, AgentType type)
    {
        return new Agent(
            id,
            name,
            type,
            AgentStrategy.HybridSearch(),
            isActive: true
        );
    }

    private static List<Embedding> CreateTestEmbeddings(Guid gameId, Vector queryVector)
    {
        // Create similar but not identical vectors to avoid floating-point rounding issues
        var similarVector1 = new Vector(queryVector.Values.Select(v => v * 0.95f).ToArray());
        var similarVector2 = new Vector(queryVector.Values.Select(v => v * 0.90f).ToArray());

        return new List<Embedding>
        {
            new Embedding(
                id: Guid.NewGuid(),
                vectorDocumentId: gameId,
                textContent: "Test game rules for setup phase",
                vector: similarVector1,
                model: "test-embedding-model",
                chunkIndex: 0,
                pageNumber: 1
            ),
            new Embedding(
                id: Guid.NewGuid(),
                vectorDocumentId: gameId,
                textContent: "Test game rules for player turns",
                vector: similarVector2,
                model: "test-embedding-model",
                chunkIndex: 1,
                pageNumber: 1
            )
        };
    }

    private static IRagAccessService CreatePermissiveRagAccessServiceMock()
    {
        var mock = new Mock<IRagAccessService>();
        mock.Setup(s => s.CanAccessRagAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock.Object;
    }
}
