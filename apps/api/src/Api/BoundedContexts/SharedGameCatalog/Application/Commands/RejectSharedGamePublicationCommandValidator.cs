using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for RejectSharedGamePublicationCommand.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal sealed class RejectSharedGamePublicationCommandValidator : AbstractValidator<RejectSharedGamePublicationCommand>
{
    public RejectSharedGamePublicationCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.RejectedBy)
            .NotEmpty().WithMessage("RejectedBy is required");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required")
            .MaximumLength(1000).WithMessage("Rejection reason cannot exceed 1000 characters");
    }
}
