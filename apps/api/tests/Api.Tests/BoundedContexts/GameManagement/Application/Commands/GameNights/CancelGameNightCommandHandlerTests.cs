using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// #4856. The public page of an invitation link is cached for a minute, per token. The cancellation of a night
/// changes the NIGHT; the invitations are another aggregate and are not touched, so nothing evicted their pages: for
/// up to a minute the link of a cancelled night still said «come and answer», with the title, the place and the time.
/// The command drops the page of each invitation of the night after the commit — in the handler and not from the
/// cancellation event, which in staging and production runs from the outbox seconds later, after the emails.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4856")]
public sealed class CancelGameNightCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameNightEventRepository> _nights = new();
    private readonly Mock<IGameNightInvitationRepository> _invitations = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<HybridCache> _cache = new();
    private readonly Mock<ILogger<CancelGameNightCommandHandler>> _logger = new();

    private CancelGameNightCommandHandler CreateHandler() =>
        new(_nights.Object, _invitations.Object, _unitOfWork.Object, _cache.Object, _logger.Object);

    private static GameNightInvitation Invitation(Guid nightId, string token, GameNightInvitationStatus status) =>
        GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: token,
            gameNightId: nightId,
            email: $"{token}@example.com",
            status: status,
            expiresAt: Now.AddDays(3),
            respondedAt: null,
            respondedByUserId: null,
            createdAt: Now.AddDays(-2),
            createdBy: Guid.NewGuid());

    private GameNightEvent GivenAPublishedNight(Guid organizerId)
    {
        var night = GameNightEvent.Create(organizerId, "Friday Catan", Now.AddDays(2));
        night.Publish([]);
        _nights.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        return night;
    }

    private void GivenInvitations(Guid nightId, params GameNightInvitation[] invitations) =>
        _invitations
            .Setup(r => r.GetByGameNightIdAsync(nightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitations);

    private void VerifyDropped(string token, Times times) =>
        _cache.Verify(
            c => c.RemoveByTagAsync(
                GetGameNightInvitationByTokenQueryHandler.CacheTagPrefix + token, It.IsAny<CancellationToken>()),
            times);

    [Fact]
    public async Task Handle_CancelsTheNight_AndDropsThePageOfEveryInvitation_WhateverItsState()
    {
        var organizerId = Guid.NewGuid();
        var night = GivenAPublishedNight(organizerId);
        GivenInvitations(night.Id,
            Invitation(night.Id, "pending-token", GameNightInvitationStatus.Pending),
            Invitation(night.Id, "accepted-token", GameNightInvitationStatus.Accepted),
            Invitation(night.Id, "declined-token", GameNightInvitationStatus.Declined),
            Invitation(night.Id, "withdrawn-token", GameNightInvitationStatus.Cancelled));

        await CreateHandler().Handle(new CancelGameNightCommand(night.Id, organizerId), CancellationToken.None);

        night.Status.Should().Be(GameNightStatus.Cancelled);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        VerifyDropped("pending-token", Times.Once());
        VerifyDropped("accepted-token", Times.Once());
        VerifyDropped("declined-token", Times.Once());
        VerifyDropped("withdrawn-token", Times.Once());
    }

    [Fact]
    public async Task Handle_ThePagesAreDroppedAfterTheCommit_NotBefore()
    {
        // A page dropped before the commit can be rebuilt, still live, by a read that lands in between.
        var organizerId = Guid.NewGuid();
        var night = GivenAPublishedNight(organizerId);
        GivenInvitations(night.Id, Invitation(night.Id, "tok", GameNightInvitationStatus.Pending));
        var order = new List<string>();
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("save"))
            .ReturnsAsync(1);
        _cache
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("drop"))
            .Returns(ValueTask.CompletedTask);

        await CreateHandler().Handle(new CancelGameNightCommand(night.Id, organizerId), CancellationToken.None);

        order.Should().Equal("save", "drop");
    }

    [Fact]
    public async Task Handle_ANightAlreadyCancelled_DropsThePagesAgain_SoARetryRepairsAFailedDrop()
    {
        var organizerId = Guid.NewGuid();
        var night = GivenAPublishedNight(organizerId);
        night.Cancel();
        GivenInvitations(night.Id, Invitation(night.Id, "tok", GameNightInvitationStatus.Accepted));

        await CreateHandler().Handle(new CancelGameNightCommand(night.Id, organizerId), CancellationToken.None);

        VerifyDropped("tok", Times.Once());
    }

    [Fact]
    public async Task Handle_ANightWithNoInvitations_DropsNothing()
    {
        var organizerId = Guid.NewGuid();
        var night = GivenAPublishedNight(organizerId);
        GivenInvitations(night.Id);

        await CreateHandler().Handle(new CancelGameNightCommand(night.Id, organizerId), CancellationToken.None);

        _cache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotTheOrganizer_CancelsNothing_AndDropsNothing()
    {
        var night = GivenAPublishedNight(Guid.NewGuid());
        GivenInvitations(night.Id, Invitation(night.Id, "tok", GameNightInvitationStatus.Pending));

        var act = () => CreateHandler().Handle(
            new CancelGameNightCommand(night.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        night.Status.Should().Be(GameNightStatus.Published);
        _cache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownNight_IsNotFound_AndDropsNothing()
    {
        var nightId = Guid.NewGuid();
        _nights.Setup(r => r.GetByIdAsync(nightId, It.IsAny<CancellationToken>())).ReturnsAsync((GameNightEvent?)null);

        var act = () => CreateHandler().Handle(
            new CancelGameNightCommand(nightId, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _cache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OneDropFails_TheOthersAreStillDropped_AndTheCancellationStands()
    {
        // The cancellation is committed: a Redis that is down must not turn it into a 500 that makes the organizer
        // repeat a done thing, and the page expires within a minute anyway.
        var organizerId = Guid.NewGuid();
        var night = GivenAPublishedNight(organizerId);
        GivenInvitations(night.Id,
            Invitation(night.Id, "first", GameNightInvitationStatus.Pending),
            Invitation(night.Id, "second", GameNightInvitationStatus.Accepted));
        _cache
            .Setup(c => c.RemoveByTagAsync(
                GetGameNightInvitationByTokenQueryHandler.CacheTagPrefix + "first", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var act = () => CreateHandler().Handle(new CancelGameNightCommand(night.Id, organizerId), CancellationToken.None);

        await act.Should().NotThrowAsync();
        night.Status.Should().Be(GameNightStatus.Cancelled);
        VerifyDropped("second", Times.Once());
    }

    [Fact]
    public async Task Handle_TheListOfInvitationsCannotBeRead_TheCancellationStands()
    {
        var organizerId = Guid.NewGuid();
        var night = GivenAPublishedNight(organizerId);
        _invitations
            .Setup(r => r.GetByGameNightIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => CreateHandler().Handle(new CancelGameNightCommand(night.Id, organizerId), CancellationToken.None);

        await act.Should().NotThrowAsync();
        night.Status.Should().Be(GameNightStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_TheCallerLeavesWhileDropping_TheCancellationIsNotSwallowed()
    {
        var organizerId = Guid.NewGuid();
        var night = GivenAPublishedNight(organizerId);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _invitations
            .Setup(r => r.GetByGameNightIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => CreateHandler().Handle(new CancelGameNightCommand(night.Id, organizerId), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
