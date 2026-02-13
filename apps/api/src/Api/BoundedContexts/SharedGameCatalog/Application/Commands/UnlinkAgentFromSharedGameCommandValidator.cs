using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UnlinkAgentFromSharedGameCommand.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
internal sealed class UnlinkAgentFromSharedGameCommandValidator : AbstractValidator<UnlinkAgentFromSharedGameCommand>
{
    public UnlinkAgentFromSharedGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");
    }
}
