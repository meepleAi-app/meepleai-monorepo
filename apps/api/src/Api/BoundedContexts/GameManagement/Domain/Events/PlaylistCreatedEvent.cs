using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game night playlist is created.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal sealed class PlaylistCreatedEvent : DomainEventBase
{
    public Guid PlaylistId { get; }
    public string Name { get; }
    public Guid CreatorUserId { get; }

    public PlaylistCreatedEvent(Guid playlistId, string name, Guid creatorUserId)
    {
        PlaylistId = playlistId;
        Name = name;
        CreatorUserId = creatorUserId;
    }
}
