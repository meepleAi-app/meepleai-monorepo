using System.Threading.Channels;
using Api.BoundedContexts.Authentication.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Infrastructure.Services;

/// <summary>
/// Event dispatched when an invited user activates their account and the invitation
/// carried game suggestions. Consumed by GameSuggestionProcessorService (Task 9).
/// </summary>
internal sealed record GameSuggestionEvent(
    Guid UserId,
    Guid InvitedByUserId,
    IReadOnlyList<InvitationGameSuggestion> Suggestions);

/// <summary>
/// Unbounded Channel for decoupling invitation activation from game suggestion processing.
/// Single reader (background service) / multiple writers (activation handlers).
/// Registered as singleton in DI.
/// </summary>
internal sealed class GameSuggestionChannel
{
    private readonly Channel<GameSuggestionEvent> _channel;

    public GameSuggestionChannel()
    {
        _channel = Channel.CreateUnbounded<GameSuggestionEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelWriter<GameSuggestionEvent> Writer => _channel.Writer;
    public ChannelReader<GameSuggestionEvent> Reader => _channel.Reader;
}
