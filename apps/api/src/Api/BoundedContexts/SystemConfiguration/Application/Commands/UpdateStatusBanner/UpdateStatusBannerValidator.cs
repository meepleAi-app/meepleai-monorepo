using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateStatusBanner;

internal sealed class UpdateStatusBannerValidator : AbstractValidator<UpdateStatusBannerCommand>
{
    public UpdateStatusBannerValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .When(x => x.IsActive)
            .WithMessage("Message is required when banner is active");

        RuleFor(x => x.Message)
            .MaximumLength(IncidentBannerState.MaxMessageLength)
            .WithMessage($"Message must not exceed {IncidentBannerState.MaxMessageLength} characters");

        RuleFor(x => x.Severity)
            .Must(s => Enum.TryParse<BannerSeverity>(s, ignoreCase: true, out _))
            .WithMessage("Severity must be one of: Info, Warning, Critical");

        RuleFor(x => x)
            .Must(cmd => !(cmd.StartsAt.HasValue && cmd.EndsAt.HasValue && cmd.StartsAt.Value >= cmd.EndsAt.Value))
            .WithMessage("StartsAt must be earlier than EndsAt");
    }
}
