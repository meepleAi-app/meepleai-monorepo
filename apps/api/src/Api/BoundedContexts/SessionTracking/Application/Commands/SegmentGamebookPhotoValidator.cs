using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed class SegmentGamebookPhotoValidator : AbstractValidator<SegmentGamebookPhotoCommand>
{
    public SegmentGamebookPhotoValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.PhotoId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
    }
}
