using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to rebuild the RAPTOR (Recursive Abstractive Processing for Tree-Organized Retrieval)
/// hierarchical summary tree for all PDFs of a game.
///
/// This operation is gated by subscription tier:
/// - Free-tier users: 403 Forbidden with error code "TIER_FEATURE_LOCKED"
/// - Premium/Admin users: allowed
///
/// Implementation note: Synchronous execution in the request pipeline.
/// Background-job queuing is a planned future enhancement.
///
/// Issue #903: SG2 — KB lifecycle con re-index smoke test.
/// </summary>
/// <param name="GameId">The ID of the game whose RAPTOR tree should be rebuilt.</param>
/// <param name="UserId">The ID of the user triggering the rebuild (used for tier check).</param>
internal record RebuildRaptorCommand(Guid GameId, Guid UserId)
    : ICommand<KbJobResponse>;
