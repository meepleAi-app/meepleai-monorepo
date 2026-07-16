using System.Text.Json;

using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;

using Microsoft.Extensions.Time.Testing;

using Moq;

using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>#3025 L1: UpdateLiveGameStateCommand — host-authz write path.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class UpdateLiveGameStateCommandHandlerTests
{
    private readonly Mock<ILiveSessionRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly FakeTimeProvider _timeProvider =
        new(new DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero));

    private UpdateLiveGameStateCommandHandler CreateSut() => new(_repo.Object, _uow.Object);

    private static JsonElement State(string json) => JsonDocument.Parse(json).RootElement;

    private LiveGameSession CreateInProgressSession(Guid creator)
    {
        var session = LiveGameSession.Create(Guid.NewGuid(), creator, "Mage Knight", _timeProvider);
        session.AddPlayer(null, "Alice", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);
        return session;
    }

    [Fact]
    public async Task Handle_AuthorizedCreator_UpdatesStateAndSaves()
    {
        var creator = Guid.NewGuid();
        var session = CreateInProgressSession(creator);
        _repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        await CreateSut().Handle(
            new UpdateLiveGameStateCommand(session.Id, creator, State("""{"x":1}""")),
            CancellationToken.None);

        _repo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((LiveGameSession?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateSut().Handle(
                new UpdateLiveGameStateCommand(Guid.NewGuid(), Guid.NewGuid(), State("{}")),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonParticipant_ThrowsForbiddenAndDoesNotSave()
    {
        var session = CreateInProgressSession(Guid.NewGuid());
        _repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateSut().Handle(
                new UpdateLiveGameStateCommand(session.Id, Guid.NewGuid() /* stranger */, State("{}")),
                CancellationToken.None));

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
