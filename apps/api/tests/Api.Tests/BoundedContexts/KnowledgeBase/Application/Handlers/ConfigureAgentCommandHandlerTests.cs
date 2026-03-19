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
/// Tests for ConfigureAgentCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ConfigureAgentCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _mockRepository;
    private readonly Mock<ILogger<ConfigureAgentCommandHandler>> _mockLogger;
    private readonly ConfigureAgentCommandHandler _handler;

    public ConfigureAgentCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentRepository>();
        _mockLogger = new Mock<ILogger<ConfigureAgentCommandHandler>>();
        _handler = new ConfigureAgentCommandHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidAgent_ConfiguresSuccessfully()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var agentType = AgentType.Parse("RAG");
        var strategy = AgentStrategy.Custom("OldStrategy", new Dictionary<string, object>());
        var agent = new Agent(agentId, "TestAgent", agentType, strategy, true);

        var newStrategyName = "NewStrategy";
        var newStrategyParams = new Dictionary<string, object> { { "temperature", 0.7 } };

        _mockRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var command = new ConfigureAgentCommand(agentId, newStrategyName, newStrategyParams);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(agentId, result.AgentId);
        Assert.Contains("configured successfully", result.Message);

        _mockRepository.Verify(r => r.UpdateAsync(agent, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentAgent_ReturnsFailureResult()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(agentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var command = new ConfigureAgentCommand(agentId, "Strategy", new Dictionary<string, object>());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("AGENT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
