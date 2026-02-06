using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.BatchJobs;

/// <summary>
/// Command to cancel a running or queued batch job (Issue #3693)
/// </summary>
internal sealed record CancelBatchJobCommand(Guid JobId) : ICommand;

/// <summary>
/// Validator for CancelBatchJobCommand (Issue #3693)
/// </summary>
internal sealed class CancelBatchJobCommandValidator : AbstractValidator<CancelBatchJobCommand>
{
    public CancelBatchJobCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty()
            .WithMessage("Job ID is required");
    }
}
