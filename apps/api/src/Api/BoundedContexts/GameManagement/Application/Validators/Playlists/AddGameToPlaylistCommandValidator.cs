using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.Playlists;

/// <summary>
/// Validator for AddGameToPlaylistCommand.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal sealed class AddGameToPlaylistCommandValidator : AbstractValidator<AddGameToPlaylistCommand>
{
    public AddGameToPlaylistCommandValidator()
    {
        RuleFor(x => x.PlaylistId)
            .NotEmpty()
            .WithMessage("PlaylistId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.SharedGameId)
            .NotEmpty()
            .WithMessage("SharedGameId is required");

        RuleFor(x => x.Position)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Position must be >= 1");
    }
}
