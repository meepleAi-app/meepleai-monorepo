using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

public sealed class CreateAiModelConfigCommandValidator : AbstractValidator<CreateAiModelConfigCommand>
{
    public CreateAiModelConfigCommandValidator()
    {
        RuleFor(x => x.ModelId)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Provider)
            .NotEmpty()
            .MaximumLength(50)
            .Must(p => p is "OpenRouter" or "Ollama")
            .WithMessage("Provider must be 'OpenRouter' or 'Ollama'");

        RuleFor(x => x.Priority)
            .GreaterThanOrEqualTo(1);

        When(x => x.Settings != null, () =>
        {
            RuleFor(x => x.Settings!.MaxTokens)
                .GreaterThan(0);

            RuleFor(x => x.Settings!.Temperature)
                .InclusiveBetween(0, 2);
        });

        When(x => x.Pricing != null, () =>
        {
            RuleFor(x => x.Pricing!.InputPricePerMillion)
                .GreaterThanOrEqualTo(0);

            RuleFor(x => x.Pricing!.OutputPricePerMillion)
                .GreaterThanOrEqualTo(0);

            RuleFor(x => x.Pricing!.Currency)
                .NotEmpty()
                .MaximumLength(3);
        });
    }
}
