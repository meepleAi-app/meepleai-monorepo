using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for ScheduleReportCommand.
/// Ensures all required fields are valid for scheduling a recurring report.
/// </summary>
internal sealed class ScheduleReportCommandValidator : AbstractValidator<ScheduleReportCommand>
{
    public ScheduleReportCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(200)
            .WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Description is required")
            .MaximumLength(1000)
            .WithMessage("Description must not exceed 1000 characters");

        RuleFor(x => x.Template)
            .IsInEnum()
            .WithMessage("Template must be a valid ReportTemplate value");

        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage("Format must be a valid ReportFormat value");

        RuleFor(x => x.Parameters)
            .NotNull()
            .WithMessage("Parameters are required");

        RuleFor(x => x.ScheduleExpression)
            .NotEmpty()
            .WithMessage("ScheduleExpression is required")
            .MaximumLength(100)
            .WithMessage("ScheduleExpression must not exceed 100 characters");

        RuleFor(x => x.CreatedBy)
            .NotEmpty()
            .WithMessage("CreatedBy is required");

        RuleForEach(x => x.EmailRecipients)
            .EmailAddress()
            .WithMessage("Each email recipient must be a valid email address")
            .When(x => x.EmailRecipients != null && x.EmailRecipients.Count > 0);
    }
}
