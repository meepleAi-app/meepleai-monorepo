using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Validator for UnlinkAgentFromPrivateGameCommand.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal sealed class UnlinkAgentFromPrivateGameCommandValidator : AbstractValidator<UnlinkAgentFromPrivateGameCommand>
{
    public UnlinkAgentFromPrivateGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");
    }
}
