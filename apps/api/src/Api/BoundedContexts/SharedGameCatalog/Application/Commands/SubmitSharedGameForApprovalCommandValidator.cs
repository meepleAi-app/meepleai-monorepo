using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for SubmitSharedGameForApprovalCommand.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal sealed class SubmitSharedGameForApprovalCommandValidator : AbstractValidator<SubmitSharedGameForApprovalCommand>
{
    public SubmitSharedGameForApprovalCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.SubmittedBy)
            .NotEmpty().WithMessage("SubmittedBy is required");
    }
}
