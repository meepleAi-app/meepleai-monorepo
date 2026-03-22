using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

/// <summary>
/// Validates RejectAccessRequestCommand.
/// Ensures request ID and admin ID are provided, and optional reason has length limits.
/// </summary>
internal sealed class RejectAccessRequestCommandValidator : AbstractValidator<RejectAccessRequestCommand>
{
    public RejectAccessRequestCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Access request ID is required");

        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("Admin ID is required");

        RuleFor(x => x.Reason)
            .MaximumLength(1000)
            .WithMessage("Rejection reason must not exceed 1000 characters")
            .When(x => x.Reason != null);
    }
}
