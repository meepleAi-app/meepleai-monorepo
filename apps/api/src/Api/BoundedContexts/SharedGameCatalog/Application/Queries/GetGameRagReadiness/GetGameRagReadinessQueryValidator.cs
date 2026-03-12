using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameRagReadiness;

/// <summary>
/// Validator for GetGameRagReadinessQuery.
/// </summary>
public sealed class GetGameRagReadinessQueryValidator : AbstractValidator<GetGameRagReadinessQuery>
{
    public GetGameRagReadinessQueryValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");
    }
}
