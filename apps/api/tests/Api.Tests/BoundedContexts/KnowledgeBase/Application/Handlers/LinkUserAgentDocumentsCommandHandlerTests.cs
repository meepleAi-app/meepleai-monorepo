using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for LinkUserAgentDocumentsCommandHandler.
/// Issue #4941: Auto-link indexed PDF documents when creating game agent.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class LinkUserAgentDocumentsCommandHandlerTests
{
    private readonly Mock<IVectorDocumentRepository> _vectorDocRepoMock;
    private readonly Mock<IAgentDefinitionRepository> _agentDefRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<LinkUserAgentDocumentsCommandHandler>> _loggerMock;
    private readonly LinkUserAgentDocumentsCommandHandler _handler;

    public LinkUserAgentDocumentsCommandHandlerTests()
    {
        _vectorDocRepoMock = new Mock<IVectorDocumentRepository>();
        _agentDefRepoMock = new Mock<IAgentDefinitionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<LinkUserAgentDocumentsCommandHandler>>();

        _handler = new LinkUserAgentDocumentsCommandHandler(
            _vectorDocRepoMock.Object,
            _agentDefRepoMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenDocumentsExistAndAgentFound_LinksDocumentsToAgent()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentDefinitionId = Guid.NewGuid();
        var docId1 = Guid.NewGuid();
        var docId2 = Guid.NewGuid();

        var document1 = new VectorDocument(docId1, gameId, Guid.NewGuid(), "it", 50);
        var document2 = new VectorDocument(docId2, gameId, Guid.NewGuid(), "it", 30);

        var agentDefinition = AgentDefinitionEntity.Create(
            "Test Agent",
            "Test description",
            AgentType.Parse("RAG"),
            AgentDefinitionConfig.Default());

        _vectorDocRepoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([document1, document2]);

        _agentDefRepoMock
            .Setup(r => r.GetByIdAsync(agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDefinition);

        AgentDefinitionEntity? capturedAgent = null;
        _agentDefRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<AgentDefinitionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AgentDefinitionEntity, CancellationToken>((a, _) => capturedAgent = a)
            .Returns(Task.CompletedTask);

        var command = new LinkUserAgentDocumentsCommand(gameId, agentDefinitionId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);

        _agentDefRepoMock.Verify(r => r.UpdateAsync(It.IsAny<AgentDefinitionEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        capturedAgent.Should().NotBeNull();
        capturedAgent!.KbCardIds.Should().BeEquivalentTo(new[] { docId1, docId2 });
    }

    [Fact]
    public async Task Handle_WhenNoDocumentsExist_ReturnsUnitWithoutUpdating()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentDefinitionId = Guid.NewGuid();

        _vectorDocRepoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var command = new LinkUserAgentDocumentsCommand(gameId, agentDefinitionId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);

        _agentDefRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _agentDefRepoMock.Verify(r => r.UpdateAsync(It.IsAny<AgentDefinitionEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAgentDefinitionNotFound_ReturnsUnitWithoutUpdating()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentDefinitionId = Guid.NewGuid();
        var document = new VectorDocument(Guid.NewGuid(), gameId, Guid.NewGuid(), "en", 25);

        _vectorDocRepoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        _agentDefRepoMock
            .Setup(r => r.GetByIdAsync(agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentDefinitionEntity?)null);

        var command = new LinkUserAgentDocumentsCommand(gameId, agentDefinitionId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);

        _agentDefRepoMock.Verify(r => r.UpdateAsync(It.IsAny<AgentDefinitionEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSingleDocument_LinksCorrectId()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var agentDefinitionId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        var document = new VectorDocument(docId, gameId, Guid.NewGuid(), "en", 100);

        var agentDefinition = AgentDefinitionEntity.Create(
            "Single Doc Agent",
            "Description",
            AgentType.Parse("RAG"),
            AgentDefinitionConfig.Default());

        _vectorDocRepoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([document]);

        _agentDefRepoMock
            .Setup(r => r.GetByIdAsync(agentDefinitionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentDefinition);

        _agentDefRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<AgentDefinitionEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new LinkUserAgentDocumentsCommand(gameId, agentDefinitionId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert: KbCardIds contains exactly the single document ID
        agentDefinition.KbCardIds.Should().ContainSingle()
            .Which.Should().Be(docId);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _handler.Handle(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
