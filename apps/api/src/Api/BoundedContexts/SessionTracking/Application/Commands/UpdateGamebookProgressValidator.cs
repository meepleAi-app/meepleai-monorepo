using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed class UpdateGamebookProgressValidator : AbstractValidator<UpdateGamebookProgressCommand>
{
    public UpdateGamebookProgressValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
        RuleFor(x => x.GameBookId).NotEqual(Guid.Empty);
        RuleFor(x => x.CurrentParagraph).GreaterThanOrEqualTo(0);
    }
}
