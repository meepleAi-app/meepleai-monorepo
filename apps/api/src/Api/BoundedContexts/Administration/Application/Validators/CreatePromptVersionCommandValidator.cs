using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for CreatePromptVersionCommand.
/// Ensures template ID, content, and user ID are valid.
/// </summary>
internal sealed class CreatePromptVersionCommandValidator : AbstractValidator<CreatePromptVersionCommand>
{
    public CreatePromptVersionCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("TemplateId is required");

        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required")
            .MaximumLength(50000)
            .WithMessage("Content must not exceed 50000 characters");

        RuleFor(x => x.Metadata)
            .MaximumLength(10000)
            .WithMessage("Metadata must not exceed 10000 characters")
            .When(x => x.Metadata != null);

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty()
            .WithMessage("CreatedByUserId is required");
    }
}
