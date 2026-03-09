using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.Playlists;

/// <summary>
/// Validator for UpdatePlaylistCommand.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal sealed class UpdatePlaylistCommandValidator : AbstractValidator<UpdatePlaylistCommand>
{
    public UpdatePlaylistCommandValidator()
    {
        RuleFor(x => x.PlaylistId)
            .NotEmpty()
            .WithMessage("PlaylistId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Name)
            .MaximumLength(200)
            .WithMessage("Playlist name cannot exceed 200 characters")
            .When(x => x.Name != null);
    }
}
