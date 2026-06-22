using Api.BoundedContexts.Testing.Infrastructure;
using FluentValidation;

namespace Api.BoundedContexts.Testing.Application.Commands;

internal sealed class SeedTestPlayerCommandValidator : AbstractValidator<SeedTestPlayerCommand>
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
    {
        "host", "player", "guest"
    };

    public SeedTestPlayerCommandValidator()
    {
        RuleFor(x => x.TestRunId)
            .NotEmpty()
            .Must(TestRunIdMetadata.IsValid)
            .WithMessage("TestRunId must match format e2e-{testId}-{epochMs}");

        RuleFor(x => x.GameNightId)
            .NotEqual(Guid.Empty);

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(r => AllowedRoles.Contains(r))
            .WithMessage($"Role must be one of: {string.Join(", ", AllowedRoles)}");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .When(x => x.Role is "guest");
    }
}
