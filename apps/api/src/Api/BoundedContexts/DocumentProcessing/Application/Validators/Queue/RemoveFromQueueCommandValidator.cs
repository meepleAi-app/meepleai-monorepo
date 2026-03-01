using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators.Queue;

/// <summary>
/// Validator for RemoveFromQueueCommand.
/// Issue #4731: Queue command validation.
/// </summary>
internal sealed class RemoveFromQueueCommandValidator : AbstractValidator<RemoveFromQueueCommand>
{
    public RemoveFromQueueCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty()
            .WithMessage("Job ID is required.");
    }
}
