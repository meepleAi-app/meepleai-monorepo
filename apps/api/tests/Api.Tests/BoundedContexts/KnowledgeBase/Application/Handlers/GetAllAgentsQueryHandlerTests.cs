using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetAllAgentsQueryHandler — used by GET /api/v1/games/{id}/agents.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAllAgentsQueryHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly GetAllAgentsQueryHandler _handler;

    public GetAllAgentsQueryHandlerTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _handler = new GetAllAgentsQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_ActiveOnly_ShouldCallGetAllActiveAsync()
    {
        // Arrange
        var agents = new List<AgentDefinitionEntity>
        {
            CreateAgent("QA Agent"),
            CreateAgent("Rules Agent")
        };

        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        var query = new GetAllAgentsQuery(ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(a => a.Should().BeOfType<AgentDto>());
        _mockRepository.Verify(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoFilter_ShouldCallGetAllAsync()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity>());

        var query = new GetAllAgentsQuery();

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MapsFieldsCorrectly()
    {
        // Arrange
        var agent = CreateAgent("Test Agent");
        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity> { agent });

        var query = new GetAllAgentsQuery(ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        var dto = result[0];
        dto.Name.Should().Be("Test Agent");
        dto.Type.Should().NotBeNullOrEmpty();
        dto.StrategyName.Should().NotBeNullOrEmpty();
        dto.IsActive.Should().BeFalse(); // AgentDefinition.Create sets IsActive=false
    }

    [Fact]
    public async Task Handle_EmptyResult_ShouldReturnEmptyList()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentDefinitionEntity>());

        var query = new GetAllAgentsQuery(ActiveOnly: true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    private static AgentDefinitionEntity CreateAgent(string name)
    {
        return AgentDefinitionEntity.Create(
            name,
            $"Description for {name}",
            AgentType.RagAgent,
            AgentDefinitionConfig.Default());
    }
}
