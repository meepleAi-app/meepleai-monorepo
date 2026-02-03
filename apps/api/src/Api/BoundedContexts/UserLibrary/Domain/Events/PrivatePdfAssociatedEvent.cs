using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when a private PDF document is associated with a library entry.
/// </summary>
internal sealed class PrivatePdfAssociatedEvent : DomainEventBase
{
    /// <summary>
    /// The ID of the library entry.
    /// </summary>
    public Guid LibraryEntryId { get; }

    /// <summary>
    /// The ID of the user who owns the library entry.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The ID of the game in the library entry.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// The ID of the PDF document that was associated.
    /// </summary>
    public Guid PdfDocumentId { get; }

    public PrivatePdfAssociatedEvent(
        Guid libraryEntryId,
        Guid userId,
        Guid gameId,
        Guid pdfDocumentId)
    {
        LibraryEntryId = libraryEntryId;
        UserId = userId;
        GameId = gameId;
        PdfDocumentId = pdfDocumentId;
    }
}
