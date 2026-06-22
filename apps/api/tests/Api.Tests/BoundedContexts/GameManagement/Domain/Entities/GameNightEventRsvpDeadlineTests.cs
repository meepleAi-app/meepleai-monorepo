using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

[Trait("Category", "Unit")]
public class GameNightEventRsvpDeadlineTests
{
    private static GameNightEvent CreateEvent() => GameNightEvent.Create(
        organizerId: Guid.NewGuid(),
        title: "Test Game Night",
        scheduledAt: DateTimeOffset.UtcNow.AddDays(7));

    [Fact]
    public void DefaultEvent_NoRsvpDeadlineNorClosure()
    {
        var evt = CreateEvent();

        evt.RsvpDeadline.Should().BeNull();
        evt.RsvpClosedAt.Should().BeNull();
        evt.IsRsvpClosed(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void SetRsvpDeadline_FutureDate_Persists()
    {
        var evt = CreateEvent();
        var deadline = DateTimeOffset.UtcNow.AddDays(3);

        evt.SetRsvpDeadline(deadline);

        evt.RsvpDeadline.Should().Be(deadline);
        evt.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetRsvpDeadline_PastDate_Throws()
    {
        var evt = CreateEvent();
        var pastDeadline = DateTimeOffset.UtcNow.AddDays(-1);

        var act = () => evt.SetRsvpDeadline(pastDeadline);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be in the future*");
    }

    [Fact]
    public void SetRsvpDeadline_Null_ClearsDeadline()
    {
        var evt = CreateEvent();
        evt.SetRsvpDeadline(DateTimeOffset.UtcNow.AddDays(3));

        evt.SetRsvpDeadline(null);

        evt.RsvpDeadline.Should().BeNull();
    }

    [Fact]
    public void SetRsvpDeadline_AfterClosure_Throws()
    {
        var evt = CreateEvent();
        evt.MarkRsvpClosed();

        var act = () => evt.SetRsvpDeadline(DateTimeOffset.UtcNow.AddDays(3));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot change RSVP deadline after RSVP has been closed*");
    }

    [Fact]
    public void MarkRsvpClosed_SetsTimestamp()
    {
        var evt = CreateEvent();
        var beforeClose = DateTimeOffset.UtcNow;

        evt.MarkRsvpClosed();

        evt.RsvpClosedAt.Should().NotBeNull();
        evt.RsvpClosedAt!.Value.Should().BeOnOrAfter(beforeClose);
    }

    [Fact]
    public void MarkRsvpClosed_Idempotent_DoesNotOverwriteTimestamp()
    {
        var evt = CreateEvent();
        evt.MarkRsvpClosed();
        var firstClose = evt.RsvpClosedAt!.Value;

        // Wait a tiny bit so a second call would observably differ
        Thread.Sleep(10);
        evt.MarkRsvpClosed();

        evt.RsvpClosedAt!.Value.Should().Be(firstClose);
    }

    [Fact]
    public void IsRsvpClosed_DeadlineFuture_ReturnsFalse()
    {
        var evt = CreateEvent();
        evt.SetRsvpDeadline(DateTimeOffset.UtcNow.AddDays(3));

        evt.IsRsvpClosed(DateTimeOffset.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsRsvpClosed_DeadlinePastButNotMarkedClosed_ReturnsTrue()
    {
        var evt = CreateEvent();
        var deadline = DateTimeOffset.UtcNow.AddHours(1);
        evt.SetRsvpDeadline(deadline);

        // Caller passes a "now" past the deadline (validator-time check)
        evt.IsRsvpClosed(deadline.AddMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void IsRsvpClosed_ExplicitClosure_ReturnsTrueRegardlessOfDeadline()
    {
        var evt = CreateEvent();
        evt.SetRsvpDeadline(DateTimeOffset.UtcNow.AddDays(7));
        evt.MarkRsvpClosed();

        evt.IsRsvpClosed(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsRsvpClosed_DeadlineExactlyAtNow_ReturnsTrue()
    {
        var evt = CreateEvent();
        var deadline = DateTimeOffset.UtcNow.AddHours(1);
        evt.SetRsvpDeadline(deadline);

        // utcNow == deadline → inclusive close
        evt.IsRsvpClosed(deadline).Should().BeTrue();
    }
}
