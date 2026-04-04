using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

internal class LinkExistingKbToGameCommandValidator
    : AbstractValidator<LinkExistingKbToGameCommand>
{
    public LinkExistingKbToGameCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");

        RuleFor(x => x.TargetGameId)
            .NotEmpty().WithMessage("TargetGameId is required");

        RuleFor(x => x.SourcePdfDocumentId)
            .NotEmpty().WithMessage("SourcePdfDocumentId is required");
    }
}
