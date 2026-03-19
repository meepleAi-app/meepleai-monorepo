using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for GetAgentByIdQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAgentByIdQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _mockRepository;
    private readonly Mock<ILogger<GetAgentByIdQueryHandler>> _mockLogger;
    private readonly GetAgentByIdQueryHandler _handler;

    public GetAgentByIdQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentRepository>();
        _mockLogger = new Mock<ILogger<GetAgentByIdQueryHandler>>();
        _handler = new GetAgentByIdQueryHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithExistingAgent_ReturnsAgentDto()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agentType = AgentType.Parse("RAG");
        var strategy = AgentStrategy.Custom("TestStrategy", new Dictionary<string, object>());
        var agent = new Agent(agentId, "TestAgent", agentType, strategy, true);

        _mockRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var query = new GetAgentByIdQuery(agentId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(agentId, result.Id);
        Assert.Equal("TestAgent", result.Name);
        Assert.Equal("RAG", result.Type);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Handle_WithNonExistentAgent_ReturnsNull()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var query = new GetAgentByIdQuery(agentId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
