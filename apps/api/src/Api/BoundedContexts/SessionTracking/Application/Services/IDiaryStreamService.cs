using System.Threading.Channels;
using Api.BoundedContexts.SessionTracking.Application.DTOs;

namespace Api.BoundedContexts.SessionTracking.Application.Services;

/// <summary>
/// Channel-based in-process pub/sub for real-time diary event streaming.
/// Singleton: one bounded channel per active session, cleaned up on last
/// subscriber disconnect.
/// </summary>
public interface IDiaryStreamService
{
    /// <summary>Publish a diary event to all subscribers of the given session.</summary>
    void Publish(Guid sessionId, SessionEventDto entry);

    /// <summary>Subscribe to the diary stream for a session. Caller owns the reader lifetime.</summary>
    ChannelReader<SessionEventDto> Subscribe(Guid sessionId);

    /// <summary>Decrement subscriber count; complete the channel when the last subscriber leaves.</summary>
    void Unsubscribe(Guid sessionId);
}
