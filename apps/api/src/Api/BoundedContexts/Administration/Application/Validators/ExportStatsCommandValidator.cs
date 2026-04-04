using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for ExportStatsCommand.
/// Ensures format is valid and date range is sensible.
/// </summary>
internal sealed class ExportStatsCommandValidator : AbstractValidator<ExportStatsCommand>
{
    private static readonly string[] AllowedFormats = { "csv", "json" };

    public ExportStatsCommandValidator()
    {
        RuleFor(x => x.Format)
            .NotEmpty()
            .WithMessage("Format is required")
            .Must(f => AllowedFormats.Contains(f, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Format must be one of: csv, json");

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value <= x.ToDate.Value)
            .WithMessage("FromDate must be before or equal to ToDate");

        RuleFor(x => x.GameId)
            .Must(BeValidGuidOrNull)
            .WithMessage("GameId must be a valid GUID format")
            .When(x => !string.IsNullOrEmpty(x.GameId));
    }

    private static bool BeValidGuidOrNull(string? value)
    {
        return string.IsNullOrEmpty(value) || Guid.TryParse(value, out _);
    }
}
