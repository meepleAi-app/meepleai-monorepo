using Api.BoundedContexts.GameManagement.Application.Queries.GameSessionContext;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// FluentValidation validator for GetGameSessionContextQuery.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
internal sealed class GetGameSessionContextQueryValidator : AbstractValidator<GetGameSessionContextQuery>
{
    public GetGameSessionContextQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");
    }
}

/// <summary>
/// FluentValidation validator for RefreshGameSessionContextQuery.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
internal sealed class RefreshGameSessionContextQueryValidator : AbstractValidator<RefreshGameSessionContextQuery>
{
    public RefreshGameSessionContextQueryValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required.");
    }
}
