using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators.Queue;

/// <summary>
/// Validator for RetryJobCommand.
/// Issue #4731: Queue command validation.
/// </summary>
internal sealed class RetryJobCommandValidator : AbstractValidator<RetryJobCommand>
{
    public RetryJobCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty()
            .WithMessage("Job ID is required.");
    }
}
