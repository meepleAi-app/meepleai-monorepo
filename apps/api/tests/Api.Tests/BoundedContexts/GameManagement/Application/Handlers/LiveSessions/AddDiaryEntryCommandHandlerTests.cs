using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Unit tests for AddDiaryEntryCommand, AddDiaryEntryCommandValidator, and AddDiaryEntryCommandHandler.
/// TDD: Tests written first (RED), then implementation (GREEN).
/// Issue #2570 SP3 T3.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class AddDiaryEntryCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddDiaryEntryCommandHandler _handler;
    private readonly AddDiaryEntryCommandValidator _validator;

    private static readonly Guid DefaultSessionId = Guid.NewGuid();
    private static readonly Guid DefaultAuthorId = Guid.NewGuid();
    private const string DefaultText = "We played the first round and it was amazing!";

    public AddDiaryEntryCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILiveSessionRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new AddDiaryEntryCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object);

        _validator = new AddDiaryEntryCommandValidator();
    }

    // === Helpers ===

    private static LiveGameSession CreateActiveSession(Guid? sessionId = null)
    {
        var id = sessionId ?? DefaultSessionId;
        var session = LiveGameSession.Create(id, DefaultAuthorId, "Test Game", TimeProvider.System);
        // Start requires at least one player
        session.AddPlayer(DefaultAuthorId, "Host Player", PlayerColor.Red, TimeProvider.System);
        session.Start(TimeProvider.System);
        return session;
    }

    private static LiveGameSession CreateCompletedSession(Guid? sessionId = null)
    {
        var session = CreateActiveSession(sessionId);
        session.Complete(TimeProvider.System);
        return session;
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
        var command = new AddDiaryEntryCommand(Guid.Empty, DefaultAuthorId, DefaultText);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validator_EmptyAuthorId_HasValidationError()
    {
        var command = new AddDiaryEntryCommand(DefaultSessionId, Guid.Empty, DefaultText);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.AuthorId);
    }

    [Fact]
    public void Validator_EmptyText_HasValidationError()
    {
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, string.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void Validator_WhitespaceText_HasValidationError()
    {
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, "   ");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void Validator_TextExceedsCap_HasValidationError()
    {
        var overCapText = new string('a', 2001);
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, overCapText);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void Validator_TextAtMaxLength_IsValid()
    {
        var maxText = new string('a', 2000);
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, maxText);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void Validator_ValidCommand_NoErrors()
    {
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, DefaultText);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // === Handler tests ===

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        SetupRepoGetById(DefaultSessionId, null);
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, DefaultText);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CallerIsNotParticipant_ThrowsForbiddenException()
    {
        // Arrange — session created by DefaultAuthorId, caller is a different user (nonParticipant)
        var session = CreateActiveSession();
        SetupRepoGetById(DefaultSessionId, session);
        var nonParticipantId = Guid.NewGuid();
        var command = new AddDiaryEntryCommand(DefaultSessionId, nonParticipantId, DefaultText);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>(
            "only the session creator or an active participant may add diary entries");
    }

    [Fact]
    public async Task Handle_NotFoundTakesPrecedenceOver403()
    {
        // Arrange — session doesn't exist; non-participant id
        SetupRepoGetById(DefaultSessionId, null);
        var command = new AddDiaryEntryCommand(DefaultSessionId, Guid.NewGuid(), DefaultText);

        // Act & Assert — 404 fires before 403 (load-first pattern)
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SessionNotFound_DoesNotCallSaveChanges()
    {
        // Arrange
        SetupRepoGetById(DefaultSessionId, null);
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, DefaultText);

        // Act
        try { await _handler.Handle(command, CancellationToken.None); } catch { /* expected */ }

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CompletedSession_ThrowsConflictException()
    {
        // Arrange
        var session = CreateCompletedSession();
        SetupRepoGetById(DefaultSessionId, session);
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, DefaultText);

        // Act & Assert
        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsDiaryEntryId()
    {
        // Arrange
        var session = CreateActiveSession();
        SetupRepoGetById(DefaultSessionId, session);
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, DefaultText);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidCommand_EntryAppendsToSession()
    {
        // Arrange
        var session = CreateActiveSession();
        SetupRepoGetById(DefaultSessionId, session);
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, DefaultText);

        // Act
        var entryId = await _handler.Handle(command, CancellationToken.None);

        // Assert
        session.DiaryEntries.Should().HaveCount(1);
        session.DiaryEntries[0].Id.Should().Be(entryId);
        session.DiaryEntries[0].AuthorId.Should().Be(DefaultAuthorId);
        session.DiaryEntries[0].Text.Should().Be(DefaultText);
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsSaveChangesOnce()
    {
        // Arrange
        var session = CreateActiveSession();
        SetupRepoGetById(DefaultSessionId, session);
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, DefaultText);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsRepositoryUpdateAsync()
    {
        // Arrange
        var session = CreateActiveSession();
        SetupRepoGetById(DefaultSessionId, session);
        var command = new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, DefaultText);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleEntries_AllAppended()
    {
        // Arrange
        var session = CreateActiveSession();
        SetupRepoGetById(DefaultSessionId, session);

        // Act
        var id1 = await _handler.Handle(new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, "Entry one"), CancellationToken.None);
        var id2 = await _handler.Handle(new AddDiaryEntryCommand(DefaultSessionId, DefaultAuthorId, "Entry two"), CancellationToken.None);

        // Assert
        session.DiaryEntries.Should().HaveCount(2);
        session.DiaryEntries[0].Id.Should().Be(id1);
        session.DiaryEntries[1].Id.Should().Be(id2);
    }
}
