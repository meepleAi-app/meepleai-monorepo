using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed class BootstrapGamebookGlossaryValidator : AbstractValidator<BootstrapGamebookGlossaryCommand>
{
    public BootstrapGamebookGlossaryValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
    }
}
