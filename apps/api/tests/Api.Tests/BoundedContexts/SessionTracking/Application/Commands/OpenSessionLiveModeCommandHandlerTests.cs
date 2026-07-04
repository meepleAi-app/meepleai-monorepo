using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Unit tests for <see cref="OpenSessionLiveModeCommandHandler"/> (WS1 DEC-1/6, issue #2647):
/// opens live mode on a tracking Session, idempotent when already live, NotFound when missing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class OpenSessionLiveModeCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private OpenSessionLiveModeCommandHandler CreateHandler() => new(_repo.Object, _uow.Object);

    private static Session NewSession() =>
        Session.Create(Guid.NewGuid(), Guid.NewGuid(), SessionType.GameSpecific);

    [Fact]
    public async Task Handle_OpensLiveMode_SetsStartedAt_AndSaves()
    {
        var session = NewSession();
        _repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        await CreateHandler().Handle(new OpenSessionLiveModeCommand(session.Id), CancellationToken.None);

        session.IsLive.Should().BeTrue();
        session.StartedAt.Should().NotBeNull();
        _repo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyLive_IsIdempotent_NoThrow_NoSave()
    {
        var session = NewSession();
        session.OpenLiveMode(); // already live
        _repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var act = async () =>
            await CreateHandler().Handle(new OpenSessionLiveModeCommand(session.Id), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionMissing_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var act = async () =>
            await CreateHandler().Handle(new OpenSessionLiveModeCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
