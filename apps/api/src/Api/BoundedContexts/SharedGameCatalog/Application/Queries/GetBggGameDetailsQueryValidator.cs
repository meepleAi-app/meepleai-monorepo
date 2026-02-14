using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Validator for GetBggGameDetailsQuery.
/// Ensures BGG ID is valid and positive.
/// Issue #4139: Backend - API Endpoints PDF Wizard
/// </summary>
internal sealed class GetBggGameDetailsQueryValidator : AbstractValidator<GetBggGameDetailsQuery>
{
    public GetBggGameDetailsQueryValidator()
    {
        RuleFor(x => x.BggId)
            .GreaterThan(0)
            .WithMessage("BGG ID must be a positive integer");
    }
}
