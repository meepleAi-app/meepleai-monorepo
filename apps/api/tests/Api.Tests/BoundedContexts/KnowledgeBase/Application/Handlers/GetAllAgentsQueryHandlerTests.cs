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
/// Tests for GetAllAgentsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAllAgentsQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _mockRepository;
    private readonly Mock<ILogger<GetAllAgentsQueryHandler>> _mockLogger;
    private readonly GetAllAgentsQueryHandler _handler;

    public GetAllAgentsQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentRepository>();
        _mockLogger = new Mock<ILogger<GetAllAgentsQueryHandler>>();
        _handler = new GetAllAgentsQueryHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithNoFilter_ReturnsAllAgents()
    {
        // Arrange
        var agents = new List<Agent>
        {
            CreateAgent("Agent1", "RAG", true),
            CreateAgent("Agent2", "Citation", false)
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetAllAgentsQuery(Type: null, ActiveOnly: null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithActiveOnlyFilter_ReturnsActiveAgents()
    {
        // Arrange
        var agents = new List<Agent>
        {
            CreateAgent("Agent1", "RAG", true)
        };

        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetAllAgentsQuery(Type: null, ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithTypeFilter_ReturnsFilteredAgents()
    {
        // Arrange
        var agents = new List<Agent>
        {
            CreateAgent("RagAgent", "RAG", true)
        };

        _mockRepository
            .Setup(r => r.GetByTypeAsync(It.IsAny<AgentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetAllAgentsQuery(Type: "RAG", ActiveOnly: null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result[0].Type.Should().Be("RAG");
    }

    private static Agent CreateAgent(string name, string type, bool isActive)
    {
        var agentType = AgentType.Parse(type);
        var strategy = AgentStrategy.Custom("DefaultStrategy", new Dictionary<string, object>());
        return new Agent(Guid.NewGuid(), name, agentType, strategy, isActive);
    }
}
