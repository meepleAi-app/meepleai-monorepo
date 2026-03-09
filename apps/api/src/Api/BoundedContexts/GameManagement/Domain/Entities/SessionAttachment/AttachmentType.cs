namespace Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;

/// <summary>
/// Types of photos/attachments that can be captured during a live game session.
/// Issue #5359 - SessionAttachment domain entity.
/// </summary>
public enum AttachmentType
{
    PlayerArea = 0,
    BoardState = 1,
    CharacterSheet = 2,
    ResourceInventory = 3,
    Custom = 4,
}
