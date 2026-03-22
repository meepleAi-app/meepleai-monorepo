using Api.BoundedContexts.Administration.Application.Commands.PromptEvaluation;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for ComparePromptVersionsCommand.
/// Ensures all required IDs and dataset path are provided.
/// </summary>
internal sealed class ComparePromptVersionsCommandValidator : AbstractValidator<ComparePromptVersionsCommand>
{
    public ComparePromptVersionsCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty()
            .WithMessage("TemplateId is required");

        RuleFor(x => x.BaselineVersionId)
            .NotEmpty()
            .WithMessage("BaselineVersionId is required");

        RuleFor(x => x.CandidateVersionId)
            .NotEmpty()
            .WithMessage("CandidateVersionId is required");

        RuleFor(x => x.DatasetPath)
            .NotEmpty()
            .WithMessage("DatasetPath is required")
            .MaximumLength(500)
            .WithMessage("DatasetPath must not exceed 500 characters");
    }
}
