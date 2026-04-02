using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;

/// <summary>
/// Validator for RemoveDocumentFromKbCommand.
/// </summary>
internal sealed class RemoveDocumentFromKbCommandValidator : AbstractValidator<RemoveDocumentFromKbCommand>
{
    public RemoveDocumentFromKbCommandValidator()
    {
        RuleFor(x => x.VectorDocumentId).NotEmpty();
        RuleFor(x => x.GameId).NotEmpty();
    }
}
