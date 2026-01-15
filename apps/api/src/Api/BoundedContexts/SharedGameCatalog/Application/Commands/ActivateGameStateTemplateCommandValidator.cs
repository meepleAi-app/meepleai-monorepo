using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for ActivateGameStateTemplateCommand.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class ActivateGameStateTemplateCommandValidator : AbstractValidator<ActivateGameStateTemplateCommand>
{
    public ActivateGameStateTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEqual(Guid.Empty)
            .WithMessage("TemplateId is required");
    }
}
