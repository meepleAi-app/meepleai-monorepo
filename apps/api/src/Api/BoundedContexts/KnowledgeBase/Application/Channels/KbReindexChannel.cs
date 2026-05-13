using System.Threading.Channels;

namespace Api.BoundedContexts.KnowledgeBase.Application.Channels;

/// <summary>
/// Event written to <see cref="KbReindexChannel"/> when a reindex job is enqueued.
/// Carries everything the background consumer needs to do the work — no DB read on the hot path.
/// </summary>
internal sealed record KbReindexJobRequest(Guid JobId, Guid GameId, Guid UserId);

/// <summary>
/// Unbounded in-process channel that decouples the HTTP POST handler (writer) from
/// the background reindex worker (single reader). Singleton in DI.
///
/// Issue #941 / ADR-057. Pattern mirrors
/// <see cref="Api.BoundedContexts.Authentication.Infrastructure.Services.GameSuggestionChannel"/>.
/// </summary>
internal sealed class KbReindexChannel
{
    private readonly Channel<KbReindexJobRequest> _channel;

    public KbReindexChannel()
    {
        _channel = Channel.CreateUnbounded<KbReindexJobRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelWriter<KbReindexJobRequest> Writer => _channel.Writer;
    public ChannelReader<KbReindexJobRequest> Reader => _channel.Reader;
}
