using System.ComponentModel.DataAnnotations;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Aggregate root representing a migration decision after a private game proposal is approved.
/// Tracks the user's choice to link their library entry to the new SharedGame or keep the PrivateGame separate.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
public sealed class ProposalMigration : AggregateRoot<Guid>
{
    /// <summary>
    /// Reference to the approved ShareRequest that triggered this migration.
    /// </summary>
    public Guid ShareRequestId { get; private set; }

    /// <summary>
    /// Reference to the original PrivateGame that was proposed.
    /// </summary>
    public Guid PrivateGameId { get; private set; }

    /// <summary>
    /// Reference to the newly created SharedGame from the approved proposal.
    /// </summary>
    public Guid SharedGameId { get; private set; }

    /// <summary>
    /// The ID of the user who owns this migration decision.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// The user's choice for how to handle their library entry post-approval.
    /// </summary>
    public PostApprovalMigrationChoice Choice { get; private set; }

    /// <summary>
    /// When the migration was created (i.e., when the proposal was approved).
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// When the user made their choice (null if still pending).
    /// </summary>
    public DateTime? ChoiceAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private ProposalMigration() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Private constructor with validation.
    /// </summary>
    private ProposalMigration(
        Guid id,
        Guid shareRequestId,
        Guid privateGameId,
        Guid sharedGameId,
        Guid userId) : base(id)
    {
        ShareRequestId = shareRequestId;
        PrivateGameId = privateGameId;
        SharedGameId = sharedGameId;
        UserId = userId;
        Choice = PostApprovalMigrationChoice.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new ProposalMigration after a private game proposal is approved.
    /// </summary>
    /// <param name="shareRequestId">The approved ShareRequest ID</param>
    /// <param name="privateGameId">The original PrivateGame ID</param>
    /// <param name="sharedGameId">The newly created SharedGame ID</param>
    /// <param name="userId">The user who owns this migration</param>
    /// <returns>A new ProposalMigration in Pending state</returns>
    public static ProposalMigration Create(
        Guid shareRequestId,
        Guid privateGameId,
        Guid sharedGameId,
        Guid userId)
    {
        ValidateId(shareRequestId, nameof(shareRequestId));
        ValidateId(privateGameId, nameof(privateGameId));
        ValidateId(sharedGameId, nameof(sharedGameId));
        ValidateId(userId, nameof(userId));

        return new ProposalMigration(
            Guid.NewGuid(),
            shareRequestId,
            privateGameId,
            sharedGameId,
            userId);
    }

    /// <summary>
    /// Records the user's choice to link to the catalog.
    /// </summary>
    public void ChooseLinkToCatalog()
    {
        if (Choice != PostApprovalMigrationChoice.Pending)
            throw new InvalidOperationException("Migration choice has already been made");

        Choice = PostApprovalMigrationChoice.LinkToCatalog;
        ChoiceAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records the user's choice to keep the private game.
    /// </summary>
    public void ChooseKeepPrivate()
    {
        if (Choice != PostApprovalMigrationChoice.Pending)
            throw new InvalidOperationException("Migration choice has already been made");

        Choice = PostApprovalMigrationChoice.KeepPrivate;
        ChoiceAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if this migration is still pending a user choice.
    /// </summary>
    public bool IsPending => Choice == PostApprovalMigrationChoice.Pending;

    /// <summary>
    /// Gets or sets the row version for optimistic concurrency control.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; private set; }

    // Validation methods
    private static void ValidateId(Guid id, string paramName)
    {
        if (id == Guid.Empty)
            throw new ArgumentException($"{paramName} cannot be empty", paramName);
    }
}
