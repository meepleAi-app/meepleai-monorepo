using Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Keeps an inverted date window rejected on <c>GET /api/v1/admin/rag-executions</c>.
/// </summary>
/// <remarks>
/// Until the window was normalized there was no validator here at all: an inverted range was
/// rejected only because it hit the same Npgsql <c>Kind=Unspecified</c> failure as every other
/// dated request (400 on <c>dateFrom=2026-10-05&amp;dateTo=2026-10-04</c>, measured 2026-10-04 —
/// identical response to the valid windows next to it). Normalizing the kind would have removed
/// that accidental rejection along with the defect, so the rule is stated here instead. It reads
/// the normalized bounds, which is what keeps the degenerate <c>dateFrom == dateTo</c> window — the
/// inspector's default "today" — valid: it normalizes to one whole day, not to an empty instant.
/// </remarks>
internal sealed class GetRagExecutionsQueryValidator : AbstractValidator<GetRagExecutionsQuery>
{
    public GetRagExecutionsQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => RagExecutionDateWindow.IsOrdered(x.From, x.To))
            .WithMessage("dateTo must be greater than or equal to dateFrom");
    }
}

/// <summary>
/// Keeps an inverted date window rejected on <c>GET /api/v1/admin/rag-executions/stats</c>,
/// which takes the same window as the list endpoint. See
/// <see cref="GetRagExecutionsQueryValidator"/>.
/// </summary>
internal sealed class GetRagExecutionStatsQueryValidator : AbstractValidator<GetRagExecutionStatsQuery>
{
    public GetRagExecutionStatsQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => RagExecutionDateWindow.IsOrdered(x.From, x.To))
            .WithMessage("dateTo must be greater than or equal to dateFrom");
    }
}
