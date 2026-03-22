using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for CreatePromptTemplateCommand.
/// Ensures name and initial content are provided with sensible limits.
/// </summary>
internal sealed class CreatePromptTemplateCommandValidator : AbstractValidator<CreatePromptTemplateCommand>
{
    public CreatePromptTemplateCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(200)
            .WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters")
            .When(x => x.Description != null);

        RuleFor(x => x.Category)
            .MaximumLength(100)
            .WithMessage("Category must not exceed 100 characters")
            .When(x => x.Category != null);

        RuleFor(x => x.InitialContent)
            .NotEmpty()
            .WithMessage("InitialContent is required")
            .MaximumLength(50000)
            .WithMessage("InitialContent must not exceed 50000 characters");

        RuleFor(x => x.Metadata)
            .MaximumLength(10000)
            .WithMessage("Metadata must not exceed 10000 characters")
            .When(x => x.Metadata != null);

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty()
            .WithMessage("CreatedByUserId is required");
    }
}
