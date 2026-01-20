using Api.BoundedContexts.UserLibrary.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for GetSharedLibraryQuery.
/// </summary>
internal sealed class GetSharedLibraryQueryValidator : AbstractValidator<GetSharedLibraryQuery>
{
    public GetSharedLibraryQueryValidator()
    {
        RuleFor(x => x.ShareToken)
            .NotEmpty()
            .WithMessage("ShareToken is required")
            .Length(32)
            .WithMessage("ShareToken must be exactly 32 characters");
    }
}
