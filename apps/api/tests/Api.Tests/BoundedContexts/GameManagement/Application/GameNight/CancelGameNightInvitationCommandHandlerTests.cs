using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #4721: the organizer withdraws an invitation sent by email. Pending and accepted ones are cancelled
/// (the link stops working, an accepted guest loses the seat); declined and expired ones are history.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CancelGameNightInvitationCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameNightInvitationRepository> _invitations = new();
    private readonly Mock<IGameNightEventRepository> _gameNights = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDomainEventCollector> _events = new();
    private readonly Mock<HybridCache> _cache = new();
    private readonly CancelGameNightInvitationCommandHandler _sut;
    private readonly Guid _organizerId = Guid.NewGuid();
    private readonly GameNightEvent _night;

    public CancelGameNightInvitationCommandHandlerTests()
    {
        _night = GameNightEvent.Create(_organizerId, "Serata Catan", Now.AddDays(2), gameIds: [Guid.NewGuid()]);
        _gameNights.Setup(r => r.GetByIdAsync(_night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_night);
        _sut = new CancelGameNightInvitationCommandHandler(
            _invitations.Object,
            _gameNights.Object,
            _unitOfWork.Object,
            _events.Object,
            _cache.Object,
            new FakeTimeProvider(Now),
            NullLogger<CancelGameNightInvitationCommandHandler>.Instance);
    }

    private GameNightInvitation Invitation(GameNightInvitationStatus status, Guid? gameNightId = null)
    {
        var invitation = GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: "tok" + Guid.NewGuid().ToString("N")[..20],
            gameNightId: gameNightId ?? _night.Id,
            email: "carla@example.com",
            status: status,
            expiresAt: Now.AddDays(7),
            respondedAt: status == GameNightInvitationStatus.Pending ? null : Now.AddHours(-1),
            respondedByUserId: null,
            createdAt: Now.AddDays(-1),
            createdBy: _organizerId);
        _invitations.Setup(r => r.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);
        return invitation;
    }

    private Task Handle(GameNightInvitation invitation, Guid? caller = null) =>
        _sut.Handle(
            new CancelGameNightInvitationCommand(_night.Id, invitation.Id, caller ?? _organizerId),
            TestContext.Current.CancellationToken);

    private void VerifyNothingWasWritten()
    {
        _invitations.Verify(r => r.UpdateAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Pending)]
    [InlineData(GameNightInvitationStatus.Accepted)]
    public async Task PendingOrAccepted_BecomesCancelled_IsSaved_AndItsPublicPageIsDropped(GameNightInvitationStatus status)
    {
        var invitation = Invitation(status);

        await Handle(invitation);

        invitation.Status.Should().Be(GameNightInvitationStatus.Cancelled);
        _invitations.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(
            c => c.RemoveByTagAsync(
                GetGameNightInvitationByTokenQueryHandler.CacheTagPrefix + invitation.Token, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AlreadyCancelled_IsNotAnError_AndWritesNothing()
    {
        var invitation = Invitation(GameNightInvitationStatus.Cancelled);

        await Handle(invitation);

        invitation.Status.Should().Be(GameNightInvitationStatus.Cancelled);
        VerifyNothingWasWritten();
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Declined)]
    [InlineData(GameNightInvitationStatus.Expired)]
    public async Task DeclinedOrExpired_IsHistory_AndIsRefusedWithAConflict(GameNightInvitationStatus status)
    {
        var invitation = Invitation(status);

        var act = () => Handle(invitation);

        await act.Should().ThrowAsync<ConflictException>();
        invitation.Status.Should().Be(status);
        VerifyNothingWasWritten();
    }

    [Fact]
    public async Task SomeoneWhoIsNotTheOrganizer_IsRefused_BeforeAnyInvitationIsRead()
    {
        var invitation = Invitation(GameNightInvitationStatus.Pending);

        var act = () => Handle(invitation, caller: Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenException>();
        invitation.Status.Should().Be(GameNightInvitationStatus.Pending);
        _invitations.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingWasWritten();
    }

    [Fact]
    public async Task UnknownGameNight_IsNotFound()
    {
        var act = () => _sut.Handle(
            new CancelGameNightInvitationCommand(Guid.NewGuid(), Guid.NewGuid(), _organizerId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UnknownInvitation_IsNotFound()
    {
        var act = () => _sut.Handle(
            new CancelGameNightInvitationCommand(_night.Id, Guid.NewGuid(), _organizerId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
        VerifyNothingWasWritten();
    }

    [Fact]
    public async Task AnInvitationOfAnotherNight_IsNotFound_EvenForAnOrganizer()
    {
        // The caller owns THIS night: an id from somebody else's night must not be withdrawable through it.
        var elsewhere = Invitation(GameNightInvitationStatus.Pending, gameNightId: Guid.NewGuid());

        var act = () => Handle(elsewhere);

        await act.Should().ThrowAsync<NotFoundException>();
        elsewhere.Status.Should().Be(GameNightInvitationStatus.Pending);
        VerifyNothingWasWritten();
    }

    [Fact]
    public async Task WhenDroppingTheCachedPageFails_TheWithdrawalStillSucceeds()
    {
        // The write is committed; an L2 that is down must not turn a done thing into an error to repeat.
        var invitation = Invitation(GameNightInvitationStatus.Pending);
        _cache.Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("L2 is down"));

        await Handle(invitation);

        invitation.Status.Should().Be(GameNightInvitationStatus.Cancelled);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ACancellationWhileDroppingTheCachedPage_IsNotSwallowed()
    {
        var invitation = Invitation(GameNightInvitationStatus.Pending);
        _cache.Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Throws(new OperationCanceledException());

        var act = () => Handle(invitation);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Another writer got there first (#4744) ───────────────────────────────────────────────────────────────
    // The invitation carries its row version: a withdrawal that overlaps the guest's answer is refused when it is
    // saved, and decided again from a fresh read. Nothing is lost and nothing is undone.

    private GameNightInvitation Reread(GameNightInvitation of, GameNightInvitationStatus status) =>
        GameNightInvitation.Reconstitute(
            id: of.Id,
            token: of.Token,
            gameNightId: of.GameNightId,
            email: of.Email,
            status: status,
            expiresAt: of.ExpiresAt,
            respondedAt: status == GameNightInvitationStatus.Pending ? null : Now.AddMinutes(-1),
            respondedByUserId: null,
            createdAt: of.CreatedAt,
            createdBy: of.CreatedBy);

    private void GivenTheReads(GameNightInvitation first, params GameNightInvitation[] rest)
    {
        var sequence = _invitations
            .SetupSequence(r => r.GetByIdAsync(first.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(first);
        foreach (var read in rest)
        {
            sequence = sequence.ReturnsAsync(read);
        }
    }

    private void GivenTheSaveIsRefused(int times)
    {
        var sequence = _unitOfWork.SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()));
        for (var i = 0; i < times; i++)
        {
            sequence = sequence.ThrowsAsync(new DbUpdateConcurrencyException("another writer got there first"));
        }

        sequence.ReturnsAsync(1);
    }

    [Fact]
    public async Task TheSaveIsRefused_AndTheGuestAcceptedMeanwhile_TheWithdrawalStillGoesThrough_OnTheFreshRow()
    {
        var read = Invitation(GameNightInvitationStatus.Pending);
        var fresh = Reread(read, GameNightInvitationStatus.Accepted);
        GivenTheReads(read, fresh);
        GivenTheSaveIsRefused(1);

        await Handle(read);

        fresh.Status.Should().Be(GameNightInvitationStatus.Cancelled);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _events.Verify(e => e.Clear(), Times.Once, "the refused attempt's events must not be published with the retry's");
        _cache.Verify(
            c => c.RemoveByTagAsync(
                GetGameNightInvitationByTokenQueryHandler.CacheTagPrefix + read.Token, It.IsAny<CancellationToken>()),
            Times.Once,
            "the page is dropped once, after the save that went through, not after the refused one");
    }

    [Fact]
    public async Task TheSaveIsRefused_AndTheGuestDeclinedMeanwhile_TheWithdrawalIsAConflict_AndDropsNothing()
    {
        // A declined invitation is history. Before the row version the withdrawal rewrote the row and turned the
        // guest's «no» into «withdrawn».
        var read = Invitation(GameNightInvitationStatus.Pending);
        GivenTheReads(read, Reread(read, GameNightInvitationStatus.Declined));
        GivenTheSaveIsRefused(1);

        var act = () => Handle(read);

        await act.Should().ThrowAsync<ConflictException>();
        _cache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheSaveIsRefused_AndAnotherWithdrawalWonTheRace_ItIsTheIdempotentNoOp()
    {
        var read = Invitation(GameNightInvitationStatus.Pending);
        GivenTheReads(read, Reread(read, GameNightInvitationStatus.Cancelled));
        GivenTheSaveIsRefused(1);

        await Handle(read);

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheSaveIsRefusedEveryTime_GivesUpWithAConflict()
    {
        var read = Invitation(GameNightInvitationStatus.Pending);
        GivenTheReads(
            read,
            Reread(read, GameNightInvitationStatus.Pending),
            Reread(read, GameNightInvitationStatus.Pending));
        GivenTheSaveIsRefused(10);

        var act = () => Handle(read);

        await act.Should().ThrowAsync<ConflictException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _cache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
