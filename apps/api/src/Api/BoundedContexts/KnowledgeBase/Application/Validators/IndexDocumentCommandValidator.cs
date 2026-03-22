using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for IndexDocumentCommand.
/// </summary>
internal sealed class IndexDocumentCommandValidator : AbstractValidator<IndexDocumentCommand>
{
    public IndexDocumentCommandValidator()
    {
        RuleFor(x => x.PdfDocumentId)
            .NotEmpty()
            .WithMessage("PdfDocumentId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.Language)
            .NotEmpty()
            .WithMessage("Language is required")
            .MaximumLength(10)
            .WithMessage("Language cannot exceed 10 characters");
    }
}
