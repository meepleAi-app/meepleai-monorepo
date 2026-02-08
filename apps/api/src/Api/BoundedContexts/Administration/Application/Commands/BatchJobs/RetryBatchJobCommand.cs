using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.BatchJobs;

/// <summary>
/// Command to retry a failed batch job (Issue #3693)
/// </summary>
internal sealed record RetryBatchJobCommand(Guid JobId) : ICommand;

/// <summary>
/// Validator for RetryBatchJobCommand (Issue #3693)
/// </summary>
internal sealed class RetryBatchJobCommandValidator : AbstractValidator<RetryBatchJobCommand>
{
    public RetryBatchJobCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty()
            .WithMessage("Job ID is required");
    }
}
