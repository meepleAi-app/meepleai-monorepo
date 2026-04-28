using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Validator for <see cref="RespondToGameNightInvitationByTokenCommand"/>.
/// Restricts <c>Response</c> to terminal user-actionable values; lifecycle
/// transitions like Expired/Cancelled are managed inside the aggregate.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
internal sealed class RespondToGameNightInvitationByTokenCommandValidator
    : AbstractValidator<RespondToGameNightInvitationByTokenCommand>
{
    public RespondToGameNightInvitationByTokenCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token is required")
            .Length(22).WithMessage("Token must be exactly 22 characters")
            .Matches("^[0-9A-Za-z]{22}$")
                .WithMessage("Token must be base62 (digits and ASCII letters only)");

        RuleFor(x => x.Response)
            .Must(r => r == GameNightInvitationStatus.Accepted
                       || r == GameNightInvitationStatus.Declined)
            .WithMessage("Response must be either Accepted or Declined");
    }
}
