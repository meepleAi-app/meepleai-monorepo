namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Represents the action an admin takes when approving a NewGameProposal.
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
public enum ProposalApprovalAction
{
    /// <summary>
    /// Approve the proposal by creating a new SharedGame.
    /// This is the default action for unique games.
    /// </summary>
    ApproveAsNew = 0,

    /// <summary>
    /// Merge the proposal's knowledge base (PDFs) into an existing SharedGame.
    /// Used when the proposed game already exists in the catalog.
    /// Does not create a new SharedGame.
    /// </summary>
    MergeKnowledgeBase = 1,

    /// <summary>
    /// Approve the proposal as a variant of an existing game.
    /// Creates a new SharedGame with a variant suffix (e.g., "Game Title (Variant)").
    /// Used for game editions, expansions, or regional variants.
    /// </summary>
    ApproveAsVariant = 2
}
