using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Validator for <see cref="CancelRecalcJobCommand"/> (ADR-051 M2.1, Sprint 2 / Task 9).
/// </summary>
/// <remarks>
/// Enforces a non-empty <c>JobId</c> at the application boundary so the validation pipeline
/// behavior surfaces a 400 before the handler hits the repository.
/// </remarks>
internal sealed class CancelRecalcJobValidator
    : AbstractValidator<CancelRecalcJobCommand>
{
    public CancelRecalcJobValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty()
            .WithMessage("JobId is required.");
    }
}
