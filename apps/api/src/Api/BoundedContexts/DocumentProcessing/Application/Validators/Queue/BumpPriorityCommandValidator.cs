using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators.Queue;

/// <summary>
/// Validator for BumpPriorityCommand.
/// Validates GUID properties are non-empty and enum is valid.
/// </summary>
internal sealed class BumpPriorityCommandValidator : AbstractValidator<BumpPriorityCommand>
{
    public BumpPriorityCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty()
            .WithMessage("Job ID is required.");

        RuleFor(x => x.NewPriority)
            .IsInEnum()
            .WithMessage("New priority must be a valid processing priority value.");
    }
}
