using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed class CreateGamebookCampaignValidator : AbstractValidator<CreateGamebookCampaignCommand>
{
    public CreateGamebookCampaignValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);

        // #2917: validate the optional roster consistently with the rest of the command.
        RuleForEach(x => x.GuestNames)
            .NotEmpty()
            .MaximumLength(120)
            .When(x => x.GuestNames is not null);

        RuleForEach(x => x.Participants)
            .Must(p => !string.IsNullOrWhiteSpace(p.DisplayName))
            .WithMessage("Participant display name is required.")
            .When(x => x.Participants is not null);
    }
}
