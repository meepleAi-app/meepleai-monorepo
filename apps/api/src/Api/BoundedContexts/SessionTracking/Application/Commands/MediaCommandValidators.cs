using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class UploadSessionMediaCommandValidator : AbstractValidator<UploadSessionMediaCommand>
{
    private static readonly string[] AllowedMediaTypes =
        ["Photo", "Note", "Screenshot", "Video", "Audio", "Document"];

    public UploadSessionMediaCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required.");
        RuleFor(x => x.ParticipantId).NotEmpty().WithMessage("Participant ID is required.");
        RuleFor(x => x.FileId).NotEmpty().WithMessage("File ID is required.");
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255).WithMessage("File name must be 1-255 characters.");
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(100).WithMessage("Content type is required.");
        RuleFor(x => x.FileSizeBytes).GreaterThan(0).WithMessage("File size must be positive.");
        RuleFor(x => x.MediaType).Must(mt => AllowedMediaTypes.Contains(mt, StringComparer.Ordinal))
            .WithMessage($"Media type must be one of: {string.Join(", ", AllowedMediaTypes)}.");
        RuleFor(x => x.Caption).MaximumLength(500).When(x => x.Caption != null);
    }
}

public class UpdateMediaCaptionCommandValidator : AbstractValidator<UpdateMediaCaptionCommand>
{
    public UpdateMediaCaptionCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.ParticipantId).NotEmpty();
        RuleFor(x => x.Caption).MaximumLength(500).When(x => x.Caption != null);
    }
}

public class DeleteSessionMediaCommandValidator : AbstractValidator<DeleteSessionMediaCommand>
{
    public DeleteSessionMediaCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.ParticipantId).NotEmpty();
    }
}
