using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
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
        result.Should().NotBeNull();
        result.Id.Should().Be(agentId);
        result.Name.Should().Be("TestAgent");
        result.Type.Should().Be("RAG");
        result.IsActive.Should().BeTrue();
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
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
