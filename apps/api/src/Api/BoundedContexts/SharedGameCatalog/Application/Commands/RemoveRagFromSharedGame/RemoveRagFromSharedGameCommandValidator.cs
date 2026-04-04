using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;

/// <summary>
/// Validator for RemoveRagFromSharedGameCommand.
/// </summary>
internal sealed class RemoveRagFromSharedGameCommandValidator : AbstractValidator<RemoveRagFromSharedGameCommand>
{
    public RemoveRagFromSharedGameCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty()
            .WithMessage("SharedGameId is required");

        RuleFor(x => x.SharedGameDocumentId)
            .NotEmpty()
            .WithMessage("SharedGameDocumentId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
