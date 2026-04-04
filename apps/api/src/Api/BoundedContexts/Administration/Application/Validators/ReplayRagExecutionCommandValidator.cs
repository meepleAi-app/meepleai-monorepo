using Api.BoundedContexts.Administration.Application.Commands.RagExecution;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for ReplayRagExecutionCommand.
/// Ensures execution ID and user ID are valid, with optional parameter bounds.
/// </summary>
internal sealed class ReplayRagExecutionCommandValidator : AbstractValidator<ReplayRagExecutionCommand>
{
    public ReplayRagExecutionCommandValidator()
    {
        RuleFor(x => x.ExecutionId)
            .NotEmpty()
            .WithMessage("ExecutionId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Strategy)
            .MaximumLength(100)
            .WithMessage("Strategy must not exceed 100 characters")
            .When(x => x.Strategy != null);

        RuleFor(x => x.TopK)
            .InclusiveBetween(1, 100)
            .WithMessage("TopK must be between 1 and 100")
            .When(x => x.TopK.HasValue);

        RuleFor(x => x.Model)
            .MaximumLength(200)
            .WithMessage("Model must not exceed 200 characters")
            .When(x => x.Model != null);

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0.0, 2.0)
            .WithMessage("Temperature must be between 0.0 and 2.0")
            .When(x => x.Temperature.HasValue);
    }
}
