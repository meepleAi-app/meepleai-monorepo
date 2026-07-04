using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

internal sealed class DeleteGlossaryEntryCommandValidator : AbstractValidator<DeleteGlossaryEntryCommand>
{
    public DeleteGlossaryEntryCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEqual(Guid.Empty);
        RuleFor(x => x.EntryId).NotEqual(Guid.Empty);
        RuleFor(x => x.CallerUserId).NotEqual(Guid.Empty);
    }
}
