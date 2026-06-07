using Api.BoundedContexts.Testing.Infrastructure;
using FluentValidation;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1929 Task C Macro 3a (DEC-B-5, DEC-C-8) — Validator for
/// <see cref="SeedTestLibraryGameCommand"/>. Enforces testRunId regex
/// (DEC-B-5) and player-count sanity bounds. Optional fields are validated
/// conditionally via <see cref="ValidatorOptions.Global"/> When-clauses.
/// </summary>
internal sealed class SeedTestLibraryGameCommandValidator
    : AbstractValidator<SeedTestLibraryGameCommand>
{
    public SeedTestLibraryGameCommandValidator()
    {
        RuleFor(x => x.TestRunId)
            .NotEmpty()
            .Must(TestRunIdMetadata.IsValid)
            .WithMessage("TestRunId must match format e2e-{testId}-{epochMs}");

        RuleFor(x => x.OwnerEmail)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.MinPlayers!.Value)
            .GreaterThan(0)
            .LessThanOrEqualTo(99)
            .When(x => x.MinPlayers.HasValue);

        RuleFor(x => x.MaxPlayers!.Value)
            .GreaterThanOrEqualTo(x => x.MinPlayers ?? 1)
            .LessThanOrEqualTo(99)
            .When(x => x.MaxPlayers.HasValue);
    }
}
