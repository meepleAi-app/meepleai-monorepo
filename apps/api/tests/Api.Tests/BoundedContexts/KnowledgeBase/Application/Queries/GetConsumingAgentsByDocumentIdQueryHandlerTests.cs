using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1651")]
public sealed class GetConsumingAgentsByDocumentIdQueryHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _agentRepo = new(MockBehavior.Strict);
    private readonly Mock<ISharedGameRepository> _gameRepo = new(MockBehavior.Strict);

    private GetConsumingAgentsByDocumentIdQueryHandler CreateHandler() =>
        new(_agentRepo.Object, _gameRepo.Object);

    private static AgentDefinition MakeCustomAgent(string name, Guid? gameId = null)
    {
        var agent = AgentDefinition.Create(
            name: name,
            description: "test",
            type: AgentType.Custom("HybridSearch", "Hybrid search"),
            config: AgentDefinitionConfig.Create("gpt-4o-mini", 1024, 0.5f));
        if (gameId.HasValue) agent.SetGameId(gameId);
        return agent;
    }

    private static AgentDefinition MakeSystemAgent(string name, string typologySlug)
    {
        return AgentDefinition.CreateSystem(
            name: name,
            description: "system",
            type: AgentType.Custom("HybridSearch", "Hybrid search"),
            config: AgentDefinitionConfig.Create("gpt-4o-mini", 1024, 0.5f),
            typologySlug: typologySlug);
    }

    [Fact]
    public async Task Handle_NoConsumingAgents_ReturnsEmptyList()
    {
        // Arrange
        var docId = Guid.NewGuid();
        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentDefinition>());

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().BeEmpty();
        _gameRepo.Verify(g => g.GetNamesByIdsAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never, "no agents → no game-name resolution needed");
    }

    [Fact]
    public async Task Handle_MapsCustomAgent_WithGameNameResolved()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agent = MakeCustomAgent("Alpha", gameId);

        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { agent });
        _gameRepo.Setup(g => g.GetNamesByIdsAsync(
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(gameId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [gameId] = "Wingspan" });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.Id.Should().Be(agent.Id);
        dto.Name.Should().Be("Alpha");
        dto.IsSystemDefined.Should().BeFalse();
        dto.TypologySlug.Should().BeNull();
        dto.GameId.Should().Be(gameId);
        dto.GameName.Should().Be("Wingspan");
        dto.Status.Should().Be(AgentDefinitionStatus.Draft.ToString());
    }

    [Fact]
    public async Task Handle_MapsSystemAgent_WithFlagAndTypologySlug()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var agent = MakeSystemAgent("Arbitro", typologySlug: "arbitro");

        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { agent });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().HaveCount(1);
        var dto = result[0];
        dto.IsSystemDefined.Should().BeTrue();
        dto.TypologySlug.Should().Be("arbitro");
        dto.GameId.Should().BeNull();
        dto.GameName.Should().BeNull();
        _gameRepo.Verify(g => g.GetNamesByIdsAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never, "no agent has a GameId → no resolution call");
    }

    [Fact]
    public async Task Handle_AgentWithMissingGame_LeavesGameNameNull()
    {
        // Arrange — the agent has a GameId, but the game has been removed from the catalog.
        var docId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var agent = MakeCustomAgent("Beta", gameId);

        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { agent });
        _gameRepo.Setup(g => g.GetNamesByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>()); // empty — game not found

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().HaveCount(1);
        result[0].GameId.Should().Be(gameId);
        result[0].GameName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_BulkResolvesGameNames_OneCallForMultipleAgents()
    {
        // Arrange — two agents, each with a distinct game. Must call GetNamesByIdsAsync ONCE.
        var docId = Guid.NewGuid();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var a1 = MakeCustomAgent("A1", g1);
        var a2 = MakeCustomAgent("A2", g2);

        _agentRepo.Setup(r => r.GetByConsumedDocumentAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { a1, a2 });
        _gameRepo.Setup(g => g.GetNamesByIdsAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [g1] = "Game1", [g2] = "Game2" });

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(new GetConsumingAgentsByDocumentIdQuery(docId), default);

        // Assert
        result.Should().HaveCount(2);
        _gameRepo.Verify(g => g.GetNamesByIdsAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once, "bulk lookup, no N+1");
    }
}
