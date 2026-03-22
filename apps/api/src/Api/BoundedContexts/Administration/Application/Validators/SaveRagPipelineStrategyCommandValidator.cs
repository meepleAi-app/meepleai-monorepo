using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for SaveRagPipelineStrategyCommand.
/// Ensures name, description, and JSON data are valid.
/// </summary>
internal sealed class SaveRagPipelineStrategyCommandValidator : AbstractValidator<SaveRagPipelineStrategyCommand>
{
    public SaveRagPipelineStrategyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(200)
            .WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required")
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters");

        RuleFor(x => x.NodesJson)
            .NotEmpty()
            .WithMessage("NodesJson is required")
            .MaximumLength(100000)
            .WithMessage("NodesJson must not exceed 100000 characters");

        RuleFor(x => x.EdgesJson)
            .NotEmpty()
            .WithMessage("EdgesJson is required")
            .MaximumLength(100000)
            .WithMessage("EdgesJson must not exceed 100000 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleForEach(x => x.Tags)
            .MaximumLength(50)
            .WithMessage("Each tag must not exceed 50 characters")
            .When(x => x.Tags != null);
    }
}
