using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Validates <see cref="StepUpTwoFactorCommand"/>. Keeps format validation minimal — the
/// authoritative code check (and constant-time comparison) is in <c>TotpService</c>; over-tight
/// format rules here would leak timing/shape information and duplicate that logic.
/// SP5 Admin Security S3 — T5.
/// </summary>
internal sealed class StepUpTwoFactorCommandValidator : AbstractValidator<StepUpTwoFactorCommand>
{
    public StepUpTwoFactorCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(10); // 6-digit TOTP; bounded to reject obviously malformed input
    }
}
