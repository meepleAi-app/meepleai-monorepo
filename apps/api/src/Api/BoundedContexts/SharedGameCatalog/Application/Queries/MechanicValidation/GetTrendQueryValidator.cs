using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Validator for <see cref="GetTrendQuery"/>. Enforces that the target shared game id is
/// non-empty and that the requested page size lies in the [1, 100] range.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 30. The 100-row upper bound prevents abusive
/// page sizes on a polling admin page; the typical default is 20.
/// </remarks>
internal sealed class GetTrendQueryValidator : AbstractValidator<GetTrendQuery>
{
    public GetTrendQueryValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty().WithMessage("SharedGameId is required");

        RuleFor(x => x.Take)
            .InclusiveBetween(1, 100)
                .WithMessage("Take must be between 1 and 100");
    }
}
