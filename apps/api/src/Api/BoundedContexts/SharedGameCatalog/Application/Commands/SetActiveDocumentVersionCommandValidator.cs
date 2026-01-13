using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for SetActiveDocumentVersionCommand.
/// </summary>
internal sealed class SetActiveDocumentVersionCommandValidator : AbstractValidator<SetActiveDocumentVersionCommand>
{
    public SetActiveDocumentVersionCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEqual(Guid.Empty).WithMessage("SharedGameId is required");

        RuleFor(x => x.DocumentId)
            .NotEqual(Guid.Empty).WithMessage("DocumentId is required");
    }
}
