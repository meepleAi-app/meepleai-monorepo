using Api.BoundedContexts.Administration.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for GetUserDashboardQuery (Issue #2854).
/// Ensures UserId is valid before processing.
/// </summary>
internal sealed class GetUserDashboardQueryValidator : AbstractValidator<GetUserDashboardQuery>
{
    public GetUserDashboardQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
