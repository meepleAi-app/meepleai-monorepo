using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

/// <summary>
/// Validates BulkApproveAccessRequestsCommand.
/// Ensures IDs list is provided with reasonable limits and admin ID is valid.
/// </summary>
internal sealed class BulkApproveAccessRequestsCommandValidator : AbstractValidator<BulkApproveAccessRequestsCommand>
{
    private const int MaxBulkItems = 100;

    public BulkApproveAccessRequestsCommandValidator()
    {
        RuleFor(x => x.Ids)
            .NotNull()
            .WithMessage("Access request IDs list is required")
            .Must(ids => ids != null && ids.Count > 0)
            .WithMessage("At least one access request ID must be provided")
            .Must(ids => ids == null || ids.Count <= MaxBulkItems)
            .WithMessage($"Cannot approve more than {MaxBulkItems} requests at once");

        RuleForEach(x => x.Ids)
            .NotEmpty()
            .WithMessage("Each access request ID must be a valid non-empty GUID");

        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("Admin ID is required");
    }
}
