using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for InvalidateCacheByTagCommand.
/// </summary>
internal sealed class InvalidateCacheByTagCommandValidator : AbstractValidator<InvalidateCacheByTagCommand>
{
    public InvalidateCacheByTagCommandValidator()
    {
        RuleFor(x => x.Tag)
            .NotEmpty()
            .WithMessage("Tag is required")
            .MaximumLength(200)
            .WithMessage("Tag cannot exceed 200 characters");
    }
}
