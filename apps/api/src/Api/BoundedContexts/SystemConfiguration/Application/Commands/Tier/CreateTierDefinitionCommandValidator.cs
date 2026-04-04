using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;

internal sealed class CreateTierDefinitionCommandValidator : AbstractValidator<CreateTierDefinitionCommand>
{
    public CreateTierDefinitionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("DisplayName is required")
            .MaximumLength(200)
            .WithMessage("DisplayName must not exceed 200 characters");

        RuleFor(x => x.Limits)
            .NotNull()
            .WithMessage("Limits is required");

        RuleFor(x => x.LlmModelTier)
            .NotEmpty()
            .WithMessage("LlmModelTier is required")
            .MaximumLength(100)
            .WithMessage("LlmModelTier must not exceed 100 characters");
    }
}
