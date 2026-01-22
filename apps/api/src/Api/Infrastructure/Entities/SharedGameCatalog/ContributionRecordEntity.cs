namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// EF Core entity for ContributionRecord.
/// Maps to the contribution_records table.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
public class ContributionRecordEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// The contributor who made this contribution.
    /// </summary>
    public Guid ContributorId { get; set; }

    /// <summary>
    /// Type: 0=InitialSubmission, 1=DocumentAddition, 2=MetadataUpdate, 3=ContentEnhancement
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Description of what was contributed.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Version number of this contribution (sequential per game).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// When this contribution was made.
    /// </summary>
    public DateTime ContributedAt { get; set; }

    /// <summary>
    /// Link to the share request that resulted in this contribution (if any).
    /// </summary>
    public Guid? ShareRequestId { get; set; }

    /// <summary>
    /// Document IDs included in this contribution (stored as JSON array).
    /// </summary>
    public string? DocumentIdsJson { get; set; }

    /// <summary>
    /// Whether this contribution includes game data changes.
    /// </summary>
    public bool IncludesGameData { get; set; }

    /// <summary>
    /// Whether this contribution includes metadata changes.
    /// </summary>
    public bool IncludesMetadata { get; set; }

    // Navigation properties
    public ContributorEntity Contributor { get; set; } = default!;
}
