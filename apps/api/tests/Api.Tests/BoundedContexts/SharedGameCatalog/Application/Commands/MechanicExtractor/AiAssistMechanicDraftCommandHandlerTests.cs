using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Unit tests for AiAssistMechanicDraftCommandHandler.
/// CRITICAL: Validates the Variant C copyright firewall — AI receives ONLY human notes.
/// The handler has ZERO dependencies on IBlobStorageService, PDF repositories, or any file storage.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AiAssistMechanicDraftCommandHandlerTests
{
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly AiAssistMechanicDraftCommandHandler _handler;

    public AiAssistMechanicDraftCommandHandlerTests()
    {
        _llmServiceMock = new Mock<ILlmService>();
        var loggerMock = new Mock<ILogger<AiAssistMechanicDraftCommandHandler>>();

        _handler = new AiAssistMechanicDraftCommandHandler(
            _llmServiceMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithSuccessfulLlmResponse_ReturnsAiAssistResult()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "Players trade resources to build settlements", "Catan");

        _llmServiceMock.Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), RequestSource.AdminOperation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "Catan is a resource trading game where players..."
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Section.Should().Be("summary");
        result.GeneratedDraft.Should().Be("Catan is a resource trading game where players...");
    }

    [Fact]
    public async Task Handle_PassesHumanNotesAsUserPrompt()
    {
        // Arrange
        var humanNotes = "Workers can be placed on action spaces. Each round...";
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "mechanics", humanNotes, "Agricola");

        _llmServiceMock.Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.Is<string>(p => p.Contains(humanNotes)),
                RequestSource.AdminOperation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "[\"Worker Placement\",\"Action Selection\"]"
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _llmServiceMock.Verify(s => s.GenerateCompletionAsync(
            It.IsAny<string>(),
            It.Is<string>(p => p.Contains(humanNotes)),
            RequestSource.AdminOperation,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UsesAdminOperationRequestSource()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "Some notes about the game", "Test Game");

        _llmServiceMock.Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), RequestSource.AdminOperation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult { Success = true, Response = "Draft text" });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _llmServiceMock.Verify(s => s.GenerateCompletionAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            RequestSource.AdminOperation,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLlmFails_ThrowsConflictException()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "Some notes about the game", "Test Game");

        _llmServiceMock.Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = false,
                ErrorMessage = "Rate limit exceeded"
            });

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_WhenLlmReturnsEmptyResponse_ThrowsConflictException()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "Some notes about the game", "Test Game");

        _llmServiceMock.Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = ""
            });

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_TrimsLlmResponse()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "Notes about the game", "Test Game");

        _llmServiceMock.Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "  Draft text with whitespace  \n"
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.GeneratedDraft.Should().Be("Draft text with whitespace");
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("mechanics")]
    [InlineData("victory")]
    [InlineData("resources")]
    [InlineData("phases")]
    [InlineData("questions")]
    public async Task Handle_IncludesGameTitleInSystemPrompt(string section)
    {
        // Arrange
        var gameTitle = "Terraforming Mars";
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), section, "Notes about section", gameTitle);

        _llmServiceMock.Setup(s => s.GenerateCompletionAsync(
                It.Is<string>(p => p.Contains(gameTitle)),
                It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult { Success = true, Response = "Draft" });

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _llmServiceMock.Verify(s => s.GenerateCompletionAsync(
            It.Is<string>(p => p.Contains(gameTitle)),
            It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
