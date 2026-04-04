using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for GenerateReportCommand.
/// Ensures template and format are valid enums and parameters are provided.
/// </summary>
internal sealed class GenerateReportCommandValidator : AbstractValidator<GenerateReportCommand>
{
    public GenerateReportCommandValidator()
    {
        RuleFor(x => x.Template)
            .IsInEnum()
            .WithMessage("Template must be a valid ReportTemplate value");

        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage("Format must be a valid ReportFormat value");

        RuleFor(x => x.Parameters)
            .NotNull()
            .WithMessage("Parameters are required");
    }
}
