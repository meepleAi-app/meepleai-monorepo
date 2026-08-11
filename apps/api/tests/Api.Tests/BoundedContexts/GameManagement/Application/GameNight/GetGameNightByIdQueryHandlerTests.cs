using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #2698: the detail read must be participant-scoped (organizer or invited), closing the
/// cross-tenant IDOR where any authenticated user could read another user's private game night.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetGameNightByIdQueryHandlerTests : IDisposable
{
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly MeepleAiDbContext _db;
    private readonly GetGameNightByIdQueryHandler _handler;

    public GetGameNightByIdQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GetGameNightByIdQueryHandler(_repo.Object, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private GameNightEvent PublishedEventWith(Guid organizerId, params Guid[] invitedUserIds)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Serata privata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        evt.Publish(invitedUserIds.ToList());
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);
        return evt;
    }

    [Fact]
    public async Task Handle_NonParticipant_ThrowsForbidden()
    {
        // The core IDOR: a random authenticated user must NOT read another user's private night.
        var evt = PublishedEventWith(Guid.NewGuid());

        var act = () => _handler.Handle(
            new GetGameNightByIdQuery(evt.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_Organizer_ReturnsDto()
    {
        var organizerId = Guid.NewGuid();
        var evt = PublishedEventWith(organizerId);

        var result = await _handler.Handle(
            new GetGameNightByIdQuery(evt.Id, organizerId), TestContext.Current.CancellationToken);

        result.Id.Should().Be(evt.Id);
    }

    [Fact]
    public async Task Handle_InvitedUser_ReturnsDto()
    {
        // Invitees get a Pending RSVP on Publish, so they can read the detail to respond.
        var invitedUserId = Guid.NewGuid();
        var evt = PublishedEventWith(Guid.NewGuid(), invitedUserId);

        var result = await _handler.Handle(
            new GetGameNightByIdQuery(evt.Id, invitedUserId), TestContext.Current.CancellationToken);

        result.Id.Should().Be(evt.Id);
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var act = () => _handler.Handle(
            new GetGameNightByIdQuery(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
