using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for TestAgentTypologyCommandHandler.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class TestAgentTypologyCommandHandlerTests
{
    private readonly Mock<IAgentTypologyRepository> _mockTypologyRepository;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<ILogger<TestAgentTypologyCommandHandler>> _mockLogger;
    private readonly TestAgentTypologyCommandHandler _handler;

    public TestAgentTypologyCommandHandlerTests()
    {
        _mockTypologyRepository = new Mock<IAgentTypologyRepository>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockLogger = new Mock<ILogger<TestAgentTypologyCommandHandler>>();
        _handler = new TestAgentTypologyCommandHandler(
            _mockTypologyRepository.Object,
            _mockEmbeddingService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidDraftTypology_ReturnsSuccessResult()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var typologyId = Guid.NewGuid();
        var typology = CreateDraftTypology(typologyId, ownerId);

        _mockTypologyRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        _mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { CreateMockEmbedding() }));

        var command = new TestAgentTypologyCommand(
            TypologyId: typologyId,
            TestQuery: "What are the rules for movement?",
            RequestedBy: ownerId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Response.Should().Contain("Test Successful");
        result.ConfidenceScore.Should().BeGreaterThan(0);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNonExistentTypology_ReturnsFailureWithNotFoundError()
    {
        // Arrange
        var typologyId = Guid.NewGuid();
        _mockTypologyRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentTypology?)null);

        var command = new TestAgentTypologyCommand(
            TypologyId: typologyId,
            TestQuery: "Test query",
            RequestedBy: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WithNonDraftStatus_ReturnsFailureWithInvalidStateError()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var typologyId = Guid.NewGuid();
        var typology = CreateApprovedTypology(typologyId, ownerId);

        _mockTypologyRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        var command = new TestAgentTypologyCommand(
            TypologyId: typologyId,
            TestQuery: "Test query",
            RequestedBy: ownerId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Only Draft typologies can be tested");
    }

    [Fact]
    public async Task Handle_WithDifferentOwner_ReturnsFailureWithUnauthorizedError()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var typologyId = Guid.NewGuid();
        var typology = CreateDraftTypology(typologyId, ownerId);

        _mockTypologyRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        var command = new TestAgentTypologyCommand(
            TypologyId: typologyId,
            TestQuery: "Test query",
            RequestedBy: otherUserId); // Different user trying to test

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("only test typologies you created");
    }

    [Fact]
    public async Task Handle_ValidatesEmbeddingServiceIntegration()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var typologyId = Guid.NewGuid();
        var typology = CreateDraftTypology(typologyId, ownerId);

        _mockTypologyRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        _mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { CreateMockEmbedding() }));

        var testQuery = "What are the setup rules?";
        var command = new TestAgentTypologyCommand(
            TypologyId: typologyId,
            TestQuery: testQuery,
            RequestedBy: ownerId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockEmbeddingService.Verify(
            s => s.GenerateEmbeddingAsync(testQuery, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmbeddingServiceFailure_ReturnsFailureResult()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var typologyId = Guid.NewGuid();
        var typology = CreateDraftTypology(typologyId, ownerId);

        _mockTypologyRepository
            .Setup(r => r.GetByIdAsync(typologyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typology);

        _mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding service error"));

        var command = new TestAgentTypologyCommand(
            TypologyId: typologyId,
            TestQuery: "Test query",
            RequestedBy: ownerId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    // Helper methods
    private static AgentTypology CreateDraftTypology(Guid id, Guid createdBy)
    {
        var strategy = AgentStrategy.Custom("TestStrategy", new Dictionary<string, object>
        {
            { "TopK", 10 },
            { "MinScore", 0.55 }
        });

        return new AgentTypology(
            id: id,
            name: "Test Typology",
            description: "Test Description",
            basePrompt: "Test Base Prompt",
            defaultStrategy: strategy,
            createdBy: createdBy,
            status: TypologyStatus.Draft);
    }

    private static AgentTypology CreateApprovedTypology(Guid id, Guid createdBy)
    {
        var typology = CreateDraftTypology(id, createdBy);
        typology.Approve(Guid.NewGuid());
        return typology;
    }

    private static float[] CreateMockEmbedding()
    {
        // Create a simple 384-dimensional mock embedding (typical for sentence-transformers)
        return Enumerable.Range(0, 384).Select(i => (float)Math.Sin(i * 0.1)).ToArray();
    }
}
