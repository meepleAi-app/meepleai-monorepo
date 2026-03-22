using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

/// <summary>
/// Validates ApproveAccessRequestCommand.
/// Ensures both request ID and admin ID are provided.
/// </summary>
internal sealed class ApproveAccessRequestCommandValidator : AbstractValidator<ApproveAccessRequestCommand>
{
    public ApproveAccessRequestCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Access request ID is required");

        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("Admin ID is required");
    }
}
