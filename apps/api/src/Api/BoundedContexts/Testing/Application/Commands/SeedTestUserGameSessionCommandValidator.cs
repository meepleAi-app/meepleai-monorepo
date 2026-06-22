using Api.BoundedContexts.Testing.Infrastructure;
using FluentValidation;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1929 Task C Macro 4 (DEC-C-10 REVISION, DEC-B-5) — Validator for
/// <see cref="SeedTestUserGameSessionCommand"/>. Enforces TestRunId regex
/// (DEC-B-5) and FK presence. UserLibraryEntryId existence is verified in
/// the handler (bad-request guard) not here — avoids async async DB lookup
/// in validator (pattern per SeedTestGameNightCommand).
/// </summary>
internal sealed class SeedTestUserGameSessionCommandValidator
    : AbstractValidator<SeedTestUserGameSessionCommand>
{
    public SeedTestUserGameSessionCommandValidator()
    {
        RuleFor(x => x.TestRunId)
            .NotEmpty()
            .Must(TestRunIdMetadata.IsValid)
            .WithMessage("TestRunId must match format e2e-{testId}-{epochMs}");

        RuleFor(x => x.UserLibraryEntryId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserLibraryEntryId must be a non-empty Guid.");

        RuleFor(x => x.DurationMinutes!.Value)
            .GreaterThan(0)
            .WithMessage("DurationMinutes must be > 0 when provided.")
            .When(x => x.DurationMinutes.HasValue);
    }
}
