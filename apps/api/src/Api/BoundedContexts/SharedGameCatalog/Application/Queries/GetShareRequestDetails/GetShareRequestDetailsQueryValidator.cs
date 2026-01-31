using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;

/// <summary>
/// Validator for GetShareRequestDetailsQuery.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
internal sealed class GetShareRequestDetailsQueryValidator : AbstractValidator<GetShareRequestDetailsQuery>
{
    public GetShareRequestDetailsQueryValidator()
    {
        RuleFor(x => x.ShareRequestId)
            .NotEmpty()
            .WithMessage("ShareRequestId is required");

        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("AdminId is required");
    }
}
