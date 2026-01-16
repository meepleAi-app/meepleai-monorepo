using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for ApproveSharedGamePublicationCommand.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal sealed class ApproveSharedGamePublicationCommandValidator : AbstractValidator<ApproveSharedGamePublicationCommand>
{
    public ApproveSharedGamePublicationCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.ApprovedBy)
            .NotEmpty().WithMessage("ApprovedBy is required");
    }
}
