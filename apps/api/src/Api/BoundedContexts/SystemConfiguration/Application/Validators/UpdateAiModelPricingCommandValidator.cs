using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

/// <summary>
/// #4169: un prezzo non negativo e plausibile. Il limite alto ferma solo un errore di unità, cioè il
/// prezzo per token scritto al posto del prezzo per milione.
/// </summary>
internal sealed class UpdateAiModelPricingCommandValidator : AbstractValidator<UpdateAiModelPricingCommand>
{
    internal const decimal MaxPricePerMillion = 1_000m;

    public UpdateAiModelPricingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.InputPricePerMillion)
            .InclusiveBetween(0m, MaxPricePerMillion)
            .WithMessage($"InputPricePerMillion must be between 0 and {MaxPricePerMillion} USD");

        RuleFor(x => x.OutputPricePerMillion)
            .InclusiveBetween(0m, MaxPricePerMillion)
            .WithMessage($"OutputPricePerMillion must be between 0 and {MaxPricePerMillion} USD");
    }
}
