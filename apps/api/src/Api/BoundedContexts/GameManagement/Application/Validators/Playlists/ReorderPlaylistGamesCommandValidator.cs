using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.Playlists;

/// <summary>
/// Validator for ReorderPlaylistGamesCommand.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal sealed class ReorderPlaylistGamesCommandValidator : AbstractValidator<ReorderPlaylistGamesCommand>
{
    public ReorderPlaylistGamesCommandValidator()
    {
        RuleFor(x => x.PlaylistId)
            .NotEmpty()
            .WithMessage("PlaylistId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.OrderedGameIds)
            .NotNull()
            .WithMessage("OrderedGameIds is required")
            .NotEmpty()
            .WithMessage("OrderedGameIds cannot be empty");
    }
}
