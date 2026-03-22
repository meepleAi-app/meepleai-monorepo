using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for UpdateQueueConfigCommand.
/// Ensures UpdatedBy is non-empty and MaxConcurrentWorkers is within a valid range when provided.
/// </summary>
internal sealed class UpdateQueueConfigCommandValidator : AbstractValidator<UpdateQueueConfigCommand>
{
    public UpdateQueueConfigCommandValidator()
    {
        RuleFor(x => x.UpdatedBy)
            .NotEmpty()
            .WithMessage("UpdatedBy is required.");

        RuleFor(x => x.MaxConcurrentWorkers)
            .InclusiveBetween(1, 20)
            .When(x => x.MaxConcurrentWorkers.HasValue)
            .WithMessage("MaxConcurrentWorkers must be between 1 and 20.");
    }
}
