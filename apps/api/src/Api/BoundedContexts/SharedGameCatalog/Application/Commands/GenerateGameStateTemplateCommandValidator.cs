using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for GenerateGameStateTemplateCommand.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class GenerateGameStateTemplateCommandValidator : AbstractValidator<GenerateGameStateTemplateCommand>
{
    public GenerateGameStateTemplateCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEqual(Guid.Empty)
            .WithMessage("SharedGameId is required");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(200)
            .WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.CreatedBy)
            .NotEqual(Guid.Empty)
            .WithMessage("CreatedBy is required");
    }
}
