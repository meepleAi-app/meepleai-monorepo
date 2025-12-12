using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Entities;

/// <summary>
/// DocumentCollection aggregate root representing a collection of related PDF documents for a game.
/// Issue #2051: Supports base rulebooks, expansions, errata, and house rules
/// </summary>
public sealed class DocumentCollection : AggregateRoot<Guid>
{
    private const int MaxDocumentsPerCollection = 5;

    public Guid GameId { get; private set; }
    public CollectionName Name { get; private set; }
    public string? Description { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Collection of document references (stored as Guid references, not full entities)
    private readonly List<CollectionDocument> _documents = new();
    public IReadOnlyList<CollectionDocument> Documents => _documents.AsReadOnly();

#pragma warning disable CS8618
    private DocumentCollection() : base() { }
#pragma warning restore CS8618

    public DocumentCollection(
        Guid id,
        Guid gameId,
        CollectionName name,
        Guid createdByUserId,
        string? description = null) : base(id)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID cannot be empty", nameof(gameId));

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("Created by user ID cannot be empty", nameof(createdByUserId));

        GameId = gameId;
        Name = name;
        Description = description;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a PDF document to this collection.
    /// Validates: max documents limit, no duplicates, valid document type
    /// </summary>
    public void AddDocument(Guid pdfDocumentId, DocumentType documentType, int sortOrder = 0)
    {
        if (pdfDocumentId == Guid.Empty)
            throw new ArgumentException("PDF document ID cannot be empty", nameof(pdfDocumentId));

        ArgumentNullException.ThrowIfNull(documentType);

        // Validate max documents
        if (_documents.Count >= MaxDocumentsPerCollection)
            throw new DomainException(
                $"Cannot add document: collection already contains maximum of {MaxDocumentsPerCollection} documents");

        // Validate no duplicates
        if (_documents.Any(d => d.PdfDocumentId == pdfDocumentId))
            throw new DomainException($"Document {pdfDocumentId} is already in this collection");

        var collectionDoc = new CollectionDocument(pdfDocumentId, documentType, sortOrder);
        _documents.Add(collectionDoc);
        UpdatedAt = DateTime.UtcNow;

        // Domain event for auditing
        // AddDomainEvent(new DocumentAddedToCollectionEvent(Id, pdfDocumentId, documentType));
    }

    /// <summary>
    /// Removes a PDF document from this collection.
    /// </summary>
    public void RemoveDocument(Guid pdfDocumentId)
    {
        var document = _documents.FirstOrDefault(d => d.PdfDocumentId == pdfDocumentId);

        if (document == null)
            throw new DomainException($"Document {pdfDocumentId} not found in collection");

        _documents.Remove(document);
        UpdatedAt = DateTime.UtcNow;

        // Domain event for auditing
        // AddDomainEvent(new DocumentRemovedFromCollectionEvent(Id, pdfDocumentId));
    }

    /// <summary>
    /// Gets a document by ID from this collection.
    /// </summary>
    public CollectionDocument? GetDocument(Guid pdfDocumentId)
    {
        return _documents.FirstOrDefault(d => d.PdfDocumentId == pdfDocumentId);
    }

    /// <summary>
    /// Updates the collection metadata.
    /// </summary>
    public void UpdateMetadata(CollectionName name, string? description)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets documents ordered by sort order for display.
    /// </summary>
    public IReadOnlyList<CollectionDocument> GetDocumentsOrdered()
    {
        return _documents.OrderBy(d => d.SortOrder).ToList().AsReadOnly();
    }

    /// <summary>
    /// Checks if collection contains a specific document type.
    /// </summary>
    public bool ContainsDocumentType(DocumentType type)
    {
        return _documents.Any(d => d.Type.Value == type.Value);
    }

    /// <summary>
    /// Gets count of documents in collection.
    /// </summary>
    public int DocumentCount => _documents.Count;

    /// <summary>
    /// Checks if collection is empty.
    /// </summary>
    public bool IsEmpty => _documents.Count == 0;

    /// <summary>
    /// Checks if collection is at max capacity.
    /// </summary>
    public bool IsFull => _documents.Count >= MaxDocumentsPerCollection;
}

/// <summary>
/// Value object representing a document within a collection.
/// Stores reference to PDF document ID plus collection-specific metadata.
/// </summary>
public sealed record CollectionDocument
{
    public Guid PdfDocumentId { get; init; }
    public DocumentType Type { get; init; }
    public int SortOrder { get; init; }
    public DateTime AddedAt { get; init; }

    public CollectionDocument(Guid pdfDocumentId, DocumentType type, int sortOrder)
    {
        if (pdfDocumentId == Guid.Empty)
            throw new ArgumentException("PDF document ID cannot be empty", nameof(pdfDocumentId));

        ArgumentNullException.ThrowIfNull(type);

        if (sortOrder < 0)
            throw new ArgumentException("Sort order cannot be negative", nameof(sortOrder));

        PdfDocumentId = pdfDocumentId;
        Type = type;
        SortOrder = sortOrder;
        AddedAt = DateTime.UtcNow;
    }
}
