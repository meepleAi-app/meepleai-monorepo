using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestHistory;

/// <summary>
/// Validator for GetShareRequestHistoryQuery.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
internal sealed class GetShareRequestHistoryQueryValidator : AbstractValidator<GetShareRequestHistoryQuery>
{
    public GetShareRequestHistoryQueryValidator()
    {
        RuleFor(x => x.ShareRequestId)
            .NotEmpty()
            .WithMessage("ShareRequestId is required");
    }
}
