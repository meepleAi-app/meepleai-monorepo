using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class UpdateChatHistoryLimitsCommandValidator : AbstractValidator<UpdateChatHistoryLimitsCommand>
{
    private const int MinLimit = 1;
    private const int MaxLimit = 10000;

    public UpdateChatHistoryLimitsCommandValidator()
    {
        RuleFor(x => x.FreeTierLimit)
            .InclusiveBetween(MinLimit, MaxLimit)
            .WithMessage($"FreeTierLimit must be between {MinLimit} and {MaxLimit}");

        RuleFor(x => x.NormalTierLimit)
            .InclusiveBetween(MinLimit, MaxLimit)
            .WithMessage($"NormalTierLimit must be between {MinLimit} and {MaxLimit}");

        RuleFor(x => x.PremiumTierLimit)
            .InclusiveBetween(MinLimit, MaxLimit)
            .WithMessage($"PremiumTierLimit must be between {MinLimit} and {MaxLimit}");

        RuleFor(x => x.UpdatedByUserId)
            .NotEmpty()
            .WithMessage("UpdatedByUserId is required");

        RuleFor(x => x.NormalTierLimit)
            .GreaterThanOrEqualTo(x => x.FreeTierLimit)
            .WithMessage("NormalTierLimit must be greater than or equal to FreeTierLimit");

        RuleFor(x => x.PremiumTierLimit)
            .GreaterThanOrEqualTo(x => x.NormalTierLimit)
            .WithMessage("PremiumTierLimit must be greater than or equal to NormalTierLimit");
    }
}
