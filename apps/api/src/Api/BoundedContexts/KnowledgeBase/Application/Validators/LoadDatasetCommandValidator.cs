using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for LoadDatasetCommand.
/// </summary>
internal sealed class LoadDatasetCommandValidator : AbstractValidator<LoadDatasetCommand>
{
    public LoadDatasetCommandValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty()
            .WithMessage("FilePath is required")
            .MaximumLength(500)
            .WithMessage("FilePath cannot exceed 500 characters");
    }
}
