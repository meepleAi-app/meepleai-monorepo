using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Validator for CheckDuplicateGameQuery.
/// Issue #4158: Backend - Duplicate Detection Enhancement
/// </summary>
internal sealed class CheckDuplicateGameQueryValidator : AbstractValidator<CheckDuplicateGameQuery>
{
    public CheckDuplicateGameQueryValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required for duplicate detection")
            .MaximumLength(500)
            .WithMessage("Title must not exceed 500 characters");

        RuleFor(x => x.BggId)
            .GreaterThan(0)
            .When(x => x.BggId.HasValue)
            .WithMessage("BggId must be a positive integer when provided");
    }
}
