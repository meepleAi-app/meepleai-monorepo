using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetSystemPrompt;

/// <summary>
/// Query for the marketplace toolkit's agent system-prompt surface
/// (issue #822 — Phase 5 PR-2 / spec-panel 2026-05-18 §2).
/// </summary>
/// <param name="ToolkitId">Marketplace toolkit id.</param>
/// <param name="ViewerId">
/// Authenticated caller — gates between owner (full prompt) and public
/// (mode + char count only) projections per Wiegers DTO shape decision.
/// </param>
/// <remarks>
/// Returns <c>null</c> when the toolkit is missing or hidden from the viewer
/// (same security boundary as <c>GetToolkitDetail</c>: non-authors only see
/// published+non-yanked). Endpoint maps <c>null</c> to 404.
/// </remarks>
internal sealed record GetSystemPromptQuery(
    Guid ToolkitId,
    Guid ViewerId) : IQuery<SystemPromptResponse?>;
