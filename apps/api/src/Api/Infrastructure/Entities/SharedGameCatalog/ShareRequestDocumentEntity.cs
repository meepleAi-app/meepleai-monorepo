namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// EF Core entity for documents attached to a ShareRequest.
/// Maps to the share_request_documents table.
/// </summary>
public class ShareRequestDocumentEntity
{
    public Guid Id { get; set; }
    public Guid ShareRequestId { get; set; }
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime AttachedAt { get; set; }

    // Navigation property
    public ShareRequestEntity ShareRequest { get; set; } = default!;
}
