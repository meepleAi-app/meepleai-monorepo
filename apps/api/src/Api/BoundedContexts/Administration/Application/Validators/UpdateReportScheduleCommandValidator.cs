using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for UpdateReportScheduleCommand.
/// Ensures report ID is valid and schedule expression has proper format.
/// </summary>
internal sealed class UpdateReportScheduleCommandValidator : AbstractValidator<UpdateReportScheduleCommand>
{
    public UpdateReportScheduleCommandValidator()
    {
        RuleFor(x => x.ReportId)
            .NotEmpty()
            .WithMessage("ReportId is required");

        RuleFor(x => x.ScheduleExpression)
            .MaximumLength(100)
            .WithMessage("ScheduleExpression must not exceed 100 characters")
            .When(x => x.ScheduleExpression != null);
    }
}
