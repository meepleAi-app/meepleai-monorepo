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
/// <param name="ModelOverride">#539: optional LLM model override. Routing is by model name
///   (<c>ILlmClient.SupportsModel</c>), so this alone selects the provider; null → DeepSeek default.</param>
/// <param name="ProviderOverride">#539: optional provider label recorded for telemetry (e.g. "OpenRouter").</param>
/// <param name="ForceRegenerate">#539: when true, skips the T7 idempotency short-circuit so a re-run
///   with a different model creates a new analysis instead of returning the existing one.</param>
internal record GenerateMechanicAnalysisCommand(
    Guid SharedGameId,
    Guid PdfDocumentId,
    Guid RequestedBy,
    decimal CostCapUsd,
    CostCapOverrideInput? CostCapOverride = null,
    // #539 eval: optional LLM override. The pipeline routes the provider purely by model name
    // (ILlmClient.SupportsModel), so supplying ModelOverride is sufficient to switch provider;
    // ProviderOverride only labels telemetry. Null → the ADR-007 DeepSeek defaults in the handler.
    string? ModelOverride = null,
    string? ProviderOverride = null,
    // #539: bypass the T7 idempotency short-circuit so a re-run (e.g. with a different model)
    // creates a NEW analysis instead of returning the existing one for the same (game, pdf, prompt).
    bool ForceRegenerate = false) : ICommand<MechanicAnalysisGenerationResponseDto>;

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
