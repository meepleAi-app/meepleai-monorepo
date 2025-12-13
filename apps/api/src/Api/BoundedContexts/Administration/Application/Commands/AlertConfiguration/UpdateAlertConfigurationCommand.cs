using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertConfiguration;

/// <summary>
/// Command to update alert configuration (Issue #915)
/// </summary>
public record UpdateAlertConfigurationCommand(
    string ConfigKey,
    string ConfigValue,
    string Category,
    string UpdatedBy,
    string? Description = null) : IRequest<bool>;

public class UpdateAlertConfigurationCommandValidator : AbstractValidator<UpdateAlertConfigurationCommand>
{
    public UpdateAlertConfigurationCommandValidator()
    {
        RuleFor(x => x.ConfigKey)
            .NotEmpty().WithMessage("ConfigKey is required")
            .MaximumLength(100).WithMessage("ConfigKey must not exceed 100 characters");

        RuleFor(x => x.ConfigValue)
            .NotEmpty().WithMessage("ConfigValue is required")
            .MaximumLength(1000).WithMessage("ConfigValue must not exceed 1000 characters");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required")
            .Must(c => new[] { "Email", "Slack", "PagerDuty", "Global" }.Contains(c))
            .WithMessage("Category must be one of: Email, Slack, PagerDuty, Global");

        RuleFor(x => x.UpdatedBy)
            .NotEmpty().WithMessage("UpdatedBy is required");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
