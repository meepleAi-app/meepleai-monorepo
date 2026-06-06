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
    }
}
