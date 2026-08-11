using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class AddNoteCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly MeepleAiDbContext _context;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISessionSyncService> _syncServiceMock = new();

    public AddNoteCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"AddNoteTest_{Guid.NewGuid()}")
            .Options;
        // Real DbContext + InMemory provider instead of Mock<MeepleAiDbContext>: Castle's
        // proxy generator demands an exact-arity ctor match and breaks every time the
        // DbContext gains a new optional dependency (Issue #661 → ILogger, Issue #1535 T3
        // → IOptions<DomainEventOutboxOptions>). The handler's NotFound short-circuit
        // never touches the context, so a real instance with stub collaborators is the
        // minimal-coupling fixture that survives future ctor evolution.
        _context = new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var handler = new AddNoteCommandHandler(
            _sessionRepoMock.Object, _context,
            _unitOfWorkMock.Object, _syncServiceMock.Object);

        var sessionId = Guid.NewGuid();
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new AddNoteCommand(sessionId, Guid.NewGuid(), "Shared", null, "Test note", false, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CallerNotOwnerOrParticipant_ThrowsForbiddenException()
    {
        // Arrange — session owned by someone else; caller is neither owner nor participant (IDOR).
        var handler = new AddNoteCommandHandler(
            _sessionRepoMock.Object, _context,
            _unitOfWorkMock.Object, _syncServiceMock.Object);

        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var session = Session.Create(ownerId, Guid.NewGuid(), SessionType.Generic);

        _sessionRepoMock.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var command = new AddNoteCommand(
            session.Id, session.Participants.First().Id, "Shared", null, "Test note", false, attackerId);

        // Act & Assert — IDOR guard rejects before any note is persisted.
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(command, CancellationToken.None));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
