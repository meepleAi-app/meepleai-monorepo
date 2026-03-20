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
public class DeleteNoteCommandHandlerTests
{
    private readonly Mock<ISessionNoteRepository> _noteRepoMock = new();
    private readonly DeleteNoteCommandHandler _handler;

    public DeleteNoteCommandHandlerTests()
    {
        _handler = new DeleteNoteCommandHandler(_noteRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_SoftDeletesNote()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var note = SessionNote.Create(sessionId, participantId, "Secret strategy");

        _noteRepoMock.Setup(r => r.GetByIdAsync(note.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var command = new DeleteNoteCommand(note.Id, sessionId, participantId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Deleted);
        Assert.Equal(note.Id, result.NoteId);
        _noteRepoMock.Verify(r => r.UpdateAsync(note, It.IsAny<CancellationToken>()), Times.Once);
        _noteRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoteNotFound_ThrowsNotFoundException()
    {
        // Arrange
        _noteRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionNote?)null);

        var command = new DeleteNoteCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WrongOwner_ThrowsForbiddenException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var note = SessionNote.Create(sessionId, ownerId, "My note");

        _noteRepoMock.Setup(r => r.GetByIdAsync(note.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var command = new DeleteNoteCommand(note.Id, sessionId, Guid.NewGuid()); // different participant

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WrongSession_ThrowsConflictException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var note = SessionNote.Create(sessionId, participantId, "My note");

        _noteRepoMock.Setup(r => r.GetByIdAsync(note.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(note);

        var command = new DeleteNoteCommand(note.Id, Guid.NewGuid(), participantId); // wrong session

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
