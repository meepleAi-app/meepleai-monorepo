using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for <c>ImpersonationStartCommand</c> (SP5 Admin Security S2 — T3).
/// Structural validation only — role/target eligibility (D-S2-1) is enforced in the handler
/// where the user entities are loaded.
/// </summary>
internal sealed class ImpersonationStartCommandValidator : AbstractValidator<ImpersonationStartCommand>
{
    private const int MinDurationMinutes = 5;
    private const int MaxDurationMinutes = 60;

    public ImpersonationStartCommandValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty()
            .WithMessage("TargetUserId is required");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty()
            .WithMessage("RequestingUserId is required");

        RuleFor(x => x)
            .Must(x => x.TargetUserId != x.RequestingUserId)
            .WithMessage("Cannot impersonate yourself");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(500)
            .WithMessage("Reason is required (10-500 chars) for the audit trail");

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(MinDurationMinutes, MaxDurationMinutes)
            .WithMessage($"DurationMinutes must be between {MinDurationMinutes} and {MaxDurationMinutes}");
    }
}
