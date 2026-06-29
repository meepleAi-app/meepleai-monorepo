using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveGameSession_DiaryTests
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly DateTimeOffset _now;

    private static readonly Guid AuthorA = Guid.NewGuid();
    private static readonly Guid AuthorB = Guid.NewGuid();

    public LiveGameSession_DiaryTests()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 6, 29, 10, 0, 0, TimeSpan.Zero));
        _now = _timeProvider.GetUtcNow();
    }

    /// <summary>Creates a session in InProgress state.</summary>
    private LiveGameSession CreateInProgressSession()
    {
        var session = LiveGameSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Mage Knight",
            _timeProvider);

        session.AddPlayer(null, "Alice", PlayerColor.Red, _timeProvider);
        session.Start(_timeProvider);

        return session;
    }

    /// <summary>Creates a session in Paused state.</summary>
    private LiveGameSession CreatePausedSession()
    {
        var session = CreateInProgressSession();
        session.Pause(_timeProvider);
        return session;
    }

    /// <summary>Creates a session in Completed state.</summary>
    private LiveGameSession CreateCompletedSession()
    {
        var session = CreateInProgressSession();
        session.Complete(_timeProvider);
        return session;
    }

    #region AddDiaryEntry — happy path

    [Fact]
    public void AddDiaryEntry_appends_and_raises_event()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Act
        session.AddDiaryEntry(AuthorA, "first", _timeProvider);
        session.AddDiaryEntry(AuthorB, "second", _timeProvider);

        // Assert — order preserved
        session.DiaryEntries.Should().HaveCount(2);
        session.DiaryEntries.Select(e => e.Text).Should().Equal("first", "second");

        // Assert — both entries raise the event
        var events = session.DomainEvents.OfType<LiveSessionDiaryEntryAddedEvent>().ToList();
        events.Should().HaveCount(2);
    }

    [Fact]
    public void AddDiaryEntry_entry_has_correct_properties()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Act
        session.AddDiaryEntry(AuthorA, "  my note  ", _timeProvider);

        // Assert
        var entry = session.DiaryEntries.Single();
        entry.Id.Should().NotBe(Guid.Empty);
        entry.AuthorId.Should().Be(AuthorA);
        entry.Text.Should().Be("my note"); // trimmed
        entry.CreatedAt.Should().Be(_now);
    }

    [Fact]
    public void AddDiaryEntry_first_entry_is_immutable_after_second_append()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Act
        session.AddDiaryEntry(AuthorA, "first", _timeProvider);
        var firstEntryId = session.DiaryEntries[0].Id;
        session.AddDiaryEntry(AuthorB, "second", _timeProvider);

        // Assert — first entry unchanged
        session.DiaryEntries[0].Id.Should().Be(firstEntryId);
        session.DiaryEntries[0].Text.Should().Be("first");
    }

    [Fact]
    public void AddDiaryEntry_event_carries_correct_payload()
    {
        // Arrange
        var session = CreateInProgressSession();

        // Act
        session.AddDiaryEntry(AuthorA, "hello world", _timeProvider);

        // Assert
        var evt = session.DomainEvents.OfType<LiveSessionDiaryEntryAddedEvent>().Single();
        evt.SessionId.Should().Be(session.Id);
        evt.AuthorId.Should().Be(AuthorA);
        evt.Text.Should().Be("hello world");
        evt.EntryId.Should().Be(session.DiaryEntries[0].Id);
        evt.CreatedAt.Should().Be(_now);
    }

    [Fact]
    public void AddDiaryEntry_on_paused_session_succeeds_per_AC_DIARY_3()
    {
        // Arrange — AC-DIARY-3: Paused is allowed, only Completed is rejected
        var session = CreatePausedSession();

        // Act
        var act = () => session.AddDiaryEntry(AuthorA, "note during pause", _timeProvider);

        // Assert
        act.Should().NotThrow();
        session.DiaryEntries.Should().HaveCount(1);
    }

    #endregion

    #region AddDiaryEntry — 409 guard

    [Fact]
    public void AddDiaryEntry_on_completed_session_throws_conflict()
    {
        // Arrange
        var session = CreateCompletedSession();

        // Act
        var act = () => session.AddDiaryEntry(AuthorA, "too late", _timeProvider);

        // Assert
        act.Should().Throw<ConflictException>();
    }

    #endregion

    #region AddDiaryEntry — validation guards

    [Fact]
    public void AddDiaryEntry_empty_authorId_throws_validation()
    {
        var session = CreateInProgressSession();
        var act = () => session.AddDiaryEntry(Guid.Empty, "text", _timeProvider);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void AddDiaryEntry_empty_text_throws_validation()
    {
        var session = CreateInProgressSession();
        var act = () => session.AddDiaryEntry(AuthorA, "   ", _timeProvider);
        act.Should().Throw<ValidationException>();
    }

    #endregion

    #region Notes vs Diary separation (AC-DIARY-4 pin)

    [Fact]
    public void DiaryEntries_and_Notes_are_distinct_collections()
    {
        // Notes is a single string overwritten by host; DiaryEntries is append-only multi-author.
        var session = CreateInProgressSession();

        session.UpdateNotes("session notes", _timeProvider);
        session.AddDiaryEntry(AuthorA, "diary line 1", _timeProvider);

        session.Notes.Should().Be("session notes");
        session.DiaryEntries.Should().HaveCount(1);
        session.DiaryEntries[0].Text.Should().Be("diary line 1");
    }

    [Fact]
    public void AddDiaryEntry_does_not_affect_Notes()
    {
        var session = CreateInProgressSession();
        session.UpdateNotes("original notes", _timeProvider);

        session.AddDiaryEntry(AuthorA, "diary entry", _timeProvider);

        session.Notes.Should().Be("original notes");
    }

    #endregion

    #region Initial state

    [Fact]
    public void DiaryEntries_starts_empty()
    {
        var session = LiveGameSession.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Game",
            _timeProvider);

        session.DiaryEntries.Should().BeEmpty();
    }

    #endregion
}
