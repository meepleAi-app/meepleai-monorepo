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
/// Tests for UpdateAgentTypologyCommandHandler.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class UpdateAgentTypologyCommandHandlerTests
{
    private readonly Mock<IAgentTypologyRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<UpdateAgentTypologyCommandHandler>> _mockLogger;
    private readonly UpdateAgentTypologyCommandHandler _handler;

    public UpdateAgentTypologyCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentTypologyRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<UpdateAgentTypologyCommandHandler>>();
        _handler = new UpdateAgentTypologyCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesTypology()
    {
        // Arrange
        var typologyId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var strategy = AgentStrategy.Custom("Test", new Dictionary<string, object>());
        var typology = new AgentTypology(
            id: typologyId,
            name: "Original Name",
            description: "Original Description",
            basePrompt: "Original Prompt",
            defaultStrategy: strategy,
            createdBy: createdBy);

        _mockRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        var command = new UpdateAgentTypologyCommand(
            Id: typologyId,
            Name: "Updated Name",
            Description: "Updated Description",
            BasePrompt: "Updated Prompt",
            DefaultStrategyName: "NewStrategy",
            DefaultStrategyParameters: new Dictionary<string, object> { { "key", "value" } });

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
        result.BasePrompt.Should().Be("Updated Prompt");
        result.DefaultStrategyName.Should().Be("NewStrategy");

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

        var command = new UpdateAgentTypologyCommand(
            Id: typologyId,
            Name: "Test",
            Description: "Test",
            BasePrompt: "Test",
            DefaultStrategyName: "Test",
            DefaultStrategyParameters: new Dictionary<string, object>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        exception.ResourceType.Should().Be("AgentTypology");
        exception.ResourceId.Should().Be(typologyId.ToString());
    }

    [Fact]
    public async Task Handle_WithApprovedTypology_ThrowsInvalidOperationException()
    {
        // Arrange
        var typologyId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var approvedBy = Guid.NewGuid();
        var strategy = AgentStrategy.Custom("Test", new Dictionary<string, object>());
        var typology = new AgentTypology(
            id: typologyId,
            name: "Test",
            description: "Test",
            basePrompt: "Test",
            defaultStrategy: strategy,
            createdBy: createdBy);

        typology.SubmitForApproval();
        typology.Approve(approvedBy);

        _mockRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        var command = new UpdateAgentTypologyCommand(
            Id: typologyId,
            Name: "Updated",
            Description: "Updated",
            BasePrompt: "Updated",
            DefaultStrategyName: "Updated",
            DefaultStrategyParameters: new Dictionary<string, object>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }
}
