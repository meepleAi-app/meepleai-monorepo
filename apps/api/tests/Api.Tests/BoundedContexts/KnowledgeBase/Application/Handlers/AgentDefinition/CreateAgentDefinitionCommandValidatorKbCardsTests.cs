using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Validators.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AgentDefinition;

/// <summary>
/// Unit tests for CreateAgentDefinitionCommandValidator — KbCardIds rules (Issue #5140).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CreateAgentDefinitionCommandValidatorKbCardsTests
{
    private static readonly Guid _gameId = Guid.NewGuid();
    private static readonly Guid _kbCardId = Guid.NewGuid();

    private readonly Mock<IVectorDocumentRepository> _vectorDocRepoMock;
    private readonly CreateAgentDefinitionCommandValidator _validator;

    public CreateAgentDefinitionCommandValidatorKbCardsTests()
    {
        _vectorDocRepoMock = new Mock<IVectorDocumentRepository>();
        _validator = new CreateAgentDefinitionCommandValidator(_vectorDocRepoMock.Object);
    }

    private static CreateAgentDefinitionCommand ValidCommand(
        List<Guid>? kbCardIds = null,
        Guid? gameId = null) =>
        new CreateAgentDefinitionCommand(
            Name: "TestAgent",
            Description: "Test",
            Type: "RAG",
            Model: "gpt-4",
            MaxTokens: 2048,
            Temperature: 0.7f,
            KbCardIds: kbCardIds,
            GameId: gameId);

    [Fact]
    public async Task Validate_NullKbCardIds_NoKbCardsValidationRun()
    {
        // Arrange -- no GameId, no KbCardIds; rule should not trigger
        var command = ValidCommand(kbCardIds: null, gameId: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert -- KbCardIds rule not triggered
        result.ShouldNotHaveValidationErrorFor(x => x.KbCardIds);
        _vectorDocRepoMock.Verify(r => r.AnyBelongsToGameAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Validate_EmptyKbCardIds_NoKbCardsValidationRun()
    {
        // Arrange -- empty list + GameId; rule condition kbCardIds.Count > 0 -> false -> rule not triggered
        var command = ValidCommand(kbCardIds: new List<Guid>(), gameId: _gameId);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.KbCardIds);
        _vectorDocRepoMock.Verify(r => r.AnyBelongsToGameAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Validate_KbCardIdsWithoutGameId_NoKbCardsValidationRun()
    {
        // Arrange -- KbCardIds provided but no GameId -> rule condition false -> not triggered
        var command = ValidCommand(kbCardIds: new List<Guid> { _kbCardId }, gameId: null);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.KbCardIds);
        _vectorDocRepoMock.Verify(r => r.AnyBelongsToGameAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Validate_KbCardsBelongToGame_PassesValidation()
    {
        // Arrange
        _vectorDocRepoMock
            .Setup(r => r.AnyBelongsToGameAsync(
                It.IsAny<IEnumerable<Guid>>(), _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = ValidCommand(kbCardIds: new List<Guid> { _kbCardId }, gameId: _gameId);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.KbCardIds);
    }

    [Fact]
    public async Task Validate_KbCardsDoNotBelongToGame_FailsValidation()
    {
        // Arrange
        _vectorDocRepoMock
            .Setup(r => r.AnyBelongsToGameAsync(
                It.IsAny<IEnumerable<Guid>>(), _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = ValidCommand(kbCardIds: new List<Guid> { _kbCardId }, gameId: _gameId);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.KbCardIds)
            .WithErrorMessage("L'agent deve avere almeno 1 KB card del game associato.");
    }

    [Fact]
    public async Task Validate_MultipleKbCardsAtLeastOneMatchesGame_PassesValidation()
    {
        // Arrange
        var otherCard = Guid.NewGuid();
        _vectorDocRepoMock
            .Setup(r => r.AnyBelongsToGameAsync(
                It.IsAny<IEnumerable<Guid>>(), _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = ValidCommand(
            kbCardIds: new List<Guid> { _kbCardId, otherCard },
            gameId: _gameId);

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.KbCardIds);
    }
}
