namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// ProposalMigration entity - persistence model for post-approval migration decisions.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
public class ProposalMigrationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the approved ShareRequest.
    /// </summary>
    public Guid ShareRequestId { get; set; }

    /// <summary>
    /// Reference to the original PrivateGame.
    /// </summary>
    public Guid PrivateGameId { get; set; }

    /// <summary>
    /// Reference to the newly created SharedGame.
    /// </summary>
    public Guid SharedGameId { get; set; }

    /// <summary>
    /// The user who owns this migration decision.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The migration choice (stored as integer enum).
    /// 0 = Pending, 1 = LinkToCatalog, 2 = KeepPrivate
    /// </summary>
    public int Choice { get; set; }

    /// <summary>
    /// When the migration was created (proposal approved).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user made their choice.
    /// </summary>
    public DateTime? ChoiceAt { get; set; }

    /// <summary>
    /// Optimistic concurrency control.
    /// </summary>
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public PrivateGameEntity? PrivateGame { get; set; }
}
