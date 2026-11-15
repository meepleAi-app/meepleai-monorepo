using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.AgentMemory.Domain;

/// <summary>
/// #4839 option C — the sessions already counted in a group's stats. The older guard remembers one event; this one
/// remembers the sessions, so a redelivery is recognised whatever came in between.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "AgentMemory")]
[Trait("Issue", "4839")]
public sealed class ProcessedSessionsTests
{
    [Fact]
    public void Contains_ASessionThatWasAdded_IsTrue_AndAnotherIsFalse()
    {
        var sessions = new ProcessedSessions();
        var counted = Guid.NewGuid();

        sessions.Add(counted);

        sessions.Contains(counted).Should().BeTrue();
        sessions.Contains(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Add_TheSameSessionTwice_KeepsItOnce()
    {
        var sessions = new ProcessedSessions();
        var counted = Guid.NewGuid();

        sessions.Add(counted);
        sessions.Add(counted);

        sessions.Ids.Should().ContainSingle().Which.Should().Be(counted);
    }

    [Fact]
    public void Add_PastTheCapacity_ForgetsTheOldest()
    {
        var sessions = new ProcessedSessions();
        var ids = Enumerable.Range(0, ProcessedSessions.Capacity + 3).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
            sessions.Add(id);

        sessions.Ids.Should().HaveCount(ProcessedSessions.Capacity);
        sessions.Ids.Should().Equal(ids.Skip(3), "the most recent ones stay, oldest first");
        sessions.Contains(ids[0]).Should().BeFalse();
        sessions.Contains(ids[^1]).Should().BeTrue();
    }

    [Fact]
    public void Constructor_DropsDuplicatesAndEmptyIds_AndKeepsTheMostRecent()
    {
        var kept = Enumerable.Range(0, ProcessedSessions.Capacity).Select(_ => Guid.NewGuid()).ToList();
        var stored = new[] { Guid.Empty, kept[0], kept[0] }.Concat(kept).Prepend(Guid.NewGuid()).ToList();

        var sessions = new ProcessedSessions(stored);

        sessions.Ids.Should().Equal(kept, "the empty id and the repeats are dropped, and the oldest of the rest is the one that goes");
    }

    [Fact]
    public void Add_AnEmptySessionId_Throws()
    {
        var act = () => new ProcessedSessions().Add(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    // ── The aggregate ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GroupMemory_MarkProcessed_RemembersTheEventAndTheSession()
    {
        var group = GroupMemory.Create(Guid.NewGuid(), "Giovedi");
        var eventId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        group.MarkProcessed(eventId, sessionId);
        group.MarkProcessed(Guid.NewGuid(), Guid.NewGuid());

        group.LastProcessedEventId.Should().NotBe(eventId, "the last event moved on");
        group.HasProcessedSession(sessionId).Should().BeTrue("the session is still remembered");
    }

    [Fact]
    public void GroupMemory_RestoreProcessedSessions_PutsTheSessionsBack()
    {
        var group = GroupMemory.Create(Guid.NewGuid(), "Giovedi");
        var sessionId = Guid.NewGuid();

        group.RestoreProcessedSessions([sessionId]);

        group.HasProcessedSession(sessionId).Should().BeTrue();
    }
}
