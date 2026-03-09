using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.Playlists;

/// <summary>
/// Validator for CreatePlaylistCommand.
/// Issue #5582: Game Night Playlist backend.
/// </summary>
internal sealed class CreatePlaylistCommandValidator : AbstractValidator<CreatePlaylistCommand>
{
    public CreatePlaylistCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Playlist name is required")
            .MaximumLength(200)
            .WithMessage("Playlist name cannot exceed 200 characters");

        RuleFor(x => x.CreatorUserId)
            .NotEmpty()
            .WithMessage("CreatorUserId is required");
    }
}
