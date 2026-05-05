using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Attribution citation for a <see cref="MechanicClaim"/>: a verbatim short quote from the
/// source rulebook PDF with a page pointer, plus an optional link to the originating text chunk.
/// </summary>
/// <remarks>
/// Design rationale (ADR-051 + §2.1 of the M1.1 plan):
/// - Separate table instead of EF <c>OwnsMany</c> because the side-panel navigation needs indexed
///   lookups by <see cref="PdfPage"/>, and the chunk FK must be nullable with
///   <c>ON DELETE SET NULL</c>.
/// - Quote length is capped at 25 words at the DB level (T1 belt-and-braces CHECK constraint);
///   the domain factory also enforces it so that invariants are audited before persistence.
/// - <see cref="ChunkId"/> is nullable because text chunks may be re-indexed; losing the chunk
///   reference is acceptable, losing the quote+page is not.
/// </remarks>
public sealed class MechanicCitation : Entity<Guid>
{
    /// <summary>Maximum number of words allowed inside <see cref="Quote"/> (ADR-051 T1).</summary>
    public const int MaxQuoteWords = 25;

    /// <summary>Maximum character budget for <see cref="Quote"/>.</summary>
    /// <remarks>400 = 25 words × ~16 chars upper bound. Used to size the varchar column.</remarks>
    public const int MaxQuoteChars = 400;

    /// <summary>FK to the parent <see cref="MechanicClaim"/>.</summary>
    public Guid ClaimId { get; private set; }

    /// <summary>1-based page number in the originating PDF. Always &gt; 0.</summary>
    public int PdfPage { get; private set; }

    /// <summary>
    /// Verbatim excerpt from the rulebook used as attribution for the parent claim.
    /// Must be ≤ <see cref="MaxQuoteWords"/> words — enforced both here and by a DB CHECK constraint.
    /// </summary>
    public string Quote { get; private set; } = string.Empty;

    /// <summary>
    /// Optional reference to the originating text chunk (for side-panel navigation / re-verification).
    /// Nullable because chunks may be re-indexed and set to NULL by ON DELETE SET NULL.
    /// </summary>
    public Guid? ChunkId { get; private set; }

    /// <summary>Display order inside the claim's citation list (0-based).</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// True when the citation was instantiated via <see cref="Create"/> and is not yet persisted;
    /// false when rehydrated from storage via <see cref="Reconstitute"/>. Mirrors
    /// <see cref="MechanicClaim.IsNew"/> — see that field's remarks for rationale.
    /// </summary>
    public bool IsNew { get; private set; } = true;

    /// <summary>
    /// EF Core constructor. Do not use directly.
    /// </summary>
    private MechanicCitation() : base()
    {
    }

    private MechanicCitation(
        Guid id,
        Guid claimId,
        int pdfPage,
        string quote,
        Guid? chunkId,
        int displayOrder)
        : base(id)
    {
        ClaimId = claimId;
        PdfPage = pdfPage;
        Quote = quote;
        ChunkId = chunkId;
        DisplayOrder = displayOrder;
    }

    /// <summary>
    /// Factory that validates all invariants. Raises <see cref="ArgumentException"/> on violation
    /// (caller is expected to be the aggregate root or a command handler with validated input).
    /// </summary>
    public static MechanicCitation Create(
        Guid claimId,
        int pdfPage,
        string quote,
        Guid? chunkId,
        int displayOrder)
    {
        if (claimId == Guid.Empty)
        {
            throw new ArgumentException("ClaimId cannot be empty.", nameof(claimId));
        }

        if (pdfPage <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pdfPage),
                pdfPage,
                "PdfPage must be a positive 1-based page number.");
        }

        if (string.IsNullOrWhiteSpace(quote))
        {
            throw new ArgumentException("Quote cannot be empty.", nameof(quote));
        }

        var trimmed = quote.Trim();
        if (trimmed.Length > MaxQuoteChars)
        {
            throw new ArgumentException(
                $"Quote exceeds {MaxQuoteChars} characters (ADR-051 T1 enforcement).",
                nameof(quote));
        }

        var wordCount = CountWords(trimmed);
        if (wordCount > MaxQuoteWords)
        {
            throw new ArgumentException(
                $"Quote has {wordCount} words, exceeding the {MaxQuoteWords}-word cap (ADR-051 T1).",
                nameof(quote));
        }

        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayOrder),
                displayOrder,
                "DisplayOrder must be non-negative.");
        }

        return new MechanicCitation(
            id: Guid.NewGuid(),
            claimId: claimId,
            pdfPage: pdfPage,
            quote: trimmed,
            chunkId: chunkId,
            displayOrder: displayOrder);
    }

    /// <summary>
    /// Rehydrates a citation from persistence. Used exclusively by the repository's
    /// <c>MapToDomain</c>; bypasses validation because invariants were enforced at creation time.
    /// </summary>
    public static MechanicCitation Reconstitute(
        Guid id,
        Guid claimId,
        int pdfPage,
        string quote,
        Guid? chunkId,
        int displayOrder)
    {
        return new MechanicCitation(
            id: id,
            claimId: claimId,
            pdfPage: pdfPage,
            quote: quote,
            chunkId: chunkId,
            displayOrder: displayOrder)
        {
            IsNew = false
        };
    }

    private static readonly char[] WhitespaceSeparators =
        { ' ', '\t', '\n', '\r', '\f', '\v' };

    /// <summary>Counts whitespace-separated tokens — mirrors the DB CHECK constraint logic.</summary>
    internal static int CountWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return value.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
