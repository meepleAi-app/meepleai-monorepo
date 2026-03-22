using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators.Queue;

/// <summary>
/// Validator for BulkReindexFailedCommand.
/// Validates GUID properties are non-empty.
/// </summary>
internal sealed class BulkReindexFailedCommandValidator : AbstractValidator<BulkReindexFailedCommand>
{
    public BulkReindexFailedCommandValidator()
    {
        RuleFor(x => x.RequestedBy)
            .NotEmpty()
            .WithMessage("Requested by user ID is required.");
    }
}
