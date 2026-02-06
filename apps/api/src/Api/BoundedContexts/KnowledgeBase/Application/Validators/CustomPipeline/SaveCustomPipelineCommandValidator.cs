using Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.CustomPipeline;

/// <summary>
/// Validator for SaveCustomPipelineCommand.
/// Issue #3453: Visual RAG Strategy Builder - Validation.
/// </summary>
public sealed class SaveCustomPipelineCommandValidator : AbstractValidator<SaveCustomPipelineCommand>
{
    public SaveCustomPipelineCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Pipeline name is required")
            .MaximumLength(200).WithMessage("Pipeline name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters")
            .When(x => x.Description != null);

        RuleFor(x => x.Pipeline)
            .NotNull().WithMessage("Pipeline definition is required");

        RuleFor(x => x.Pipeline.Nodes)
            .NotEmpty().WithMessage("Pipeline must have at least one node")
            .When(x => x.Pipeline != null);

        RuleFor(x => x.Pipeline.Edges)
            .NotNull().WithMessage("Pipeline edges are required")
            .When(x => x.Pipeline != null);

        RuleFor(x => x.Pipeline.EntryPoint)
            .NotEmpty().WithMessage("Pipeline must have an entry point")
            .When(x => x.Pipeline != null);

        RuleFor(x => x.Pipeline.ExitPoints)
            .NotEmpty().WithMessage("Pipeline must have at least one exit point")
            .When(x => x.Pipeline != null);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Tags)
            .Must(tags => tags.All(t => t.Length <= 50))
            .WithMessage("Each tag must not exceed 50 characters")
            .When(x => x.Tags.Length > 0);
    }
}
