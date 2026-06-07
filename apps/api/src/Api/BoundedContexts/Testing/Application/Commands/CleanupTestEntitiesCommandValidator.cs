using Api.BoundedContexts.Testing.Infrastructure;
using FluentValidation;

namespace Api.BoundedContexts.Testing.Application.Commands;

internal sealed class CleanupTestEntitiesCommandValidator : AbstractValidator<CleanupTestEntitiesCommand>
{
    public CleanupTestEntitiesCommandValidator()
    {
        RuleFor(x => x.TestRunId)
            .NotEmpty()
            .Must(TestRunIdMetadata.IsValid)
            .WithMessage("TestRunId must match format e2e-{testId}-{epochMs}");
    }
}
