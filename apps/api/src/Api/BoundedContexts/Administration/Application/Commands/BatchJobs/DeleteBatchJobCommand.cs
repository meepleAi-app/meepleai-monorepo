using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.BatchJobs;

/// <summary>
/// Command to delete a batch job (Issue #3693)
/// </summary>
internal sealed record DeleteBatchJobCommand(Guid JobId) : ICommand;

/// <summary>
/// Validator for DeleteBatchJobCommand (Issue #3693)
/// </summary>
internal sealed class DeleteBatchJobCommandValidator : AbstractValidator<DeleteBatchJobCommand>
{
    public DeleteBatchJobCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty()
            .WithMessage("Job ID is required");
    }
}
