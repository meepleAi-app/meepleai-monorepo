using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SaveNoteCommandHandlerTests
{
    private readonly Mock<ISessionNoteRepository> _noteRepoMock = new();
    private readonly SaveNoteCommandHandler _handler;

    public SaveNoteCommandHandlerTests()
    {
        _handler = new SaveNoteCommandHandler(_noteRepoMock.Object);
    }

    [Fact]
    public async Task Handle_NewNote_CreatesAndReturnsResponse()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var command = new SaveNoteCommand(sessionId, participantId, "My secret plan", "???");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(sessionId, result.SessionId);
        Assert.Equal(participantId, result.ParticipantId);
        Assert.False(result.IsRevealed);
        Assert.Equal("???", result.ObscuredText);
        _noteRepoMock.Verify(r => r.AddAsync(It.IsAny<SessionNote>(), It.IsAny<CancellationToken>()), Times.Once);
        _noteRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateExistingNote_UpdatesContentAndReturns()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var existingNote = SessionNote.Create(sessionId, participantId, "Old content");

        _noteRepoMock.Setup(r => r.GetByIdAsync(existingNote.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingNote);

        var command = new SaveNoteCommand(sessionId, participantId, "Updated content", null, existingNote.Id);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(existingNote.Id, result.NoteId);
        _noteRepoMock.Verify(r => r.UpdateAsync(existingNote, It.IsAny<CancellationToken>()), Times.Once);
        _noteRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateNonExistentNote_ThrowsNotFoundException()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        _noteRepoMock.Setup(r => r.GetByIdAsync(noteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionNote?)null);

        var command = new SaveNoteCommand(Guid.NewGuid(), Guid.NewGuid(), "content", null, noteId);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UpdateWrongOwner_ThrowsForbiddenException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var note = SessionNote.Create(sessionId, ownerId, "Owner's note");

        _noteRepoMock.Setup(r => r.GetByIdAsync(note.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var command = new SaveNoteCommand(sessionId, Guid.NewGuid(), "Hijacked", null, note.Id);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
