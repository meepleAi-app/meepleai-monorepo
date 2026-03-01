using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Services;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Services;

/// <summary>
/// In-memory SSE broadcaster using System.Threading.Channels for backpressure-aware delivery.
/// Registered as singleton: survives the lifetime of the application.
/// Each connected SSE client gets its own bounded channel (capacity 50, drop-oldest policy).
/// Issue #5005: Backend SSE stream endpoint /api/v1/notifications/stream.
/// </summary>
internal sealed class InMemoryUserNotificationBroadcaster : IUserNotificationBroadcaster
{
    // userId → { subscriptionId → channel }
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<NotificationDto>>> _userChannels = new();

    /// <inheritdoc/>
    public void Publish(Guid userId, NotificationDto notification)
    {
        if (!_userChannels.TryGetValue(userId, out var subscriptions))
            return;

        foreach (var (_, channel) in subscriptions)
            channel.Writer.TryWrite(notification);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<NotificationDto> SubscribeAsync(
        Guid userId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var subscriptionId = Guid.NewGuid();
        var channel = Channel.CreateBounded<NotificationDto>(
            new BoundedChannelOptions(50)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        var subscriptions = _userChannels.GetOrAdd(userId,
            _ => new ConcurrentDictionary<Guid, Channel<NotificationDto>>());
        subscriptions[subscriptionId] = channel;

        try
        {
            await foreach (var notification in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                yield return notification;
        }
        finally
        {
            subscriptions.TryRemove(subscriptionId, out _);
            if (subscriptions.IsEmpty)
                _userChannels.TryRemove(userId, out _);
            channel.Writer.TryComplete();
        }
    }
}
