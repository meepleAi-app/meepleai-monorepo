namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Entities.MechanicCitation"/>.
/// </summary>
public class MechanicCitationEntity
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }

    /// <summary>1-based page number in the originating PDF. Always &gt; 0 (enforced by CHECK).</summary>
    public int PdfPage { get; set; }

    /// <summary>Verbatim quote, max 400 chars / 25 words (ADR-051 T1). Word cap enforced by CHECK.</summary>
    public string Quote { get; set; } = string.Empty;

    /// <summary>Nullable: chunks may be re-indexed. ON DELETE SET NULL in FK.</summary>
    public Guid? ChunkId { get; set; }

    public int DisplayOrder { get; set; }

    // === Navigation ===
    public MechanicClaimEntity Claim { get; set; } = default!;
}
