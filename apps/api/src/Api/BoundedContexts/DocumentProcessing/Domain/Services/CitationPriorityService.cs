using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Services;

/// <summary>
/// Domain service for determining citation priority across multiple documents in a collection.
/// Issue #2051: Implements priority logic - homerule > (errata/expansion by date) > base
/// </summary>
public sealed class CitationPriorityService
{
    /// <summary>
    /// Orders citations by document priority and date.
    /// Priority: homerule (3) > errata/expansion by upload date (2/1) > base (0)
    /// </summary>
    public IReadOnlyList<PrioritizedCitation> OrderCitations(
        IReadOnlyList<Citation> citations,
        IReadOnlyList<PdfDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(citations);
        ArgumentNullException.ThrowIfNull(documents);

        // Create lookup for document metadata
        var documentLookup = documents.ToDictionary(d => d.Id, d => d);

        var prioritized = citations
            .Select(citation =>
            {
                if (!documentLookup.TryGetValue(citation.PdfDocumentId, out var document))
                {
                    // Fallback if document not found (shouldn't happen, but defensive)
                    return new PrioritizedCitation(citation, 0, DateTime.MinValue, DocumentType.Base);
                }

                return new PrioritizedCitation(
                    citation,
                    document.DocumentType.Priority,
                    document.UploadedAt,
                    document.DocumentType);
            })
            .OrderByDescending(pc => pc.Priority) // Higher priority first
            .ThenByDescending(pc => pc.UploadedAt) // Same priority? Newer first (for errata/expansion)
            .ToList();

        return prioritized.AsReadOnly();
    }

    /// <summary>
    /// De-duplicates citations with same content hash, keeping highest priority.
    /// Prevents showing same snippet from base + expansion.
    /// </summary>
    public IReadOnlyList<PrioritizedCitation> DeduplicateCitations(
        IReadOnlyList<PrioritizedCitation> citations)
    {
        ArgumentNullException.ThrowIfNull(citations);

        // Group by content hash, take first (already ordered by priority)
        var deduplicated = citations
            .GroupBy(c => GetContentHash(c.Citation.Text))
            .Select(g => g.First()) // First = highest priority due to prior ordering
            .ToList();

        return deduplicated.AsReadOnly();
    }

    /// <summary>
    /// Filters citations to only include those from specified document IDs.
    /// Used when user selects specific documents in chat source filter.
    /// </summary>
    public IReadOnlyList<PrioritizedCitation> FilterByDocuments(
        IReadOnlyList<PrioritizedCitation> citations,
        IReadOnlyList<Guid> selectedDocumentIds)
    {
        ArgumentNullException.ThrowIfNull(citations);
        ArgumentNullException.ThrowIfNull(selectedDocumentIds);

        if (selectedDocumentIds.Count == 0)
            return citations; // No filter = all documents

        var selectedSet = new HashSet<Guid>(selectedDocumentIds);

        return citations
            .Where(c => selectedSet.Contains(c.Citation.PdfDocumentId))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Computes SHA256 hash of citation text for de-duplication.
    /// </summary>
    private static string GetContentHash(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var bytes = System.Text.Encoding.UTF8.GetBytes(content.Trim().ToLowerInvariant());
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}

/// <summary>
/// Citation with priority metadata for ordering.
/// </summary>
public sealed record PrioritizedCitation(
    Citation Citation,
    int Priority,
    DateTime UploadedAt,
    DocumentType DocumentType);

/// <summary>
/// Basic citation record (placeholder - should match actual Citation from KnowledgeBase context).
/// </summary>
public sealed record Citation(
    Guid PdfDocumentId,
    string Text,
    int PageNumber,
    string DocumentName);
