using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Validates BatchProvisionCommand.
/// Ensures invitations list is provided with reasonable limits.
/// </summary>
internal sealed class BatchProvisionCommandValidator : AbstractValidator<BatchProvisionCommand>
{
    private const int MaxItems = 100;

    public BatchProvisionCommandValidator()
    {
        RuleFor(x => x.Invitations)
            .NotNull()
            .WithMessage("Invitations list is required")
            .Must(list => list != null && list.Count > 0)
            .WithMessage("At least one invitation must be provided")
            .Must(list => list == null || list.Count <= MaxItems)
            .WithMessage($"Cannot provision more than {MaxItems} invitations at once");

        RuleFor(x => x.InvitedByUserId)
            .NotEmpty()
            .WithMessage("Inviting user ID is required");
    }
}
