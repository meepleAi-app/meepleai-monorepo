using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed class RenameGamebookCampaignValidator : AbstractValidator<RenameGamebookCampaignCommand>
{
    public RenameGamebookCampaignValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
        RuleFor(x => x.Title)
            .NotEmpty()
            .MinimumLength(1)
            .MaximumLength(200)
            .Must(t => !string.IsNullOrWhiteSpace(t))
            .WithMessage("Title cannot be whitespace.");
    }
}
