using Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for UpdateCustomPipelineCommand.
/// </summary>
internal sealed class UpdateCustomPipelineCommandValidator : AbstractValidator<UpdateCustomPipelineCommand>
{
    public UpdateCustomPipelineCommandValidator()
    {
        RuleFor(x => x.PipelineId)
            .NotEmpty()
            .WithMessage("PipelineId is required");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(200)
            .WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description != null)
            .WithMessage("Description cannot exceed 500 characters");

        RuleFor(x => x.Pipeline)
            .NotNull()
            .WithMessage("Pipeline is required");

        RuleForEach(x => x.Tags)
            .NotEmpty()
            .WithMessage("Tag cannot be empty")
            .MaximumLength(100)
            .WithMessage("Tag cannot exceed 100 characters");
    }
}
