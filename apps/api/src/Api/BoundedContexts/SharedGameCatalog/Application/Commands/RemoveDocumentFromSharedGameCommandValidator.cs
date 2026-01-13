using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for RemoveDocumentFromSharedGameCommand.
/// </summary>
internal sealed class RemoveDocumentFromSharedGameCommandValidator : AbstractValidator<RemoveDocumentFromSharedGameCommand>
{
    public RemoveDocumentFromSharedGameCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEqual(Guid.Empty).WithMessage("SharedGameId is required");

        RuleFor(x => x.DocumentId)
            .NotEqual(Guid.Empty).WithMessage("DocumentId is required");
    }
}
