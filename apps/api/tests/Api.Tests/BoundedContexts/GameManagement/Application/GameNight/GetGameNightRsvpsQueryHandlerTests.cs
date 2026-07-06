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
/// #2698: the RSVP roster read must be participant-scoped (organizer or invited), closing the
/// cross-tenant IDOR where any authenticated user could read another user's roster + names.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetGameNightRsvpsQueryHandlerTests : IDisposable
{
    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly MeepleAiDbContext _db;
    private readonly GetGameNightRsvpsQueryHandler _handler;

    public GetGameNightRsvpsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GetGameNightRsvpsQueryHandler(_repo.Object, _db);
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
        // The core IDOR: a random authenticated user must NOT read another user's roster.
        var evt = PublishedEventWith(Guid.NewGuid(), Guid.NewGuid());

        var act = () => _handler.Handle(
            new GetGameNightRsvpsQuery(evt.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_Organizer_ReturnsRoster()
    {
        var organizerId = Guid.NewGuid();
        var evt = PublishedEventWith(organizerId, Guid.NewGuid());

        var result = await _handler.Handle(
            new GetGameNightRsvpsQuery(evt.Id, organizerId), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_InvitedUser_ReturnsRoster()
    {
        var invitedUserId = Guid.NewGuid();
        var evt = PublishedEventWith(Guid.NewGuid(), invitedUserId);

        var result = await _handler.Handle(
            new GetGameNightRsvpsQuery(evt.Id, invitedUserId), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
    }
}
