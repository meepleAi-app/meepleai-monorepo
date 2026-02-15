using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

/// <summary>
/// Validates push subscription data.
/// Issue #4416: Push notifications via Service Worker.
/// </summary>
internal class SubscribePushNotificationsCommandValidator : AbstractValidator<SubscribePushNotificationsCommand>
{
    public SubscribePushNotificationsCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Endpoint)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(e => Uri.TryCreate(e, UriKind.Absolute, out _))
            .WithMessage("Endpoint must be a valid URL");
        RuleFor(x => x.P256dhKey).NotEmpty().MaximumLength(512);
        RuleFor(x => x.AuthKey).NotEmpty().MaximumLength(512);
    }
}
