using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for BulkRejectShareRequestsCommand.
/// Issue #2893: Enforces max 20 share requests per batch and reason requirement.
/// </summary>
internal sealed class BulkRejectShareRequestsCommandValidator : AbstractValidator<BulkRejectShareRequestsCommand>
{
    private const int MaxBulkSize = 20;

    public BulkRejectShareRequestsCommandValidator()
    {
        RuleFor(x => x.ShareRequestIds)
            .NotNull()
            .WithMessage("ShareRequestIds cannot be null")
            .NotEmpty()
            .WithMessage("ShareRequestIds cannot be empty")
            .Must(ids => ids.Count <= MaxBulkSize)
            .WithMessage($"Bulk operation exceeds maximum limit of {MaxBulkSize} share requests");

        RuleFor(x => x.EditorId)
            .NotEmpty()
            .WithMessage("EditorId is required");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Reason is required for bulk rejection")
            .MaximumLength(2000)
            .WithMessage("Reason cannot exceed 2000 characters");
    }
}
