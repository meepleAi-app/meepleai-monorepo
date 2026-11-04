using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// #4846. The unsubscribe KEY of an invitation is the credential for «stop writing to this address»: it names the address
/// and the invitation, it travels only in the emails that go to that address, and the guest has no account to log
/// in to. It is not the RSVP token: the organizer holds that one. It works whatever became of the
/// invitation (answered, cancelled, expired): a person must be able to stop the mail from a note they still
/// have, even when the night it announced is long gone.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4846")]
public sealed class OptOutGameNightGuestEmailCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 17, 9, 0, 0, TimeSpan.Zero);
    private const string Token = "AbCdEfGhIjKlMnOpQrStUv";
    private const string Key = "ZyXwVuTsRqPoNmLkJiHgFe";

    private readonly Mock<IGameNightInvitationRepository> _invitations = new();
    private readonly Mock<IGameNightGuestEmailOptOutRepository> _optOuts = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly OptOutGameNightGuestEmailCommandHandler _sut;

    public OptOutGameNightGuestEmailCommandHandlerTests()
    {
        _sut = new OptOutGameNightGuestEmailCommandHandler(
            _invitations.Object,
            _optOuts.Object,
            _unitOfWork.Object,
            new FakeTimeProvider(Now));
    }

    private static GameNightInvitation InvitationIn(GameNightInvitationStatus status, string email = "guest@example.com") =>
        GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: Token,
            unsubscribeKey: Key,
            gameNightId: Guid.NewGuid(),
            email: email,
            status: status,
            expiresAt: status == GameNightInvitationStatus.Expired ? Now.AddDays(-1) : Now.AddDays(3),
            respondedAt: status is GameNightInvitationStatus.Accepted or GameNightInvitationStatus.Declined ? Now : null,
            respondedByUserId: null,
            createdAt: Now.AddDays(-2),
            createdBy: Guid.NewGuid());

    private GameNightInvitation GivenInvitation(GameNightInvitationStatus status = GameNightInvitationStatus.Pending, string email = "guest@example.com")
    {
        var invitation = InvitationIn(status, email);
        _invitations
            .Setup(r => r.GetByUnsubscribeKeyAsync(Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        return invitation;
    }

    [Fact]
    public async Task AnUnknownToken_IsNotFound_AndNothingIsStored()
    {
        _invitations
            .Setup(r => r.GetByUnsubscribeKeyAsync(Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightInvitation?)null);

        var act = () => _sut.Handle(new OptOutGameNightGuestEmailCommand(Key), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _optOuts.Verify(r => r.AddAsync(It.IsAny<GameNightGuestEmailOptOut>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheRsvpToken_IsNotTheCredential_TheOrganizerHoldsIt_AndTheKeyIsOnlyInTheEmails()
    {
        // The organizer gets the RSVP token back when they create the invitation, so it cannot prove that whoever
        // holds it controls the address. The opt-out is global (every organizer's mail to that address), so it has
        // its own key, which is written in the emails and nowhere else. Asking with the RSVP token finds nothing.
        GivenInvitation();

        var act = () => _sut.Handle(new OptOutGameNightGuestEmailCommand(Token), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _invitations.Verify(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _optOuts.Verify(r => r.AddAsync(It.IsAny<GameNightGuestEmailOptOut>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Pending)]
    [InlineData(GameNightInvitationStatus.Accepted)]
    [InlineData(GameNightInvitationStatus.Declined)]
    [InlineData(GameNightInvitationStatus.Cancelled)]
    [InlineData(GameNightInvitationStatus.Expired)]
    public async Task TheAddressOfTheInvitationIsOptedOut_WhateverTheStateOfTheInvitation(GameNightInvitationStatus status)
    {
        var invitation = GivenInvitation(status, email: "guest@example.com");
        GameNightGuestEmailOptOut? stored = null;
        _optOuts
            .Setup(r => r.AddAsync(It.IsAny<GameNightGuestEmailOptOut>(), It.IsAny<CancellationToken>()))
            .Callback<GameNightGuestEmailOptOut, CancellationToken>((o, _) => stored = o)
            .Returns(Task.CompletedTask);

        await _sut.Handle(new OptOutGameNightGuestEmailCommand(Key), CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.Email.Should().Be("guest@example.com");
        stored.SourceInvitationId.Should().Be(invitation.Id);
        stored.OptedOutAt.Should().Be(Now);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnAddressAlreadyOptedOut_IsASuccess_AndNothingIsWrittenTwice()
    {
        GivenInvitation();
        _optOuts
            .Setup(r => r.ExistsAsync("guest@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.Handle(new OptOutGameNightGuestEmailCommand(Key), CancellationToken.None);

        _optOuts.Verify(r => r.AddAsync(It.IsAny<GameNightGuestEmailOptOut>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ARequestThatLostTheRaceForTheSameAddress_IsASuccessToo()
    {
        // Two taps of the same button: both read «not yet», one insert wins, the other hits the unique index.
        GivenInvitation();
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "unique violation",
                new PostgresException("duplicate key", "ERROR", "ERROR", "23505")));

        var act = () => _sut.Handle(new OptOutGameNightGuestEmailCommand(Key), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AnyOtherDatabaseFailure_IsNotHidden()
    {
        GivenInvitation();
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "connection lost",
                new PostgresException("terminating connection", "FATAL", "FATAL", "57P01")));

        var act = () => _sut.Handle(new OptOutGameNightGuestEmailCommand(Key), CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
