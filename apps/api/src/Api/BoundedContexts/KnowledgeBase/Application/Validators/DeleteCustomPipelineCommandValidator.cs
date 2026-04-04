using Api.BoundedContexts.KnowledgeBase.Application.Commands.CustomPipeline;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for DeleteCustomPipelineCommand.
/// </summary>
internal sealed class DeleteCustomPipelineCommandValidator : AbstractValidator<DeleteCustomPipelineCommand>
{
    public DeleteCustomPipelineCommandValidator()
    {
        RuleFor(x => x.PipelineId)
            .NotEmpty()
            .WithMessage("PipelineId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
