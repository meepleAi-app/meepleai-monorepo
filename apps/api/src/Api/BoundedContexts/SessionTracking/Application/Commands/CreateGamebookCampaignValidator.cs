using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed class CreateGamebookCampaignValidator : AbstractValidator<CreateGamebookCampaignCommand>
{
    public CreateGamebookCampaignValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
    }
}
