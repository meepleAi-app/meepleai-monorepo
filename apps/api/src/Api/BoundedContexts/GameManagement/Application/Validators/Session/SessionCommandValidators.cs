using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.Session;

/// <summary>
/// Validator for ConfirmScoreProposalCommand.
/// Ensures all GUID properties are non-empty and score fields are valid.
/// </summary>
internal sealed class ConfirmScoreProposalCommandValidator : AbstractValidator<ConfirmScoreProposalCommand>
{
    public ConfirmScoreProposalCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.ConfirmingUserId)
            .NotEmpty().WithMessage("Confirming user ID is required");

        RuleFor(x => x.TargetPlayerId)
            .NotEmpty().WithMessage("Target player ID is required");

        RuleFor(x => x.Round)
            .GreaterThan(0).WithMessage("Round must be greater than 0");

        RuleFor(x => x.Dimension)
            .NotEmpty().WithMessage("Dimension is required")
            .MaximumLength(200).WithMessage("Dimension must not exceed 200 characters");

        RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(0).WithMessage("Value must be zero or greater");
    }
}

/// <summary>
/// Validator for CreateSessionInviteCommand.
/// Ensures SessionId and UserId are non-empty and invite parameters are valid.
/// </summary>
internal sealed class CreateSessionInviteCommandValidator : AbstractValidator<CreateSessionInviteCommand>
{
    public CreateSessionInviteCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.MaxUses)
            .GreaterThan(0).WithMessage("Max uses must be greater than 0")
            .LessThanOrEqualTo(1000).WithMessage("Max uses must not exceed 1000");

        RuleFor(x => x.ExpiryMinutes)
            .GreaterThan(0).WithMessage("Expiry minutes must be greater than 0")
            .LessThanOrEqualTo(10080).WithMessage("Expiry minutes must not exceed 10080 (7 days)");
    }
}

/// <summary>
/// Validator for JoinSessionCommand.
/// Ensures Token is provided and GuestName is within bounds when set.
/// </summary>
internal sealed class JoinSessionCommandValidator : AbstractValidator<JoinSessionCommand>
{
    public JoinSessionCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required")
            .MaximumLength(500).WithMessage("Token must not exceed 500 characters");

        RuleFor(x => x.GuestName)
            .MaximumLength(200).WithMessage("Guest name must not exceed 200 characters")
            .When(x => x.GuestName is not null);
    }
}

/// <summary>
/// Validator for ProposeScoreCommand.
/// Ensures all GUID properties are non-empty and score fields are valid.
/// </summary>
internal sealed class ProposeScoreCommandValidator : AbstractValidator<ProposeScoreCommand>
{
    public ProposeScoreCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.ProposingParticipantId)
            .NotEmpty().WithMessage("Proposing participant ID is required");

        RuleFor(x => x.TargetPlayerId)
            .NotEmpty().WithMessage("Target player ID is required");

        RuleFor(x => x.Round)
            .GreaterThan(0).WithMessage("Round must be greater than 0");

        RuleFor(x => x.Dimension)
            .NotEmpty().WithMessage("Dimension is required")
            .MaximumLength(200).WithMessage("Dimension must not exceed 200 characters");

        RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(0).WithMessage("Value must be zero or greater");

        RuleFor(x => x.ProposerName)
            .MaximumLength(200).WithMessage("Proposer name must not exceed 200 characters")
            .When(x => x.ProposerName is not null);
    }
}
