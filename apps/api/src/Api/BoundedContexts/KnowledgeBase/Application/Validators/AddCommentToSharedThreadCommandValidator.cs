using Api.BoundedContexts.KnowledgeBase.Application.Commands.AddCommentToSharedThread;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for AddCommentToSharedThreadCommand.
/// </summary>
internal sealed class AddCommentToSharedThreadCommandValidator : AbstractValidator<AddCommentToSharedThreadCommand>
{
    public AddCommentToSharedThreadCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Token is required");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required")
            .MaximumLength(2000)
            .WithMessage("Content cannot exceed 2000 characters");
    }
}
