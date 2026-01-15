using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UpdateGameStateTemplateCommand.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class UpdateGameStateTemplateCommandValidator : AbstractValidator<UpdateGameStateTemplateCommand>
{
    public UpdateGameStateTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEqual(Guid.Empty)
            .WithMessage("TemplateId is required");

        RuleFor(x => x.Name)
            .MaximumLength(200)
            .WithMessage("Name cannot exceed 200 characters")
            .When(x => x.Name is not null);

        RuleFor(x => x.SchemaJson)
            .NotEmpty()
            .WithMessage("SchemaJson is required")
            .Must(BeValidJson)
            .WithMessage("SchemaJson must be valid JSON");

        RuleFor(x => x.NewVersion)
            .NotEmpty()
            .WithMessage("NewVersion is required")
            .Matches(@"^\d+\.\d+$")
            .WithMessage("Version must be in format MAJOR.MINOR (e.g., '1.0', '2.1')");
    }

    private static bool BeValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
