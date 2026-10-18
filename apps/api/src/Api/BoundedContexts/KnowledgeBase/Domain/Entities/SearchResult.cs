using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Represents a search result from vector/hybrid search.
/// Not an aggregate root - created during search operations.
/// </summary>
internal sealed class SearchResult : Entity<Guid>
{
    public Guid VectorDocumentId { get; private set; }
    public string TextContent { get; private set; }
    public int PageNumber { get; private set; }
    public Confidence RelevanceScore { get; private set; }
    public int Rank { get; private set; }
    public string? SearchMethod { get; private set; } // "vector", "keyword", "hybrid"
    public Guid PdfDocumentId { get; private set; }
    public int ChunkIndex { get; private set; }

    /// <summary>
    /// #4288: l'id del <c>text_chunks</c> da cui viene il passaggio. Null quando il braccio vettoriale
    /// lo restituisce da un embedding scritto senza <c>source_chunk_id</c>: un lotto di foto, che non
    /// ha un chunk testuale, un embedding precedente a SP2, o un PDF caricato da <c>/ingest/pdf</c>
    /// finché è aperta #4384.
    /// </summary>
    public Guid? TextChunkId { get; private set; }

    public GameBookRole RoleTags { get; private set; }

    /// <summary>#3270: chunk heading-path label for the heading-match boost (nullable).</summary>
    public string? Heading { get; private set; }

    /// <summary>
    /// SP-C (#3407): raw bounding-box JSON + char offsets carried from the vector arm to the citation
    /// surface (parsed + Full-gated at the API boundary). Null for the keyword arm / pre-coordinate corpus.
    /// </summary>
    public string? BoundingBoxesJson { get; private set; }
    public int? CharStart { get; private set; }
    public int? CharEnd { get; private set; }

    /// <summary>#4168 (5e-c-2): i segnali grezzi della fusione, per chi fonde di nuovo sopra di lei.</summary>
    public RetrievalSignals Signals { get; private set; } = RetrievalSignals.None;

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private SearchResult() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new search result.
    /// </summary>
    public SearchResult(
        Guid id,
        Guid vectorDocumentId,
        string textContent,
        int pageNumber,
        Confidence relevanceScore,
        int rank,
        string? searchMethod = null,
        Guid pdfDocumentId = default,
        int chunkIndex = 0,
        GameBookRole roleTags = GameBookRole.None,
        string? heading = null,
        string? boundingBoxesJson = null,
        int? charStart = null,
        int? charEnd = null,
        RetrievalSignals? signals = null,
        Guid? textChunkId = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(textContent))
            throw new ArgumentException("Text content cannot be empty", nameof(textContent));

        if (pageNumber < 1)
            throw new ArgumentException("Page number must be positive", nameof(pageNumber));

        if (rank < 1)
            throw new ArgumentException("Rank must be positive", nameof(rank));

        VectorDocumentId = vectorDocumentId;
        TextContent = textContent;
        PageNumber = pageNumber;
        RelevanceScore = relevanceScore ?? throw new ArgumentNullException(nameof(relevanceScore));
        Rank = rank;
        SearchMethod = searchMethod;
        PdfDocumentId = pdfDocumentId;
        ChunkIndex = chunkIndex;
        RoleTags = roleTags;
        Heading = heading;
        BoundingBoxesJson = boundingBoxesJson;
        CharStart = charStart;
        CharEnd = charEnd;
        Signals = signals ?? RetrievalSignals.None;
        TextChunkId = textChunkId;
    }

    /// <summary>
    /// Checks if this result meets a minimum confidence threshold.
    /// </summary>
    public bool MeetsThreshold(double minScore)
    {
        return RelevanceScore.Value >= minScore;
    }
}
