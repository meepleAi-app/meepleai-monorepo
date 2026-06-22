using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed class UploadGamebookPhotoValidator : AbstractValidator<UploadGamebookPhotoCommand>
{
    public UploadGamebookPhotoValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.CallerUserId).NotEmpty();
        RuleFor(x => x.ContentType).NotEmpty();
    }
}
