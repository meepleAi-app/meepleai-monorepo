using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for ApproveAgentTypologyCommandHandler.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ApproveAgentTypologyCommandHandlerTests
{
    private readonly Mock<IAgentTypologyRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly Mock<ILogger<ApproveAgentTypologyCommandHandler>> _mockLogger;
    private readonly ApproveAgentTypologyCommandHandler _handler;

    public ApproveAgentTypologyCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentTypologyRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPublisher = new Mock<IPublisher>();
        _mockLogger = new Mock<ILogger<ApproveAgentTypologyCommandHandler>>();
        _handler = new ApproveAgentTypologyCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockPublisher.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithPendingTypology_ApprovesSuccessfully()
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

        typology.SubmitForApproval(); // Set to Pending

        _mockRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        var command = new ApproveAgentTypologyCommand(typologyId, approvedBy);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(TypologyStatus.Approved.ToString());
        result.ApprovedBy.Should().Be(approvedBy);
        result.ApprovedAt.Should().NotBeNull();

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
        var approvedBy = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentTypology?)null);

        var command = new ApproveAgentTypologyCommand(typologyId, approvedBy);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        exception.ResourceType.Should().Be("AgentTypology");
        exception.ResourceId.Should().Be(typologyId.ToString());
    }

    [Fact]
    public async Task Handle_WithEmptyApprovedBy_ThrowsArgumentException()
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

        var command = new ApproveAgentTypologyCommand(typologyId, Guid.Empty);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_WithAlreadyApprovedTypology_RemainsApproved()
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
        typology.Approve(approvedBy); // Already approved

        _mockRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        var command = new ApproveAgentTypologyCommand(typologyId, approvedBy);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Status.Should().Be(TypologyStatus.Approved.ToString());
        result.ApprovedBy.Should().Be(approvedBy);
    }
}
