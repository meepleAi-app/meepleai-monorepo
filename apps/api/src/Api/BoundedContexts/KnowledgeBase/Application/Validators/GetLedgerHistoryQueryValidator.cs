using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for GetLedgerHistoryQuery.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
internal sealed class GetLedgerHistoryQueryValidator : AbstractValidator<GetLedgerHistoryQuery>
{
    public GetLedgerHistoryQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.Limit)
            .GreaterThan(0)
            .WithMessage("Limit must be greater than 0")
            .LessThanOrEqualTo(200)
            .WithMessage("Limit cannot exceed 200 to prevent performance issues");
    }
}
