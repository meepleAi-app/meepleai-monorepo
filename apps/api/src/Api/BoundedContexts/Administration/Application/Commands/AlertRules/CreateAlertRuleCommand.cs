using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

internal record CreateAlertRuleCommand(
    string Name,
    string AlertType,
    string Severity,
    double ThresholdValue,
    string ThresholdUnit,
    int DurationMinutes,
    string? Description,
    string CreatedBy) : IRequest<Guid>;

internal class CreateAlertRuleCommandValidator : AbstractValidator<CreateAlertRuleCommand>
{
    private static readonly string[] AllowedSeverities = { "Info", "Warning", "Error", "Critical" };

    public CreateAlertRuleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Alert rule name is required")
            .MaximumLength(200).WithMessage("Alert rule name must not exceed 200 characters");

        RuleFor(x => x.AlertType)
            .NotEmpty().WithMessage("Alert type is required")
            .MaximumLength(100).WithMessage("Alert type must not exceed 100 characters");

        RuleFor(x => x.Severity)
            .NotEmpty().WithMessage("Severity is required")
            .Must(s => AllowedSeverities.Contains(s, StringComparer.Ordinal))
            .WithMessage("Severity must be one of: Info, Warning, Error, Critical");

        RuleFor(x => x.ThresholdValue)
            .GreaterThanOrEqualTo(0).WithMessage("Threshold value must be non-negative");

        RuleFor(x => x.ThresholdUnit)
            .NotEmpty().WithMessage("Threshold unit is required")
            .MaximumLength(20).WithMessage("Threshold unit must not exceed 20 characters");

        RuleFor(x => x.DurationMinutes)
            .GreaterThan(0).WithMessage("Duration must be at least 1 minute")
            .LessThanOrEqualTo(10080).WithMessage("Duration must not exceed 7 days (10080 minutes)");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description must not exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.CreatedBy)
            .NotEmpty().WithMessage("CreatedBy is required");
    }
}
