using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Unit tests for the candidate-game approval-voting domain behaviour on
/// <see cref="GameNightEvent"/> — Issue #2700 (Umbrella #2697, spec §4a US-INT-3 step 4).
///
/// Model: each confirmed participant (Accepted RSVP) approves any subset of the event's
/// candidate games (1 vote per candidate, toggle). Voting closes 1h before ScheduledAt
/// (lazy check, ADR-074 Option C philosophy). On a tie at close, the organiser resolves it.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Feature", "GameNightVoting")]
public class GameNightEventVotingTests
{
    private static readonly DateTimeOffset ScheduledAt = new(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Open = ScheduledAt.AddHours(-3);   // > 1h before → open
    private static readonly DateTimeOffset Closed = ScheduledAt.AddMinutes(-30); // < 1h before → closed

    private static (GameNightEvent Evt, Guid Voter, Guid GameA, Guid GameB, Guid Organizer)
        BuildPublishedEventWithConfirmedVoter()
    {
        var organizer = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var gameA = Guid.NewGuid();
        var gameB = Guid.NewGuid();
        var evt = GameNightEvent.Create(
            organizer, "Serata voti", ScheduledAt, gameIds: new List<Guid> { gameA, gameB });
        evt.Publish(new List<Guid> { voter });
        evt.GetRsvp(voter)!.Accept();
        return (evt, voter, gameA, gameB, organizer);
    }

    [Fact]
    public void CastVote_ByConfirmedParticipant_RecordsVote()
    {
        var (evt, voter, gameA, _, _) = BuildPublishedEventWithConfirmedVoter();

        evt.CastVote(voter, gameA, Open);

        evt.Votes.Should().ContainSingle(v => v.VoterUserId == voter && v.CandidateGameId == gameA);
        evt.TallyVotes().CountsByCandidate[gameA].Should().Be(1);
    }

    [Fact]
    public void CastVote_IsIdempotent_ForSameVoterAndCandidate()
    {
        var (evt, voter, gameA, _, _) = BuildPublishedEventWithConfirmedVoter();

        evt.CastVote(voter, gameA, Open);
        evt.CastVote(voter, gameA, Open);

        evt.TallyVotes().CountsByCandidate[gameA].Should().Be(1);
    }

    [Fact]
    public void CastVote_ApprovalAllowsMultipleCandidates()
    {
        var (evt, voter, gameA, gameB, _) = BuildPublishedEventWithConfirmedVoter();

        evt.CastVote(voter, gameA, Open);
        evt.CastVote(voter, gameB, Open);

        var tally = evt.TallyVotes();
        tally.CountsByCandidate[gameA].Should().Be(1);
        tally.CountsByCandidate[gameB].Should().Be(1);
    }

    [Fact]
    public void CastVote_ByNonConfirmedUser_ThrowsForbidden()
    {
        var (evt, _, gameA, _, _) = BuildPublishedEventWithConfirmedVoter();
        var pendingUser = Guid.NewGuid();
        evt.AddInvitees(new List<Guid> { pendingUser }); // Pending, not Accepted

        var act = () => evt.CastVote(pendingUser, gameA, Open);

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void CastVote_ByStranger_ThrowsForbidden()
    {
        var (evt, _, gameA, _, _) = BuildPublishedEventWithConfirmedVoter();

        var act = () => evt.CastVote(Guid.NewGuid(), gameA, Open);

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void CastVote_ForNonCandidateGame_ThrowsNotFound()
    {
        var (evt, voter, _, _, _) = BuildPublishedEventWithConfirmedVoter();

        var act = () => evt.CastVote(voter, Guid.NewGuid(), Open);

        act.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void CastVote_WhenVotingClosed_ThrowsConflict()
    {
        var (evt, voter, gameA, _, _) = BuildPublishedEventWithConfirmedVoter();

        var act = () => evt.CastVote(voter, gameA, Closed);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void CastVote_WhenNotPublished_ThrowsConflict()
    {
        var organizer = Guid.NewGuid();
        var gameA = Guid.NewGuid();
        var evt = GameNightEvent.Create(organizer, "Bozza", ScheduledAt, gameIds: new List<Guid> { gameA });
        // still Draft — no confirmed voters possible, but the status guard must fire first

        var act = () => evt.CastVote(organizer, gameA, Open);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void IsVotingClosed_TrueWithinOneHour_FalseEarlier()
    {
        var (evt, _, _, _, _) = BuildPublishedEventWithConfirmedVoter();

        evt.IsVotingClosed(Open).Should().BeFalse();
        evt.IsVotingClosed(Closed).Should().BeTrue();
    }

    [Fact]
    public void RetractVote_RemovesVote_AndIsIdempotent()
    {
        var (evt, voter, gameA, _, _) = BuildPublishedEventWithConfirmedVoter();
        evt.CastVote(voter, gameA, Open);

        evt.RetractVote(voter, gameA, Open);
        evt.RetractVote(voter, gameA, Open); // idempotent

        evt.Votes.Should().BeEmpty();
        evt.TallyVotes().CountsByCandidate[gameA].Should().Be(0);
    }

    [Fact]
    public void TallyVotes_SingleLeader_DerivesWinner_NoTie()
    {
        var (evt, voter, gameA, gameB, organizer) = BuildPublishedEventWithConfirmedVoter();
        evt.AddInvitees(new List<Guid> { organizer });
        evt.GetRsvp(organizer)!.Accept();

        evt.CastVote(voter, gameA, Open);
        evt.CastVote(organizer, gameA, Open); // gameA: 2, gameB: 0

        var tally = evt.TallyVotes();
        tally.IsTie.Should().BeFalse();
        tally.LeadingCandidateGameIds.Should().ContainSingle().Which.Should().Be(gameA);
        tally.WinnerGameId.Should().Be(gameA);
    }

    [Fact]
    public void TallyVotes_EqualCandidates_IsTie_NoWinnerUntilResolved()
    {
        var (evt, voter, gameA, gameB, organizer) = BuildPublishedEventWithConfirmedVoter();
        evt.AddInvitees(new List<Guid> { organizer });
        evt.GetRsvp(organizer)!.Accept();

        evt.CastVote(voter, gameA, Open);
        evt.CastVote(organizer, gameB, Open); // gameA: 1, gameB: 1 → tie

        var tally = evt.TallyVotes();
        tally.IsTie.Should().BeTrue();
        tally.LeadingCandidateGameIds.Should().BeEquivalentTo(new[] { gameA, gameB });
        tally.WinnerGameId.Should().BeNull();
    }

    [Fact]
    public void ResolveVotingTie_ByHost_AfterClose_SetsWinner()
    {
        var (evt, voter, gameA, gameB, organizer) = BuildPublishedEventWithConfirmedVoter();
        evt.AddInvitees(new List<Guid> { organizer });
        evt.GetRsvp(organizer)!.Accept();
        evt.CastVote(voter, gameA, Open);
        evt.CastVote(organizer, gameB, Open); // tie

        evt.ResolveVotingTie(organizer, gameA, Closed);

        evt.VotingWinnerGameId.Should().Be(gameA);
        evt.TallyVotes().WinnerGameId.Should().Be(gameA);
    }

    [Fact]
    public void ResolveVotingTie_ByNonHost_ThrowsForbidden()
    {
        var (evt, voter, gameA, gameB, organizer) = BuildPublishedEventWithConfirmedVoter();
        evt.AddInvitees(new List<Guid> { organizer });
        evt.GetRsvp(organizer)!.Accept();
        evt.CastVote(voter, gameA, Open);
        evt.CastVote(organizer, gameB, Open);

        var act = () => evt.ResolveVotingTie(voter, gameA, Closed);

        act.Should().Throw<ForbiddenException>();
    }

    [Fact]
    public void ResolveVotingTie_WhenVotingStillOpen_ThrowsConflict()
    {
        var (evt, voter, gameA, gameB, organizer) = BuildPublishedEventWithConfirmedVoter();
        evt.AddInvitees(new List<Guid> { organizer });
        evt.GetRsvp(organizer)!.Accept();
        evt.CastVote(voter, gameA, Open);
        evt.CastVote(organizer, gameB, Open);

        var act = () => evt.ResolveVotingTie(organizer, gameA, Open);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void ResolveVotingTie_WhenNoTie_ThrowsConflict()
    {
        var (evt, voter, gameA, _, organizer) = BuildPublishedEventWithConfirmedVoter();
        evt.CastVote(voter, gameA, Open); // single leader, no tie

        var act = () => evt.ResolveVotingTie(organizer, gameA, Closed);

        act.Should().Throw<ConflictException>();
    }
}
