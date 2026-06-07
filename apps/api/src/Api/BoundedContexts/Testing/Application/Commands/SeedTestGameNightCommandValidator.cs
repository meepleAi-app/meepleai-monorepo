using Api.BoundedContexts.Testing.Infrastructure;
using FluentValidation;

namespace Api.BoundedContexts.Testing.Application.Commands;

internal sealed class SeedTestGameNightCommandValidator : AbstractValidator<SeedTestGameNightCommand>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "Draft", "Published", "InProgress", "Completed"
    };

    public SeedTestGameNightCommandValidator()
    {
        RuleFor(x => x.TestRunId)
            .NotEmpty()
            .Must(TestRunIdMetadata.IsValid)
            .WithMessage("TestRunId must match format e2e-{testId}-{epochMs}");

        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage($"Status must be one of: {string.Join(", ", AllowedStatuses)}");

        RuleFor(x => x.OwnerEmail)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.RosterCount)
            .InclusiveBetween(0, 16);

        RuleFor(x => x.ScoringType)
            .Must(s => s is null or "Points" or "BinaryWin" or "Objectives" or "Ranking")
            .When(x => x.ScoringType is not null);

        // Issue #1929 Macro 4 (DEC-C-10 PIVOT): GameId format-only validation (non-empty Guid).
        // Existence check (GameId must be in SharedGames) is performed in the handler to keep
        // the validator DB-free (consistent with SeedTestLibraryGameCommandValidator pattern).
        RuleFor(x => x.GameId!.Value)
            .NotEqual(Guid.Empty)
            .WithMessage("GameId must be a non-empty Guid when provided")
            .When(x => x.GameId.HasValue);
    }
}
