using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// #4921: <see cref="GameNightInvitation.ChangeAnswer"/> is the one door through which an answered
/// invitation changes, and only the account that answered holds the key. The token path keeps its
/// D2 (b) rule — a replayed public link does not flip an answer — and these tests pin that the two
/// doors stay different.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4921")]
public sealed class GameNightInvitationChangeAnswerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static GameNightInvitation AnsweredBy(Guid responder, GameNightInvitationStatus status) =>
        GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: "tokentokentokentokent1",
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            status: status,
            expiresAt: Now.AddDays(7),
            respondedAt: Now.AddDays(-1),
            respondedByUserId: responder,
            createdAt: Now.AddDays(-2),
            createdBy: Guid.NewGuid());

    [Fact]
    public void TheResponder_CanSwitchTheAnswer_BothWays()
    {
        var me = Guid.NewGuid();
        var invitation = AnsweredBy(me, GameNightInvitationStatus.Accepted);

        invitation.ChangeAnswer(GameNightInvitationStatus.Declined, me, Now).Should().BeTrue();
        invitation.Status.Should().Be(GameNightInvitationStatus.Declined);
        invitation.RespondedAt.Should().Be(Now);

        invitation.ChangeAnswer(GameNightInvitationStatus.Accepted, me, Now.AddMinutes(1)).Should().BeTrue();
        invitation.Status.Should().Be(GameNightInvitationStatus.Accepted);
    }

    [Fact]
    public void TheSameAnswerAgain_ChangesNothing()
    {
        var me = Guid.NewGuid();
        var invitation = AnsweredBy(me, GameNightInvitationStatus.Accepted);
        var before = invitation.RespondedAt;

        invitation.ChangeAnswer(GameNightInvitationStatus.Accepted, me, Now).Should().BeFalse();
        invitation.RespondedAt.Should().Be(before);
    }

    [Fact]
    public void AnotherAccount_CannotChangeIt()
    {
        var invitation = AnsweredBy(Guid.NewGuid(), GameNightInvitationStatus.Accepted);

        var act = () => invitation.ChangeAnswer(GameNightInvitationStatus.Declined, Guid.NewGuid(), Now);

        act.Should().Throw<InvalidOperationException>();
        invitation.Status.Should().Be(GameNightInvitationStatus.Accepted);
    }

    [Fact]
    public void TheTokenPath_StillRefusesASwitch()
    {
        // The public link is a credential anyone holding it can replay: it must not flip an answer.
        var me = Guid.NewGuid();
        var invitation = AnsweredBy(me, GameNightInvitationStatus.Accepted);

        var act = () => invitation.Decline(me, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Cannot change response*");
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Cancelled)]
    [InlineData(GameNightInvitationStatus.Expired)]
    public void ATerminalInvitation_CannotBeAnsweredAgain(GameNightInvitationStatus terminal)
    {
        var me = Guid.NewGuid();
        var invitation = AnsweredBy(me, terminal);

        var act = () => invitation.ChangeAnswer(GameNightInvitationStatus.Accepted, me, Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OnlyAcceptedOrDeclined_AreAnswers()
    {
        var me = Guid.NewGuid();
        var invitation = AnsweredBy(me, GameNightInvitationStatus.Accepted);

        var act = () => invitation.ChangeAnswer(GameNightInvitationStatus.Pending, me, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
