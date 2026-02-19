using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentAssertions;
using MediatR;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class SseEventTypeMapperTests
{
    #region Session Lifecycle Events → session:state

    [Theory]
    [InlineData(typeof(SessionCreatedEvent))]
    [InlineData(typeof(SessionPausedEvent))]
    [InlineData(typeof(SessionResumedEvent))]
    [InlineData(typeof(SessionFinalizedEvent))]
    public void GetEventType_SessionLifecycleEvents_ReturnsSessionState(Type eventType)
    {
        // Act
        var result = SseEventTypeMapper.GetEventType(eventType);

        // Assert
        result.Should().Be("session:state");
    }

    #endregion

    #region Score Events → session:score

    [Fact]
    public void GetEventType_ScoreUpdatedEvent_ReturnsSessionScore()
    {
        // Act
        var result = SseEventTypeMapper.GetEventType<ScoreUpdatedEvent>();

        // Assert
        result.Should().Be("session:score");
    }

    #endregion

    #region Player Events → session:player

    [Fact]
    public void GetEventType_ParticipantAddedEvent_ReturnsSessionPlayer()
    {
        // Act
        var result = SseEventTypeMapper.GetEventType<ParticipantAddedEvent>();

        // Assert
        result.Should().Be("session:player");
    }

    #endregion

    #region Toolkit Events → session:toolkit

    [Theory]
    [InlineData(typeof(DiceRolledEvent))]
    [InlineData(typeof(CardsDrawnEvent))]
    [InlineData(typeof(CardsDiscardedEvent))]
    [InlineData(typeof(CardsRevealedEvent))]
    [InlineData(typeof(DeckShuffledEvent))]
    [InlineData(typeof(DeckCreatedEvent))]
    [InlineData(typeof(DeckResetEvent))]
    [InlineData(typeof(NoteSavedEvent))]
    [InlineData(typeof(NoteRevealedEvent))]
    [InlineData(typeof(NoteHiddenEvent))]
    [InlineData(typeof(NoteUpdatedEvent))]
    [InlineData(typeof(NoteDeletedEvent))]
    [InlineData(typeof(SessionMediaUploadedEvent))]
    [InlineData(typeof(SessionMediaDeletedEvent))]
    public void GetEventType_ToolkitEvents_ReturnsSessionToolkit(Type eventType)
    {
        // Act
        var result = SseEventTypeMapper.GetEventType(eventType);

        // Assert
        result.Should().Be("session:toolkit");
    }

    #endregion

    #region Chat Events → session:chat

    [Fact]
    public void GetEventType_SessionChatMessageSentEvent_ReturnsSessionChat()
    {
        // Act
        var result = SseEventTypeMapper.GetEventType<SessionChatMessageSentEvent>();

        // Assert
        result.Should().Be("session:chat");
    }

    #endregion

    #region Unmapped Events → session:state (fallback)

    [Fact]
    public void GetEventType_UnmappedEvent_ReturnsFallbackSessionState()
    {
        // Act
        var result = SseEventTypeMapper.GetEventType(typeof(TestUnmappedEvent));

        // Assert
        result.Should().Be("session:state");
    }

    #endregion

    #region Instance-based overload

    [Fact]
    public void GetEventType_WithInstance_ReturnsCorrectType()
    {
        // Arrange
        INotification evt = new ScoreUpdatedEvent
        {
            SessionId = Guid.NewGuid(),
            ParticipantId = Guid.NewGuid(),
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 10
        };

        // Act
        var result = SseEventTypeMapper.GetEventType(evt);

        // Assert
        result.Should().Be("session:score");
    }

    #endregion

    #region All mapped events coverage

    [Fact]
    public void GetEventType_AllMappedEvents_AreNonEmpty()
    {
        // All known event types should map to non-empty strings
        var allTypes = new[]
        {
            typeof(SessionCreatedEvent), typeof(SessionPausedEvent),
            typeof(SessionResumedEvent), typeof(SessionFinalizedEvent),
            typeof(ScoreUpdatedEvent), typeof(ParticipantAddedEvent),
            typeof(DiceRolledEvent), typeof(CardsDrawnEvent),
            typeof(CardsDiscardedEvent), typeof(CardsRevealedEvent),
            typeof(DeckShuffledEvent), typeof(DeckCreatedEvent),
            typeof(DeckResetEvent), typeof(SessionChatMessageSentEvent),
            typeof(NoteSavedEvent), typeof(NoteRevealedEvent),
            typeof(NoteHiddenEvent), typeof(NoteUpdatedEvent),
            typeof(NoteDeletedEvent), typeof(SessionMediaUploadedEvent),
            typeof(SessionMediaDeletedEvent)
        };

        foreach (var type in allTypes)
        {
            var result = SseEventTypeMapper.GetEventType(type);
            result.Should().NotBeNullOrEmpty($"event type {type.Name} should be mapped");
            result.Should().StartWith("session:", $"event type {type.Name} should follow naming convention");
        }
    }

    #endregion

    private record TestUnmappedEvent : INotification;
}
