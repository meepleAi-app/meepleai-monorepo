using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for RenameChatSessionCommand.
/// Issue #905: SG4 — Sessions CRUD naming disambiguation.
/// </summary>
internal sealed class RenameChatSessionCommandValidator : AbstractValidator<RenameChatSessionCommand>
{
    public RenameChatSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .MaximumLength(200)
            .WithMessage("Title cannot exceed 200 characters");
    }
}
