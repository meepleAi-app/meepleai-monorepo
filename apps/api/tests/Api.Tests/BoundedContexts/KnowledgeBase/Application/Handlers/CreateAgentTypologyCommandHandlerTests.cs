using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
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
/// Tests for CreateAgentTypologyCommandHandler.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CreateAgentTypologyCommandHandlerTests
{
    private readonly Mock<IAgentTypologyRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<CreateAgentTypologyCommandHandler>> _mockLogger;
    private readonly CreateAgentTypologyCommandHandler _handler;

    public CreateAgentTypologyCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentTypologyRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<CreateAgentTypologyCommandHandler>>();
        _handler = new CreateAgentTypologyCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesTypology()
    {
        // Arrange
        var command = new CreateAgentTypologyCommand(
            Name: "Strategy Coach",
            Description: "Suggests optimal moves",
            BasePrompt: "You are a Strategy Coach agent...",
            DefaultStrategyName: "MultiModelConsensus",
            DefaultStrategyParameters: new Dictionary<string, object>
            {
                { "models", new[] { "claude", "gpt4" } },
                { "threshold", 0.7 }
            },
            CreatedBy: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Strategy Coach");
        result.Description.Should().Be("Suggests optimal moves");
        result.Status.Should().Be(TypologyStatus.Draft.ToString());
        result.CreatedBy.Should().Be(command.CreatedBy);
        result.IsDeleted.Should().BeFalse();

        _mockRepository.Verify(r =>
            r.AddAsync(It.IsAny<AgentTypology>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u =>
            u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var command = new CreateAgentTypologyCommand(
            Name: "",
            Description: "Test",
            BasePrompt: "Test prompt",
            DefaultStrategyName: "Test",
            DefaultStrategyParameters: new Dictionary<string, object>(),
            CreatedBy: Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        _mockRepository.Verify(r =>
            r.AddAsync(It.IsAny<AgentTypology>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Arrange
        var command = new CreateAgentTypologyCommand(
            Name: "Test",
            Description: "Test",
            BasePrompt: "Test prompt",
            DefaultStrategyName: "Test",
            DefaultStrategyParameters: new Dictionary<string, object>(),
            CreatedBy: Guid.Empty);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsCorrectDto()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var command = new CreateAgentTypologyCommand(
            Name: "Rules Expert",
            Description: "Explains game rules",
            BasePrompt: "You are a Rules Expert...",
            DefaultStrategyName: "SingleModel",
            DefaultStrategyParameters: new Dictionary<string, object> { { "model", "claude" } },
            CreatedBy: createdBy);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("Rules Expert");
        result.DefaultStrategyName.Should().Be("SingleModel");
        result.DefaultStrategyParameters.Should().ContainKey("model");
        result.ApprovedBy.Should().BeNull();
        result.ApprovedAt.Should().BeNull();
    }
}
