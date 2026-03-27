using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AgentDefinition;

/// <summary>
/// Unit tests for CreateAgentDefinitionCommandHandler — KbCardIds support (Issue #5140).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CreateAgentDefinitionCommandHandlerKbCardsTests
{
    private static readonly Guid _gameId = Guid.NewGuid();
    private static readonly Guid _kbCard1 = Guid.NewGuid();
    private static readonly Guid _kbCard2 = Guid.NewGuid();

    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly CreateAgentDefinitionCommandHandler _handler;

    public CreateAgentDefinitionCommandHandlerKbCardsTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _mockRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _handler = new CreateAgentDefinitionCommandHandler(
            _mockRepository.Object,
            new Mock<ILogger<CreateAgentDefinitionCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_NullKbCardIds_CreatesAgentWithEmptyKbCards()
    {
        // Arrange
        var command = new CreateAgentDefinitionCommand(
            Name: "TestAgent",
            Description: "Desc",
            Type: "RAG",
            Model: "gpt-4",
            MaxTokens: 2048,
            Temperature: 0.7f);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.KbCardIds.Should().BeEmpty("no KbCardIds were supplied");
    }

    [Fact]
    public async Task Handle_WithKbCardIds_AppliesKbCardIdsToAgentDefinition()
    {
        // Arrange
        var kbCardIds = new List<Guid> { _kbCard1, _kbCard2 };
        var command = new CreateAgentDefinitionCommand(
            Name: "TestAgent",
            Description: "Desc",
            Type: "RAG",
            Model: "gpt-4",
            MaxTokens: 2048,
            Temperature: 0.7f,
            KbCardIds: kbCardIds,
            GameId: _gameId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.KbCardIds.Should().BeEquivalentTo(kbCardIds);
    }

    [Fact]
    public async Task Handle_EmptyKbCardIds_DoesNotApplyKbCardIds()
    {
        // Arrange
        var command = new CreateAgentDefinitionCommand(
            Name: "TestAgent",
            Description: "Desc",
            Type: "RAG",
            Model: "gpt-4",
            MaxTokens: 2048,
            Temperature: 0.7f,
            KbCardIds: new List<Guid>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.KbCardIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithKbCardIds_PersistsToRepository()
    {
        // Arrange
        var kbCardIds = new List<Guid> { _kbCard1 };
        var command = new CreateAgentDefinitionCommand(
            Name: "TestAgent",
            Description: "Desc",
            Type: "RAG",
            Model: "gpt-4",
            MaxTokens: 2048,
            Temperature: 0.7f,
            KbCardIds: kbCardIds,
            GameId: _gameId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(
            It.Is<AgentDefinitionEntity>(a => a.KbCardIds.SequenceEqual(kbCardIds)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
