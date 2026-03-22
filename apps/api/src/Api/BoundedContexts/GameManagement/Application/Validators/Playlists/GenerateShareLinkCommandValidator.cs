using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.Playlists;

/// <summary>
/// Validator for GenerateShareLinkCommand.
/// Ensures PlaylistId and UserId are non-empty GUIDs.
/// </summary>
internal sealed class GenerateShareLinkCommandValidator : AbstractValidator<GenerateShareLinkCommand>
{
    public GenerateShareLinkCommandValidator()
    {
        RuleFor(x => x.PlaylistId)
            .NotEmpty().WithMessage("Playlist ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}
