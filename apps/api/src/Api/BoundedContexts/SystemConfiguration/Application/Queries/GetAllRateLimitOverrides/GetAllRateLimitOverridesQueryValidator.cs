using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.GetAllRateLimitOverrides;

/// <summary>
/// Validator for GetAllRateLimitOverridesQuery.
/// Ensures pagination parameters are within acceptable ranges.
/// </summary>
internal sealed class GetAllRateLimitOverridesQueryValidator : AbstractValidator<GetAllRateLimitOverridesQuery>
{
    public GetAllRateLimitOverridesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
    }
}
