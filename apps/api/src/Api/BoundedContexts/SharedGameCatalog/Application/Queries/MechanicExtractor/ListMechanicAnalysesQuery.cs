using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Paged listing of recent mechanic analyses for the admin discovery page
/// (spec-panel gap #2 / ADR-051 M1.2). Bypasses the global <c>IsSuppressed</c> query
/// filter so moderators can find suppressed rows from the index, mirroring the behavior
/// of the per-id status query.
/// </summary>
/// <param name="Page">1-based page number. Values &lt; 1 are clamped to 1.</param>
/// <param name="PageSize">Number of items per page. Values &lt; 1 are clamped to 1, values
/// &gt; <see cref="MaxPageSize"/> are clamped down to keep the response bounded.</param>
internal sealed record ListMechanicAnalysesQuery(int Page, int PageSize)
    : IQuery<MechanicAnalysisListPageDto>
{
    /// <summary>Hard upper bound on <c>PageSize</c>. The handler enforces this.</summary>
    public const int MaxPageSize = 100;
}
