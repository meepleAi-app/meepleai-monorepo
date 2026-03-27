using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for ProposeAgentTypologyCommandHandler.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ProposeAgentTypologyCommandHandlerTests
{
    private readonly Mock<IAgentTypologyRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<ProposeAgentTypologyCommandHandler>> _mockLogger;
    private readonly ProposeAgentTypologyCommandHandler _handler;

    public ProposeAgentTypologyCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentTypologyRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<ProposeAgentTypologyCommandHandler>>();
        _handler = new ProposeAgentTypologyCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesTypologyAsDraft()
    {
        // Arrange
        var proposedBy = Guid.NewGuid();
        var command = new ProposeAgentTypologyCommand(
            Name: "Strategy Coach",
            Description: "Suggests optimal moves",
            BasePrompt: "You are a Strategy Coach agent...",
            DefaultStrategyName: "MultiModelConsensus",
            DefaultStrategyParameters: new Dictionary<string, object>
            {
                { "models", new[] { "claude", "gpt4" } },
                { "threshold", 0.7 }
            },
            ProposedBy: proposedBy);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Strategy Coach");
        result.Description.Should().Be("Suggests optimal moves");
        result.Status.Should().Be(TypologyStatus.Draft.ToString());
        result.CreatedBy.Should().Be(proposedBy);
        result.ApprovedBy.Should().BeNull();
        result.ApprovedAt.Should().BeNull();
        result.IsDeleted.Should().BeFalse();

        _mockRepository.Verify(r =>
            r.AddAsync(It.Is<AgentTypology>(t => t.Status == TypologyStatus.Draft), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u =>
            u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var command = new ProposeAgentTypologyCommand(
            Name: "",
            Description: "Test",
            BasePrompt: "Test prompt",
            DefaultStrategyName: "Test",
            DefaultStrategyParameters: new Dictionary<string, object>(),
            ProposedBy: Guid.NewGuid());

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();

        _mockRepository.Verify(r =>
            r.AddAsync(It.IsAny<AgentTypology>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyProposedBy_ThrowsArgumentException()
    {
        // Arrange
        var command = new ProposeAgentTypologyCommand(
            Name: "Test",
            Description: "Test",
            BasePrompt: "Test prompt",
            DefaultStrategyName: "Test",
            DefaultStrategyParameters: new Dictionary<string, object>(),
            ProposedBy: Guid.Empty);

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsCorrectDto()
    {
        // Arrange
        var proposedBy = Guid.NewGuid();
        var command = new ProposeAgentTypologyCommand(
            Name: "Rules Expert",
            Description: "Explains game rules in detail",
            BasePrompt: "You are a Rules Expert specialized in board game rules...",
            DefaultStrategyName: "SingleModel",
            DefaultStrategyParameters: new Dictionary<string, object> { { "model", "claude" } },
            ProposedBy: proposedBy);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("Rules Expert");
        result.DefaultStrategyName.Should().Be("SingleModel");
        result.DefaultStrategyParameters.Should().ContainKey("model");
        result.CreatedBy.Should().Be(proposedBy);
        result.ApprovedBy.Should().BeNull();
        result.ApprovedAt.Should().BeNull();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_WithEmptyDescription_ThrowsArgumentException()
    {
        // Arrange
        var command = new ProposeAgentTypologyCommand(
            Name: "Test",
            Description: "",
            BasePrompt: "Test prompt",
            DefaultStrategyName: "Test",
            DefaultStrategyParameters: new Dictionary<string, object>(),
            ProposedBy: Guid.NewGuid());

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_WithEmptyBasePrompt_ThrowsArgumentException()
    {
        // Arrange
        var command = new ProposeAgentTypologyCommand(
            Name: "Test",
            Description: "Test",
            BasePrompt: "",
            DefaultStrategyName: "Test",
            DefaultStrategyParameters: new Dictionary<string, object>(),
            ProposedBy: Guid.NewGuid());

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_CreatesTypologyWithDraftStatus()
    {
        // Arrange
        AgentTypology? capturedTypology = null;
        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<AgentTypology>(), It.IsAny<CancellationToken>()))
            .Callback<AgentTypology, CancellationToken>((t, _) => capturedTypology = t)
            .Returns(Task.CompletedTask);

        var command = new ProposeAgentTypologyCommand(
            Name: "Test",
            Description: "Test Description",
            BasePrompt: "Test Prompt",
            DefaultStrategyName: "Test Strategy",
            DefaultStrategyParameters: new Dictionary<string, object> { { "param", "value" } },
            ProposedBy: Guid.NewGuid());

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        capturedTypology.Should().NotBeNull();
        capturedTypology!.Status.Should().Be(TypologyStatus.Draft);
        capturedTypology.ApprovedBy.Should().BeNull();
        capturedTypology.ApprovedAt.Should().BeNull();
    }
}
