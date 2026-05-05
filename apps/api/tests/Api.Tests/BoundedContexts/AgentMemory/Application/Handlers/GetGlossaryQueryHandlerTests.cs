using Api.BoundedContexts.AgentMemory.Application.Queries;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Application.Handlers;

/// <summary>
/// Tests for GetGlossaryQueryHandler covering all memory states.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
public class GetGlossaryQueryHandlerTests
{
    private readonly Mock<IGameMemoryRepository> _gameMemoryRepoMock = new();
    private readonly GetGlossaryQueryHandler _handler;

    public GetGlossaryQueryHandlerTests()
    {
        _handler = new GetGlossaryQueryHandler(_gameMemoryRepoMock.Object);
    }

    [Fact]
    public async Task Handle_MemoryExistsWithEntries_ReturnsMappedDtoList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var memory = GameMemory.Create(gameId, ownerId);
        memory.AddGlossaryEntry("Meeple", "A playing piece shaped like a person", "en", GlossaryEntrySource.Manual);
        memory.AddGlossaryEntry("Worker", "A resource-gathering game piece", "en", GlossaryEntrySource.UserDefined);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        var query = new GetGlossaryQuery(gameId, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Term.Should().Be("Meeple");
        result[0].Definition.Should().Be("A playing piece shaped like a person");
        result[0].Language.Should().Be("en");
        result[0].Source.Should().Be(GlossaryEntrySource.Manual.ToString());
        result[1].Term.Should().Be("Worker");
        result[1].Source.Should().Be(GlossaryEntrySource.UserDefined.ToString());
    }

    [Fact]
    public async Task Handle_MemoryNotFound_ReturnsEmptyList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameMemory?)null);

        var query = new GetGlossaryQuery(gameId, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MemoryExistsWithEmptyGlossary_ReturnsEmptyList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        // Memory exists but no glossary entries
        var memory = GameMemory.Create(gameId, ownerId);
        memory.AddHouseRule("No phones at table", HouseRuleSource.UserAdded);

        _gameMemoryRepoMock
            .Setup(r => r.GetByGameAndOwnerAsync(gameId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memory);

        var query = new GetGlossaryQuery(gameId, ownerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
