using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Unit tests for UpdateSetupChecklistCommand, Validator, and Handler.
/// TDD: Tests written first, then implementation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class UpdateSetupChecklistCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpdateSetupChecklistCommandHandler _handler;
    private readonly UpdateSetupChecklistCommandValidator _validator;

    private static readonly Guid DefaultSessionId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();

    public UpdateSetupChecklistCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new UpdateSetupChecklistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        _validator = new UpdateSetupChecklistCommandValidator();
    }

    // === Helpers ===

    private static LiveGameSession CreateSession(Guid? sessionId = null)
    {
        return LiveGameSession.Create(
            sessionId ?? DefaultSessionId,
            DefaultUserId,
            "Test Game",
            TimeProvider.System,
            gameId: Guid.NewGuid());
    }

    private static SetupChecklistData CreateChecklist(int playerCount = 4)
    {
        return new SetupChecklistData(
            playerCount,
            new List<SetupComponent> { new("Board", 1) },
            new List<SetupStep> { new(1, "Place board in center") });
    }

    private void SetupRepoGetById(Guid sessionId, LiveGameSession? session)
    {
        _repositoryMock
            .Setup(x => x.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    // === Validator tests ===

    [Fact]
    public void Validator_EmptySessionId_HasValidationError()
    {
        var command = new UpdateSetupChecklistCommand(Guid.Empty, CreateChecklist());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validator_NullChecklist_HasValidationError()
    {
        var command = new UpdateSetupChecklistCommand(DefaultSessionId, null!);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Checklist);
    }

    [Fact]
    public void Validator_ValidCommand_NoErrors()
    {
        var command = new UpdateSetupChecklistCommand(DefaultSessionId, CreateChecklist());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // === Handler tests ===

    [Fact]
    public async Task Handle_ValidSession_UpdatesChecklistSuccessfully()
    {
        // Arrange
        var session = CreateSession();
        SetupRepoGetById(DefaultSessionId, session);
        var checklist = CreateChecklist(3);
        var command = new UpdateSetupChecklistCommand(DefaultSessionId, checklist);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(session.SetupChecklist);
        Assert.Equal(3, session.SetupChecklist!.PlayerCount);
        Assert.Single(session.SetupChecklist.SetupSteps);
        Assert.Equal("Place board in center", session.SetupChecklist.SetupSteps[0].Instruction);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupRepoGetById(DefaultSessionId, null);
        var command = new UpdateSetupChecklistCommand(DefaultSessionId, CreateChecklist());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidSession_ReplacesExistingChecklist()
    {
        // Arrange
        var session = CreateSession();
        // Set an initial checklist
        session.SetSetupChecklist(new SetupChecklistData(
            2,
            new List<SetupComponent>(),
            new List<SetupStep> { new(1, "Old step") }));

        SetupRepoGetById(DefaultSessionId, session);

        // Create a new checklist with different data
        var newChecklist = new SetupChecklistData(
            5,
            new List<SetupComponent> { new("Dice", 2) },
            new List<SetupStep> { new(1, "New step 1"), new(2, "New step 2") });

        var command = new UpdateSetupChecklistCommand(DefaultSessionId, newChecklist);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - should have replaced entirely
        Assert.Equal(5, session.SetupChecklist!.PlayerCount);
        Assert.Equal(2, session.SetupChecklist.SetupSteps.Count);
        Assert.Equal("New step 1", session.SetupChecklist.SetupSteps[0].Instruction);
        Assert.Single(session.SetupChecklist.Components);
    }
}
