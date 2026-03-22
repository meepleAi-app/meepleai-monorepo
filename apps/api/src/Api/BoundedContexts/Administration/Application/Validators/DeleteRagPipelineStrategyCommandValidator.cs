using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for DeleteRagPipelineStrategyCommand.
/// Ensures strategy ID and user ID are valid.
/// </summary>
internal sealed class DeleteRagPipelineStrategyCommandValidator : AbstractValidator<DeleteRagPipelineStrategyCommand>
{
    public DeleteRagPipelineStrategyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Strategy ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
