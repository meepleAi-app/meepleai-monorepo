using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class SendManualNotificationCommandValidator : AbstractValidator<SendManualNotificationCommand>
{
    private static readonly string[] ValidRecipientTypes = ["all", "role", "users"];
    private static readonly string[] ValidChannels = ["inapp", "email"];

    public SendManualNotificationCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Channels).NotEmpty()
            .Must(c => c.All(ch => ValidChannels.Contains(ch, StringComparer.OrdinalIgnoreCase)))
            .WithMessage("Channels must be: inapp, email");
        RuleFor(x => x.RecipientType).NotEmpty()
            .Must(rt => ValidRecipientTypes.Contains(rt, StringComparer.OrdinalIgnoreCase))
            .WithMessage("RecipientType must be: all, role, or users");
        RuleFor(x => x.RecipientRole)
            .NotEmpty()
            .When(x => string.Equals(x.RecipientType, "role", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.RecipientUserIds)
            .NotEmpty()
            .When(x => string.Equals(x.RecipientType, "users", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.SentByAdminId).NotEmpty();
        RuleFor(x => x.SentByAdminName).NotEmpty();
    }
}
