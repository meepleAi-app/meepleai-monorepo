using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Queries.ListEvaluations;

/// <summary>
/// Paginated history of evaluation runs for a single doc (#1675 Task 16).
/// Ordered by <c>StartedAt DESC</c> via the repository projection so the most recent
/// run appears first. <see cref="Page"/> is 1-based; out-of-range inputs are clamped
/// inside the handler (page ≥ 1, pageSize ∈ [1,100]).
/// </summary>
public sealed record ListEvaluationsQuery(
    Guid DocId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedEvaluationsDto>;
