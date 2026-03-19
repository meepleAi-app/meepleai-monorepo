using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for DeleteAgentTypologyCommandHandler.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class DeleteAgentTypologyCommandHandlerTests
{
    private readonly Mock<IAgentTypologyRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<DeleteAgentTypologyCommandHandler>> _mockLogger;
    private readonly DeleteAgentTypologyCommandHandler _handler;

    public DeleteAgentTypologyCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentTypologyRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<DeleteAgentTypologyCommandHandler>>();
        _handler = new DeleteAgentTypologyCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SoftDeletesTypology()
    {
        // Arrange
        var typologyId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var strategy = AgentStrategy.Custom("Test", new Dictionary<string, object>());
        var typology = new AgentTypology(
            id: typologyId,
            name: "Test",
            description: "Test",
            basePrompt: "Test",
            defaultStrategy: strategy,
            createdBy: createdBy);

        _mockRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        var command = new DeleteAgentTypologyCommand(typologyId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        typology.IsDeleted.Should().BeTrue();

        _mockRepository.Verify(r =>
            r.UpdateAsync(typology, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u =>
            u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentTypology_ThrowsNotFoundException()
    {
        // Arrange
        var typologyId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentTypology?)null);

        var command = new DeleteAgentTypologyCommand(typologyId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        exception.ResourceType.Should().Be("AgentTypology");
        exception.ResourceId.Should().Be(typologyId.ToString());
    }

    [Fact]
    public async Task Handle_WithAlreadyDeletedTypology_RemainsDeleted()
    {
        // Arrange
        var typologyId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var strategy = AgentStrategy.Custom("Test", new Dictionary<string, object>());
        var typology = new AgentTypology(
            id: typologyId,
            name: "Test",
            description: "Test",
            basePrompt: "Test",
            defaultStrategy: strategy,
            createdBy: createdBy);

        typology.Delete(); // Already deleted

        _mockRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        var command = new DeleteAgentTypologyCommand(typologyId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        typology.IsDeleted.Should().BeTrue();
        _mockRepository.Verify(r =>
            r.UpdateAsync(typology, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
