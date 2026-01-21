namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// EF Core entity for Contributor.
/// Maps to the contributors table.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
public class ContributorEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// The user who made the contributions.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The shared game this contributor is associated with.
    /// </summary>
    public Guid SharedGameId { get; set; }

    /// <summary>
    /// Whether this is the primary (original) contributor.
    /// </summary>
    public bool IsPrimaryContributor { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }

    // Navigation properties
    public SharedGameEntity SharedGame { get; set; } = default!;
    public ICollection<ContributionRecordEntity> Contributions { get; set; } = new List<ContributionRecordEntity>();
}
