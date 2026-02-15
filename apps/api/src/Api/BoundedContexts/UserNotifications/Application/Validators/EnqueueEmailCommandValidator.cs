using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserNotifications.Application.Validators;

/// <summary>
/// Validator for EnqueueEmailCommand.
/// Issue #4417: Email notification queue.
/// </summary>
internal sealed class EnqueueEmailCommandValidator : AbstractValidator<EnqueueEmailCommand>
{
    private static readonly string[] ValidTemplates = ["document_ready", "document_failed", "retry_available"];

    public EnqueueEmailCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.To)
            .NotEmpty()
            .WithMessage("To address is required")
            .EmailAddress()
            .WithMessage("To address must be a valid email");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .WithMessage("Subject is required")
            .MaximumLength(500)
            .WithMessage("Subject must not exceed 500 characters");

        RuleFor(x => x.TemplateName)
            .NotEmpty()
            .WithMessage("TemplateName is required")
            .Must(t => ValidTemplates.Contains(t, StringComparer.Ordinal))
            .WithMessage("TemplateName must be one of: document_ready, document_failed, retry_available");

        RuleFor(x => x.UserName)
            .NotEmpty()
            .WithMessage("UserName is required");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("FileName is required");
    }
}
