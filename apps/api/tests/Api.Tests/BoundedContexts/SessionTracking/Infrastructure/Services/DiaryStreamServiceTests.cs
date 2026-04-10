using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="DiaryStreamService"/>: Channel-based pub/sub
/// for SSE diary event streaming (Issue #376).
/// </summary>
public sealed class DiaryStreamServiceTests
{
    private static SessionEventDto MakeEntry(string id, Guid sessionId, string eventType = "test") =>
        new(
            Guid.Parse(id.PadLeft(32, '0')),
            sessionId,
            null,
            eventType,
            DateTime.UtcNow,
            null,
            null,
            "test");

    [Fact]
    public async Task Publish_Subscribe_ReceivesEvents()
    {
        // Arrange
        var sut = new DiaryStreamService();
        var sessionId = Guid.NewGuid();
        var reader = sut.Subscribe(sessionId);

        var e1 = MakeEntry("a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1", sessionId, "turn_advanced");
        var e2 = MakeEntry("b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2", sessionId, "dice_rolled");

        // Act
        sut.Publish(sessionId, e1);
        sut.Publish(sessionId, e2);

        // Assert
        var received = new List<SessionEventDto>();
        reader.TryRead(out var r1).Should().BeTrue();
        received.Add(r1!);
        reader.TryRead(out var r2).Should().BeTrue();
        received.Add(r2!);

        received.Should().HaveCount(2);
        received[0].EventType.Should().Be("turn_advanced");
        received[1].EventType.Should().Be("dice_rolled");
    }

    [Fact]
    public void Publish_ToDifferentSession_NotReceived()
    {
        // Arrange
        var sut = new DiaryStreamService();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        var reader = sut.Subscribe(sessionB);

        // Act
        sut.Publish(sessionA, MakeEntry("a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1", sessionA));

        // Assert
        reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task Publish_BeyondCapacity_DropsOldest()
    {
        // Arrange
        var sut = new DiaryStreamService();
        var sessionId = Guid.NewGuid();
        var reader = sut.Subscribe(sessionId);

        // Act — publish 150 events into a channel with capacity 100
        for (var i = 0; i < 150; i++)
        {
            var id = Guid.NewGuid();
            sut.Publish(sessionId, new SessionEventDto(
                id, sessionId, null, $"evt-{i}", DateTime.UtcNow, null, null, "test"));
        }

        // Assert — only the last 100 remain (drop-oldest)
        var received = new List<SessionEventDto>();
        while (reader.TryRead(out var item))
        {
            received.Add(item);
        }

        received.Should().HaveCount(100);
        received[0].EventType.Should().Be("evt-50");
    }

    [Fact]
    public async Task Unsubscribe_LastSubscriber_CompletesReader()
    {
        // Arrange
        var sut = new DiaryStreamService();
        var sessionId = Guid.NewGuid();
        var reader = sut.Subscribe(sessionId);

        // Act
        sut.Unsubscribe(sessionId);

        // Assert — Completion should be completed (channel writer completed)
        var completionTask = reader.Completion;
        completionTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void Unsubscribe_WithOtherSubscribers_KeepsChannelAlive()
    {
        // Arrange
        var sut = new DiaryStreamService();
        var sessionId = Guid.NewGuid();
        var reader1 = sut.Subscribe(sessionId);
        var reader2 = sut.Subscribe(sessionId);

        // Act — first subscriber leaves
        sut.Unsubscribe(sessionId);

        // second subscriber should still receive messages
        var entry = MakeEntry("c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3c3", sessionId, "session_paused");
        sut.Publish(sessionId, entry);

        // Assert — channel is still alive, both readers share the same channel
        // so at least one TryRead should succeed
        var received = reader1.TryRead(out var r1) || reader2.TryRead(out r1);
        received.Should().BeTrue();
        r1!.EventType.Should().Be("session_paused");
    }
}
