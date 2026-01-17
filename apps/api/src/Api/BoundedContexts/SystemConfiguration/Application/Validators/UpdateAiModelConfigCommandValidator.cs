using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

public sealed class UpdateAiModelConfigCommandValidator : AbstractValidator<UpdateAiModelConfigCommand>
{
    public UpdateAiModelConfigCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        When(x => x.DisplayName != null, () =>
        {
            RuleFor(x => x.DisplayName!)
                .NotEmpty()
                .MaximumLength(200);
        });

        When(x => x.Priority.HasValue, () =>
        {
            RuleFor(x => x.Priority!.Value)
                .GreaterThanOrEqualTo(1);
        });

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
        });
    }
}
