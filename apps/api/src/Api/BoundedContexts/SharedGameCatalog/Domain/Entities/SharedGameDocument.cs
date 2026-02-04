using System.Text.RegularExpressions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Entity representing a document (PDF) associated with a shared game.
/// Supports multiple document types with versioning and optional tags for homerules.
/// </summary>
public sealed partial class SharedGameDocument : Entity<Guid>
{
    private const int MaxTags = 10;
    private const int MaxTagLength = 50;

    private static readonly Regex TagNormalizeRegex = NormalizeTagRegex();

    private Guid _id;
    private Guid _sharedGameId;
    private Guid _pdfDocumentId;
    private SharedGameDocumentType _documentType;
    private string _version = string.Empty;
    private bool _isActive;
    private readonly List<string> _tags = new();
    private readonly DateTime _createdAt;
    private Guid _createdBy;

    // Approval workflow fields
    private DocumentApprovalStatus _approvalStatus;
    private Guid? _approvedBy;
    private DateTime? _approvedAt;
    private string? _approvalNotes;

    /// <summary>
    /// Gets the unique identifier of this document association.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the shared game this document belongs to.
    /// </summary>
    public Guid SharedGameId => _sharedGameId;

    /// <summary>
    /// Gets the ID of the PDF document.
    /// </summary>
    public Guid PdfDocumentId => _pdfDocumentId;

    /// <summary>
    /// Gets the type of document (Rulebook, Errata, Homerule).
    /// </summary>
    public SharedGameDocumentType DocumentType => _documentType;

    /// <summary>
    /// Gets the version string (e.g., "1.0", "2.1").
    /// </summary>
    public string Version => _version;

    /// <summary>
    /// Gets whether this is the active version for its type.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets the tags associated with this document (only for Homerule type).
    /// </summary>
    public IReadOnlyList<string> Tags => _tags.AsReadOnly();

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Gets the ID of the user who created this document association.
    /// </summary>
    public Guid CreatedBy => _createdBy;

    /// <summary>
    /// Gets the approval status of this document for RAG processing.
    /// </summary>
    public DocumentApprovalStatus ApprovalStatus => _approvalStatus;

    /// <summary>
    /// Gets the ID of the admin who approved this document (null if not yet approved).
    /// </summary>
    public Guid? ApprovedBy => _approvedBy;

    /// <summary>
    /// Gets the timestamp when this document was approved (null if not yet approved).
    /// </summary>
    public DateTime? ApprovedAt => _approvedAt;

    /// <summary>
    /// Gets optional notes from the approver.
    /// </summary>
    public string? ApprovalNotes => _approvalNotes;

    /// <summary>
    /// Parameterless constructor for EF Core.
    /// </summary>
    private SharedGameDocument() : base()
    {
    }

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal SharedGameDocument(
        Guid id,
        Guid sharedGameId,
        Guid pdfDocumentId,
        SharedGameDocumentType documentType,
        string version,
        bool isActive,
        IEnumerable<string>? tags,
        DateTime createdAt,
        Guid createdBy,
        DocumentApprovalStatus approvalStatus = DocumentApprovalStatus.Pending,
        Guid? approvedBy = null,
        DateTime? approvedAt = null,
        string? approvalNotes = null) : base(id)
    {
        _id = id;
        _sharedGameId = sharedGameId;
        _pdfDocumentId = pdfDocumentId;
        _documentType = documentType;
        _version = version;
        _isActive = isActive;
        _tags = tags?.ToList() ?? new List<string>();
        _createdAt = createdAt;
        _createdBy = createdBy;
        _approvalStatus = approvalStatus;
        _approvedBy = approvedBy;
        _approvedAt = approvedAt;
        _approvalNotes = approvalNotes;
    }

    /// <summary>
    /// Creates a new SharedGameDocument with validation.
    /// </summary>
    public static SharedGameDocument Create(
        Guid sharedGameId,
        Guid pdfDocumentId,
        SharedGameDocumentType documentType,
        string version,
        Guid createdBy,
        IEnumerable<string>? tags = null)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        if (pdfDocumentId == Guid.Empty)
            throw new ArgumentException("PdfDocumentId cannot be empty", nameof(pdfDocumentId));

        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        // Validate version format
        var documentVersion = DocumentVersion.Create(version);

        var doc = new SharedGameDocument(
            Guid.NewGuid(),
            sharedGameId,
            pdfDocumentId,
            documentType,
            documentVersion.Value,
            false, // Not active by default
            null,
            DateTime.UtcNow,
            createdBy);

