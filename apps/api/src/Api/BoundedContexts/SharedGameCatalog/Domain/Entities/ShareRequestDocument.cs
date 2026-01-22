using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a document attached to a share request.
/// References documents processed by the DocumentProcessing bounded context.
/// </summary>
public sealed class ShareRequestDocument : Entity<Guid>
{
    private Guid _id;
    private Guid _shareRequestId;
    private Guid _documentId;
    private readonly string _fileName = string.Empty;
    private readonly string _contentType = string.Empty;
    private readonly long _fileSize;
    private readonly DateTime _attachedAt;

    /// <summary>
    /// Gets the unique identifier of this document attachment.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the parent share request.
    /// </summary>
    public Guid ShareRequestId => _shareRequestId;

    /// <summary>
    /// Gets the ID of the document in the DocumentProcessing context.
    /// </summary>
    public Guid DocumentId => _documentId;

    /// <summary>
    /// Gets the original file name of the document.
    /// </summary>
    public string FileName => _fileName;

    /// <summary>
    /// Gets the MIME content type of the document.
    /// </summary>
    public string ContentType => _contentType;

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public long FileSize => _fileSize;

    /// <summary>
    /// Gets the date and time when the document was attached.
    /// </summary>
    public DateTime AttachedAt => _attachedAt;

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private ShareRequestDocument() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal ShareRequestDocument(
        Guid id,
        Guid shareRequestId,
        Guid documentId,
        string fileName,
        string contentType,
        long fileSize,
        DateTime attachedAt) : base(id)
    {
        _id = id;
        _shareRequestId = shareRequestId;
        _documentId = documentId;
        _fileName = fileName;
        _contentType = contentType;
        _fileSize = fileSize;
        _attachedAt = attachedAt;
    }

    /// <summary>
    /// Creates a new document attachment for a share request.
    /// </summary>
    /// <param name="shareRequestId">The ID of the parent share request.</param>
    /// <param name="documentId">The ID of the document in DocumentProcessing.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <returns>A new ShareRequestDocument instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when any required parameter is invalid.
    /// </exception>
    public static ShareRequestDocument Create(
        Guid shareRequestId,
        Guid documentId,
        string fileName,
        string contentType,
        long fileSize)
    {
        if (shareRequestId == Guid.Empty)
            throw new ArgumentException("ShareRequestId cannot be empty", nameof(shareRequestId));

        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId cannot be empty", nameof(documentId));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName is required", nameof(fileName));

        if (fileName.Length > 255)
            throw new ArgumentException("FileName cannot exceed 255 characters", nameof(fileName));

        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("ContentType is required", nameof(contentType));

        if (contentType.Length > 100)
            throw new ArgumentException("ContentType cannot exceed 100 characters", nameof(contentType));

        if (fileSize <= 0)
            throw new ArgumentException("FileSize must be greater than zero", nameof(fileSize));

        return new ShareRequestDocument(
            Guid.NewGuid(),
            shareRequestId,
            documentId,
            fileName.Trim(),
            contentType.Trim(),
            fileSize,
            DateTime.UtcNow);
    }
}
