using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed class DeleteGamebookCampaignValidator : AbstractValidator<DeleteGamebookCampaignCommand>
{
    public DeleteGamebookCampaignValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
    }
}