        // Add tags if it's a Homerule document
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                doc.AddTag(tag);
            }
        }

        return doc;
    }

    /// <summary>
    /// Sets this document as the active version for its type.
    /// </summary>
    public void SetAsActive()
    {
        _isActive = true;
    }

    /// <summary>
    /// Deactivates this document version.
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
    }

    /// <summary>
    /// Adds a tag to this document. Only valid for Homerule documents.
    /// </summary>
    /// <param name="tag">The tag to add</param>
    /// <exception cref="InvalidOperationException">Thrown when document is not a Homerule</exception>
    /// <exception cref="ArgumentException">Thrown when tag is invalid or max tags reached</exception>
    public void AddTag(string tag)
    {
        if (_documentType != SharedGameDocumentType.Homerule)
            throw new InvalidOperationException("Tags are only allowed for Homerule documents");

        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be empty", nameof(tag));

        var normalizedTag = NormalizeTag(tag);

        if (normalizedTag.Length > MaxTagLength)
            throw new ArgumentException($"Tag cannot exceed {MaxTagLength} characters after normalization", nameof(tag));

        if (string.IsNullOrEmpty(normalizedTag))
            throw new ArgumentException("Tag contains no valid characters after normalization", nameof(tag));

        if (_tags.Count >= MaxTags)
            throw new InvalidOperationException($"Cannot add more than {MaxTags} tags to a document");

        if (!_tags.Contains(normalizedTag, StringComparer.Ordinal))
            _tags.Add(normalizedTag);
    }

    /// <summary>
    /// Removes a tag from this document.
    /// </summary>
    /// <param name="tag">The tag to remove</param>
    public void RemoveTag(string tag)
    {
        var normalizedTag = NormalizeTag(tag);
        _tags.Remove(normalizedTag);
    }

    /// <summary>
    /// Clears all tags from this document.
    /// </summary>
    public void ClearTags()
    {
        _tags.Clear();
    }

    /// <summary>
    /// Checks if this document has a specific tag.
    /// </summary>
    /// <param name="tag">The tag to check</param>
    /// <returns>True if the document has the tag</returns>
    public bool HasTag(string tag)
    {
        var normalizedTag = NormalizeTag(tag);
        return _tags.Contains(normalizedTag, StringComparer.Ordinal);
    }

    /// <summary>
    /// Approves this document for RAG processing.
    /// </summary>
    /// <param name="approvedBy">The ID of the admin approving the document</param>
    /// <param name="notes">Optional notes from the approver</param>
    /// <exception cref="InvalidOperationException">Thrown when document is already approved or rejected</exception>
    public void Approve(Guid approvedBy, string? notes = null)
    {
        if (_approvalStatus != DocumentApprovalStatus.Pending)
            throw new InvalidOperationException($"Cannot approve document with status {_approvalStatus}");

        if (approvedBy == Guid.Empty)
            throw new ArgumentException("ApprovedBy cannot be empty", nameof(approvedBy));

        _approvalStatus = DocumentApprovalStatus.Approved;
        _approvedBy = approvedBy;
        _approvedAt = DateTime.UtcNow;
        _approvalNotes = notes;
    }

    /// <summary>
    /// Rejects this document from RAG processing.
    /// </summary>
    /// <param name="rejectedBy">The ID of the admin rejecting the document</param>
    /// <param name="reason">Reason for rejection</param>
    /// <exception cref="InvalidOperationException">Thrown when document is already approved or rejected</exception>
    public void Reject(Guid rejectedBy, string reason)
    {
        if (_approvalStatus != DocumentApprovalStatus.Pending)
            throw new InvalidOperationException($"Cannot reject document with status {_approvalStatus}");

        if (rejectedBy == Guid.Empty)
            throw new ArgumentException("RejectedBy cannot be empty", nameof(rejectedBy));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required", nameof(reason));

        _approvalStatus = DocumentApprovalStatus.Rejected;
        _approvedBy = rejectedBy;
        _approvedAt = DateTime.UtcNow;
        _approvalNotes = reason;
    }

    /// <summary>
    /// Resets approval status to Pending (admin can re-review).
    /// </summary>
    public void ResetApproval()
    {
        _approvalStatus = DocumentApprovalStatus.Pending;
        _approvedBy = null;
        _approvedAt = null;
        _approvalNotes = null;
    }

    /// <summary>
    /// Normalizes a tag: lowercase, spaces to hyphens, remove special characters.
    /// </summary>
    private static string NormalizeTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        return TagNormalizeRegex.Replace(
            tag.Trim().ToLowerInvariant().Replace(" ", "-"),
            string.Empty);
    }

    [GeneratedRegex(@"[^a-z0-9-]", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NormalizeTagRegex();
}
