using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

public record UpdateAlertRuleCommand(
    Guid Id, 
    string Name, 
    string Severity, 
    double ThresholdValue, 
    string ThresholdUnit, 
    int DurationMinutes, 
    string? Description, 
    string UpdatedBy) : IRequest<Unit>;

public class UpdateAlertRuleCommandValidator : AbstractValidator<UpdateAlertRuleCommand>
{
    public UpdateAlertRuleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Alert rule ID is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Alert rule name is required")
            .MaximumLength(200).WithMessage("Alert rule name must not exceed 200 characters");

        RuleFor(x => x.Severity)
            .NotEmpty().WithMessage("Severity is required")
            .Must(s => new[] { "Info", "Warning", "Error", "Critical" }.Contains(s))
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

        RuleFor(x => x.UpdatedBy)
            .NotEmpty().WithMessage("UpdatedBy is required");
    }
}
