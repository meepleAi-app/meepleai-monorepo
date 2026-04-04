using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.Playlists;

/// <summary>
/// Validator for RemoveGameFromPlaylistCommand.
/// Ensures all GUID properties are non-empty.
/// </summary>
internal sealed class RemoveGameFromPlaylistCommandValidator : AbstractValidator<RemoveGameFromPlaylistCommand>
{
    public RemoveGameFromPlaylistCommandValidator()
    {
        RuleFor(x => x.PlaylistId)
            .NotEmpty().WithMessage("Playlist ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.SharedGameId)
            .NotEmpty().WithMessage("Shared game ID is required");
    }
}
