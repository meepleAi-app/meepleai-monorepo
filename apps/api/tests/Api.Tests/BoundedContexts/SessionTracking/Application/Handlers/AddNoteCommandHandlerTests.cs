using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class AddNoteCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<MeepleAiDbContext> _contextMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ISessionSyncService> _syncServiceMock = new();

    public AddNoteCommandHandlerTests()
    {
        _contextMock = new Mock<MeepleAiDbContext>(MockBehavior.Loose);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var handler = new AddNoteCommandHandler(
            _sessionRepoMock.Object, _contextMock.Object,
            _unitOfWorkMock.Object, _syncServiceMock.Object);

        var sessionId = Guid.NewGuid();
        _sessionRepoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var command = new AddNoteCommand(sessionId, Guid.NewGuid(), "Shared", null, "Test note", false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(command, CancellationToken.None));
    }
}
