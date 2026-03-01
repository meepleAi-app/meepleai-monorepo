using Api.BoundedContexts.UserLibrary.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for GetGameWizardPreviewQuery.
/// Issue #4823: Backend Game Preview API
/// </summary>
internal class GetGameWizardPreviewQueryValidator : AbstractValidator<GetGameWizardPreviewQuery>
{
    private static readonly string[] ValidSources = ["catalog"];

    public GetGameWizardPreviewQueryValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.Source)
            .NotEmpty()
            .WithMessage("Source is required")
            .Must(s => ValidSources.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Source must be one of: {string.Join(", ", ValidSources)}");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
