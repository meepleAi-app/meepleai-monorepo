using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for DeleteChatSessionCommand.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class DeleteChatSessionCommandValidator : AbstractValidator<DeleteChatSessionCommand>
{
    public DeleteChatSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");
    }
}
