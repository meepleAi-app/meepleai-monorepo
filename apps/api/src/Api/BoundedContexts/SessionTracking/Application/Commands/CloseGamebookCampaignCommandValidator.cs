using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class CloseGamebookCampaignCommandValidator : AbstractValidator<CloseGamebookCampaignCommand>
{
    public CloseGamebookCampaignCommandValidator()
    {
        RuleFor(x => x.CampaignId)
            .NotEmpty()
            .WithMessage("CampaignId is required");

        RuleFor(x => x.CallerUserId)
            .NotEmpty()
            .WithMessage("CallerUserId is required");

        RuleFor(x => x.Outcome)
            .IsInEnum()
            .WithMessage("Outcome must be a valid terminal outcome (Completed or Abandoned)");
    }
}
