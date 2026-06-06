using Api.BoundedContexts.Testing.Infrastructure;
using FluentValidation;

namespace Api.BoundedContexts.Testing.Application.Commands;

internal sealed class SeedTestSessionCommandValidator : AbstractValidator<SeedTestSessionCommand>
{
    private static readonly HashSet<string> AllowedScoreTypes = new(StringComparer.Ordinal)
    {
        "Points", "BinaryWin", "Objectives", "Ranking"
    };

    public SeedTestSessionCommandValidator()
    {
        RuleFor(x => x.TestRunId)
            .NotEmpty()
            .Must(TestRunIdMetadata.IsValid)
            .WithMessage("TestRunId must match format e2e-{testId}-{epochMs}");

        RuleFor(x => x.GameNightId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.ScoreType)
            .Must(s => s is null || AllowedScoreTypes.Contains(s))
            .WithMessage($"ScoreType must be one of: {string.Join(", ", AllowedScoreTypes)}")
            .When(x => x.ScoreType is not null);
    }
}
