using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

internal sealed class UpdateSlackTeamChannelCommandValidator : AbstractValidator<UpdateSlackTeamChannelCommand>
{
    public UpdateSlackTeamChannelCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");

        RuleFor(x => x.ChannelName)
            .MaximumLength(200)
            .WithMessage("ChannelName must not exceed 200 characters")
            .When(x => x.ChannelName is not null);

        RuleFor(x => x.WebhookUrl)
            .MaximumLength(2000)
            .WithMessage("WebhookUrl must not exceed 2000 characters")
            .When(x => x.WebhookUrl is not null);

        RuleForEach(x => x.NotificationTypes)
            .NotEmpty()
            .WithMessage("NotificationType entries must not be empty")
            .MaximumLength(100)
            .WithMessage("NotificationType entries must not exceed 100 characters")
            .When(x => x.NotificationTypes is not null);
    }
}
