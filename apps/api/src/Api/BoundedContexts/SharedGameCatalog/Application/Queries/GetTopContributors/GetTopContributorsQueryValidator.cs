using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetTopContributors;

/// <summary>
/// Validator for <see cref="GetTopContributorsQuery"/>.
/// Issue #593 (Wave A.3a) — spec §5.4: <c>limit</c> is bounded 1..20 to keep the
/// public, anonymous endpoint cheap (single cache key per allowed limit, capped
/// fan-out on the contributor query).
/// </summary>
internal sealed class GetTopContributorsQueryValidator : AbstractValidator<GetTopContributorsQuery>
{
    public GetTopContributorsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 20)
            .WithMessage("Limit must be between 1 and 20.");
    }
}
