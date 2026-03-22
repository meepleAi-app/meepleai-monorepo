using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for SendAlertCommand.
/// Ensures alert type, severity, and message are provided.
/// </summary>
internal sealed class SendAlertCommandValidator : AbstractValidator<SendAlertCommand>
{
    private static readonly string[] AllowedSeverities = { "Info", "Warning", "Error", "Critical" };

    public SendAlertCommandValidator()
    {
        RuleFor(x => x.AlertType)
            .NotEmpty()
            .WithMessage("AlertType is required")
            .MaximumLength(100)
            .WithMessage("AlertType must not exceed 100 characters");

        RuleFor(x => x.Severity)
            .NotEmpty()
            .WithMessage("Severity is required")
            .Must(s => AllowedSeverities.Contains(s, StringComparer.Ordinal))
            .WithMessage("Severity must be one of: Info, Warning, Error, Critical");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message is required")
            .MaximumLength(5000)
            .WithMessage("Message must not exceed 5000 characters");
    }
}
