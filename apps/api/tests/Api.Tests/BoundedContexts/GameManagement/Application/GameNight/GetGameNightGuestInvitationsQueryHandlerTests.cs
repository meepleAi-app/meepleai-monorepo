using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
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
/// #4706: the guests invited by email were invisible to the organizer. This read lists them with
/// their state. It is organizer-only: the addresses are other people's personal data, so a
/// registered member who was invited does not get them.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class GetGameNightGuestInvitationsQueryHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameNightEventRepository> _repo = new();
    private readonly MeepleAiDbContext _db;
    private readonly GetGameNightGuestInvitationsQueryHandler _handler;

    public GetGameNightGuestInvitationsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(
            options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GetGameNightGuestInvitationsQueryHandler(_repo.Object, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private GameNightEvent PublishedEventWith(Guid organizerId, params Guid[] invitedUserIds)
    {
        var evt = GameNightEvent.Create(
            organizerId, "Serata Catan", Now.AddDays(2), gameIds: [Guid.NewGuid()]);
        evt.Publish(invitedUserIds.ToList());
        _repo.Setup(r => r.GetByIdAsync(evt.Id, It.IsAny<CancellationToken>())).ReturnsAsync(evt);
        return evt;
    }

    private void AddInvitation(
        Guid gameNightId,
        string email,
        GameNightInvitationStatus status,
        DateTimeOffset createdAt,
        string? respondedByName = null,
        DateTimeOffset? respondedAt = null)
    {
        _db.GameNightInvitations.Add(new GameNightInvitationEntity
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            GameNightId = gameNightId,
            Email = email,
            Status = status.ToString(),
            ExpiresAt = Now.AddDays(7),
            RespondedAt = respondedAt,
            RespondedByName = respondedByName,
            CreatedAt = createdAt,
            CreatedBy = Guid.NewGuid(),
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);

        var act = () => _handler.Handle(
            new GetGameNightGuestInvitationsQuery(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NonParticipant_ThrowsForbidden()
    {
        var evt = PublishedEventWith(Guid.NewGuid());
        AddInvitation(evt.Id, "carla@example.com", GameNightInvitationStatus.Pending, Now);

        var act = () => _handler.Handle(
            new GetGameNightGuestInvitationsQuery(evt.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_InvitedMemberWhoIsNotTheOrganizer_ThrowsForbidden()
    {
        // Being invited gives the right to the night's detail and roster, not to the other
        // guests' email addresses.
        var memberId = Guid.NewGuid();
        var evt = PublishedEventWith(Guid.NewGuid(), memberId);
        AddInvitation(evt.Id, "carla@example.com", GameNightInvitationStatus.Pending, Now);

        var act = () => _handler.Handle(
            new GetGameNightGuestInvitationsQuery(evt.Id, memberId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_Organizer_ListsEveryGuestWithItsStateInInvitationOrder()
    {
        var organizerId = Guid.NewGuid();
        var evt = PublishedEventWith(organizerId);
        AddInvitation(
            evt.Id, "dario@example.com", GameNightInvitationStatus.Pending, Now.AddMinutes(2));
        AddInvitation(
            evt.Id, "carla@example.com", GameNightInvitationStatus.Accepted, Now.AddMinutes(1),
            respondedByName: "Carla Bianchi", respondedAt: Now.AddHours(1));
        AddInvitation(
            evt.Id, "elena@example.com", GameNightInvitationStatus.Declined, Now.AddMinutes(3),
            respondedAt: Now.AddHours(2));

        var result = await _handler.Handle(
            new GetGameNightGuestInvitationsQuery(evt.Id, organizerId),
            TestContext.Current.CancellationToken);

        result.Select(r => r.Email).Should().Equal(
            "carla@example.com", "dario@example.com", "elena@example.com");
        var carla = result[0];
        carla.Status.Should().Be("Accepted");
        carla.RespondedByName.Should().Be("Carla Bianchi");
        carla.RespondedAt.Should().Be(Now.AddHours(1));
        result[1].Status.Should().Be("Pending");
        result[1].RespondedAt.Should().BeNull();
        result[2].Status.Should().Be("Declined");
    }

    [Fact]
    public async Task Handle_Organizer_LeavesOutCancelledInvitationsAndOtherNights()
    {
        var organizerId = Guid.NewGuid();
        var evt = PublishedEventWith(organizerId);
        AddInvitation(evt.Id, "carla@example.com", GameNightInvitationStatus.Pending, Now);
        AddInvitation(evt.Id, "revocato@example.com", GameNightInvitationStatus.Cancelled, Now);
        AddInvitation(
            Guid.NewGuid(), "altra-serata@example.com", GameNightInvitationStatus.Pending, Now);

        var result = await _handler.Handle(
            new GetGameNightGuestInvitationsQuery(evt.Id, organizerId),
            TestContext.Current.CancellationToken);

        result.Select(r => r.Email).Should().Equal("carla@example.com");
    }

    [Fact]
    public void Dto_DoesNotCarryTheResponseToken()
    {
        // The token is the secret that lets whoever holds it answer in the guest's name.
        typeof(GameNightGuestInvitationDto).GetProperties().Select(p => p.Name)
            .Should().NotContain("Token");
    }
}
