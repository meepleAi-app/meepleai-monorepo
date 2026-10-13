using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;

internal sealed class UpdateTierDefinitionCommandValidator : AbstractValidator<UpdateTierDefinitionCommand>
{
    public UpdateTierDefinitionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.DisplayName)
            .MaximumLength(200)
            .WithMessage("DisplayName must not exceed 200 characters")
            .When(x => x.DisplayName is not null);

        RuleFor(x => x.LlmModelTier)
            .MaximumLength(100)
            .WithMessage("LlmModelTier must not exceed 100 characters")
            .When(x => x.LlmModelTier is not null);

        // #4212: a client that omits the field sends 0. Named here so the caller learns which
        // field is wrong; the domain would reject it too, but as a 400 that names nothing.
        RuleFor(x => x.Limits!.MaxLibraryGames)
            .GreaterThanOrEqualTo(1)
            .WithMessage("MaxLibraryGames must be at least 1")
            .When(x => x.Limits is not null);
    }
}
