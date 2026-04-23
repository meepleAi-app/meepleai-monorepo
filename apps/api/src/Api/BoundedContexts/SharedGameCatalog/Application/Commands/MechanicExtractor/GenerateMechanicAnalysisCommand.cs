using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Kicks off an AI-generated mechanic analysis for a shared game's PDF rulebook (ISSUE-524 / M1.2,
/// ADR-051). The handler creates a <c>Draft</c> <see cref="Domain.Aggregates.MechanicAnalysis"/>
/// aggregate and schedules the LLM pipeline to run asynchronously (B5=B). The endpoint returns
/// 202 Accepted.
/// </summary>
/// <param name="SharedGameId">Target shared game whose rulebook will be analyzed.</param>
/// <param name="PdfDocumentId">The specific PDF document version to analyze. Must be linked to
///   <paramref name="SharedGameId"/> via <c>shared_game_documents</c>.</param>
/// <param name="RequestedBy">User id of the admin triggering the run. Recorded as
///   <c>CreatedBy</c> on the aggregate and, on mid-run abort, as <c>ReviewedBy</c> for audit.</param>
/// <param name="CostCapUsd">Hard USD cap for this run (T8 cost governance). The pipeline aborts
///   via <c>AutoRejectFromDraft</c> if cumulative cost exceeds the effective cap mid-run.</param>
/// <param name="CostCapOverride">Optional planning-time override (B3=A). When present, the handler
///   raises the aggregate's cap to <see cref="CostCapOverrideInput.NewCapUsd"/> immediately after
///   creation, preserving the override reason on the aggregate's audit fields
///   (<c>CostCapOverrideAt</c>/<c>CostCapOverrideBy</c>/<c>CostCapOverrideReason</c>).</param>
internal record GenerateMechanicAnalysisCommand(
    Guid SharedGameId,
    Guid PdfDocumentId,
    Guid RequestedBy,
    decimal CostCapUsd,
    CostCapOverrideInput? CostCapOverride = null) : ICommand<MechanicAnalysisGenerationResponseDto>;

/// <summary>
/// Admin-initiated raise of the default cost cap at planning time. Required when the cost
/// estimator projects a total cost above the submitted <c>CostCapUsd</c> and the admin decides
/// to proceed anyway with a justified, auditable override.
/// </summary>
/// <param name="NewCapUsd">New cap in USD. Must be strictly greater than the original
///   <c>CostCapUsd</c>.</param>
/// <param name="Reason">Free-form justification (20-500 chars). Persisted into
///   <c>MechanicAnalysis.CostCapOverrideReason</c> for audit (T6).</param>
internal record CostCapOverrideInput(decimal NewCapUsd, string Reason);
