using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for SimulateErrorCommand.
/// Ensures error type is a known simulation type.
/// </summary>
internal sealed class SimulateErrorCommandValidator : AbstractValidator<SimulateErrorCommand>
{
    private static readonly string[] AllowedErrorTypes = { "500", "400", "timeout", "exception" };

    public SimulateErrorCommandValidator()
    {
        RuleFor(x => x.ErrorType)
            .NotEmpty()
            .WithMessage("ErrorType is required")
            .Must(t => AllowedErrorTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage("ErrorType must be one of: 500, 400, timeout, exception");
    }
}
