using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tests for RSVP deduplication in <see cref="GameNightEvent.Publish"/> (Issue #2835).
///
/// Regression guard: <c>Publish</c> must not create a second <c>GameNightRsvp</c> for a user
/// that was already pre-invited during Draft creation. The pre-invite RSVP and the publish RSVP
/// would otherwise both carry the same <c>(EventId, UserId)</c> unique-index key, which the
/// persistence layer round-trips into two rows with the same key in one SaveChanges →
/// EF Core <c>Multigraph.ThrowCycle</c> → HTTP 500. The dedup guard mirrors the one already
/// present in <see cref="GameNightEvent.PreInvite"/> and <see cref="GameNightEvent.AddInvitees"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightEventPublishRsvpTests
{
    private static GameNightEvent NewDraft() =>
        GameNightEvent.Create(
            organizerId: Guid.NewGuid(),
            title: "Test Night",
            scheduledAt: DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public void Publish_WhenUserAlreadyPreInvited_DoesNotCreateDuplicateRsvp()
    {
        // Arrange: a draft with one pre-invited user (RSVP #1 created by PreInvite)
        var userA = Guid.NewGuid();
        var night = NewDraft();
        night.PreInvite(new List<Guid> { userA });
        night.Rsvps.Should().ContainSingle("PreInvite creates exactly one RSVP for userA");

        // Act: publish passing the SAME pre-invited user (mirrors the handler, which reads
        // existingInvitedIds from the RSVPs and passes them to Publish)
        night.Publish(new List<Guid> { userA });

        // Assert: still exactly one RSVP — no duplicate for the already-invited user
        night.Rsvps.Should().ContainSingle("Publish must not add a second RSVP for an already-invited user");
        night.Rsvps.Single().UserId.Should().Be(userA);
        night.Status.Should().Be(GameNightStatus.Published);

        // Exactly one GameNightPublishedEvent is raised (pre-invited users must still be notified)
        night.DomainEvents.OfType<GameNightPublishedEvent>().Should().ContainSingle();
        night.DomainEvents.OfType<GameNightPublishedEvent>().Single()
            .InvitedUserIds.Should().Contain(userA, "pre-invited users must still be notified on publish");
    }

    [Fact]
    public void Publish_WithNewUserHavingNoPriorRsvp_CreatesExactlyOneRsvp()
    {
        // Arrange: a draft with no pre-invites
        var userA = Guid.NewGuid();
        var night = NewDraft();
        night.Rsvps.Should().BeEmpty();

        // Act
        night.Publish(new List<Guid> { userA });

        // Assert: exactly one RSVP created for the freshly invited user
        night.Rsvps.Should().ContainSingle();
        night.Rsvps.Single().UserId.Should().Be(userA);
        night.Status.Should().Be(GameNightStatus.Published);
        night.DomainEvents.OfType<GameNightPublishedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Publish_WithMixOfPreInvitedAndNewUsers_CreatesOneRsvpPerUser()
    {
        // Arrange: userA pre-invited; userB is new at publish time
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var night = NewDraft();
        night.PreInvite(new List<Guid> { userA });

        // Act: publish with both — userA (already invited) and userB (new)
        night.Publish(new List<Guid> { userA, userB });

        // Assert: exactly two RSVPs, one per distinct user, no duplicate for userA
        night.Rsvps.Should().HaveCount(2);
        night.Rsvps.Select(r => r.UserId).Should().BeEquivalentTo(new[] { userA, userB });
        night.Status.Should().Be(GameNightStatus.Published);
        night.DomainEvents.OfType<GameNightPublishedEvent>().Should().ContainSingle();
    }
}
