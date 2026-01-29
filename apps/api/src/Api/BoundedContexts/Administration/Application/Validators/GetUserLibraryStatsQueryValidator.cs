using Api.BoundedContexts.Administration.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for GetUserLibraryStatsQuery.
/// Issue #3139 - Ensures valid user ID.
/// </summary>
internal sealed class GetUserLibraryStatsQueryValidator
    : AbstractValidator<GetUserLibraryStatsQuery>
{
    public GetUserLibraryStatsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required")
            .NotEqual(Guid.Empty)
            .WithMessage("UserId cannot be empty GUID");
    }
}
