using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Validator for CheckBggDuplicateQuery.
/// </summary>
internal sealed class CheckBggDuplicateQueryValidator : AbstractValidator<CheckBggDuplicateQuery>
{
    public CheckBggDuplicateQueryValidator()
    {
        RuleFor(x => x.BggId)
            .GreaterThan(0)
            .WithMessage("BGG ID must be a positive integer");
    }
}
