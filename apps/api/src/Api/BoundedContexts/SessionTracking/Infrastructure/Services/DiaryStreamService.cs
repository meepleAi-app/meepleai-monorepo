using System.Collections.Concurrent;
using System.Threading.Channels;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// In-process Channel-based pub/sub for SSE diary streaming.
/// Bounded channel (capacity 100, drop-oldest) per session; ref-counted
/// subscribers trigger channel cleanup on last disconnect.
/// </summary>
internal sealed class DiaryStreamService : IDiaryStreamService
{
    private readonly ConcurrentDictionary<Guid, Channel<SessionEventDto>> _channels = new();
    private readonly ConcurrentDictionary<Guid, int> _subscriberCounts = new();

    private static readonly BoundedChannelOptions ChannelOptions = new(capacity: 100)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleWriter = false,
        SingleReader = false
    };

    public void Publish(Guid sessionId, SessionEventDto entry)
    {
        var channel = _channels.GetOrAdd(sessionId, _ => Channel.CreateBounded<SessionEventDto>(ChannelOptions));
        channel.Writer.TryWrite(entry);
    }

    public ChannelReader<SessionEventDto> Subscribe(Guid sessionId)
    {
        var channel = _channels.GetOrAdd(sessionId, _ => Channel.CreateBounded<SessionEventDto>(ChannelOptions));
        _subscriberCounts.AddOrUpdate(sessionId, 1, (_, count) => count + 1);
        return channel.Reader;
    }

    public void Unsubscribe(Guid sessionId)
    {
        if (!_subscriberCounts.TryGetValue(sessionId, out var count) || count <= 1)
        {
            _subscriberCounts.TryRemove(sessionId, out _);
            if (_channels.TryRemove(sessionId, out var channel))
                channel.Writer.TryComplete();
        }
        else
        {
            _subscriberCounts[sessionId] = count - 1;
        }
    }
}
