using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Infrastructure.Entities.SessionTracking;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

/// <summary>
/// Maps between SessionMedia domain entity and SessionMediaEntity persistence entity.
/// Issue #4760
/// </summary>
internal static class SessionMediaMapper
{
    public static SessionMediaEntity ToEntity(SessionMedia domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return new SessionMediaEntity
        {
            Id = domain.Id,
            SessionId = domain.SessionId,
            ParticipantId = domain.ParticipantId,
            SnapshotId = domain.SnapshotId,
            FileId = domain.FileId,
            FileName = domain.FileName,
            ContentType = domain.ContentType,
            FileSizeBytes = domain.FileSizeBytes,
            MediaType = domain.MediaType.ToString(),
            Caption = domain.Caption,
            ThumbnailFileId = domain.ThumbnailFileId,
            TurnNumber = domain.TurnNumber,
            IsSharedWithSession = domain.IsSharedWithSession,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            IsDeleted = domain.IsDeleted,
            DeletedAt = domain.DeletedAt,
        };
    }

    public static SessionMedia ToDomain(SessionMediaEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var media = (SessionMedia)Activator.CreateInstance(typeof(SessionMedia), true)!;

        typeof(SessionMedia).GetProperty(nameof(SessionMedia.Id))!.SetValue(media, entity.Id);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.SessionId))!.SetValue(media, entity.SessionId);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.ParticipantId))!.SetValue(media, entity.ParticipantId);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.SnapshotId))!.SetValue(media, entity.SnapshotId);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.FileId))!.SetValue(media, entity.FileId);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.FileName))!.SetValue(media, entity.FileName);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.ContentType))!.SetValue(media, entity.ContentType);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.FileSizeBytes))!.SetValue(media, entity.FileSizeBytes);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.MediaType))!.SetValue(media, Enum.Parse<SessionMediaType>(entity.MediaType));
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.Caption))!.SetValue(media, entity.Caption);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.ThumbnailFileId))!.SetValue(media, entity.ThumbnailFileId);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.TurnNumber))!.SetValue(media, entity.TurnNumber);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.IsSharedWithSession))!.SetValue(media, entity.IsSharedWithSession);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.CreatedAt))!.SetValue(media, entity.CreatedAt);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.UpdatedAt))!.SetValue(media, entity.UpdatedAt);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.IsDeleted))!.SetValue(media, entity.IsDeleted);
        typeof(SessionMedia).GetProperty(nameof(SessionMedia.DeletedAt))!.SetValue(media, entity.DeletedAt);

        return media;
    }
}
