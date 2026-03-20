using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

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
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentId.Should().Be(agentId);
        result.Message.Should().Contain("configured successfully");

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
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("AGENT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
