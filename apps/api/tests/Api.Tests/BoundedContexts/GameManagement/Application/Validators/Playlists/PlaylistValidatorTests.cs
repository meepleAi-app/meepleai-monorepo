using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using Api.BoundedContexts.GameManagement.Application.Validators.Playlists;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Validators.Playlists;

[Trait("Category", TestCategories.Unit)]
public class PlaylistValidatorTests
{
    #region CreatePlaylistCommandValidator

    [Fact]
    public void CreatePlaylist_ValidCommand_NoErrors()
    {
        var validator = new CreatePlaylistCommandValidator();
        var command = new CreatePlaylistCommand("Game Night", Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreatePlaylist_EmptyName_HasError()
    {
        var validator = new CreatePlaylistCommandValidator();
        var command = new CreatePlaylistCommand("", Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreatePlaylist_NameTooLong_HasError()
    {
        var validator = new CreatePlaylistCommandValidator();
        var command = new CreatePlaylistCommand(new string('A', 201), Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreatePlaylist_EmptyUserId_HasError()
    {
        var validator = new CreatePlaylistCommandValidator();
        var command = new CreatePlaylistCommand("Test", Guid.Empty);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CreatorUserId);
    }

    #endregion

    #region UpdatePlaylistCommandValidator

    [Fact]
    public void UpdatePlaylist_ValidCommand_NoErrors()
    {
        var validator = new UpdatePlaylistCommandValidator();
        var command = new UpdatePlaylistCommand(Guid.NewGuid(), Guid.NewGuid(), "New Name");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdatePlaylist_EmptyPlaylistId_HasError()
    {
        var validator = new UpdatePlaylistCommandValidator();
        var command = new UpdatePlaylistCommand(Guid.Empty, Guid.NewGuid(), "Name");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PlaylistId);
    }

    [Fact]
    public void UpdatePlaylist_NameTooLong_HasError()
    {
        var validator = new UpdatePlaylistCommandValidator();
        var command = new UpdatePlaylistCommand(Guid.NewGuid(), Guid.NewGuid(), new string('A', 201));

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    #endregion

    #region AddGameToPlaylistCommandValidator

    [Fact]
    public void AddGame_ValidCommand_NoErrors()
    {
        var validator = new AddGameToPlaylistCommandValidator();
        var command = new AddGameToPlaylistCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddGame_EmptyPlaylistId_HasError()
    {
        var validator = new AddGameToPlaylistCommandValidator();
        var command = new AddGameToPlaylistCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 1);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.PlaylistId);
    }

    [Fact]
    public void AddGame_EmptyGameId_HasError()
    {
        var validator = new AddGameToPlaylistCommandValidator();
        var command = new AddGameToPlaylistCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 1);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.SharedGameId);
    }

    [Fact]
    public void AddGame_ZeroPosition_HasError()
    {
        var validator = new AddGameToPlaylistCommandValidator();
        var command = new AddGameToPlaylistCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Position);
    }

    #endregion

    #region ReorderPlaylistGamesCommandValidator

    [Fact]
    public void Reorder_ValidCommand_NoErrors()
    {
        var validator = new ReorderPlaylistGamesCommandValidator();
        var command = new ReorderPlaylistGamesCommand(Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid() });

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Reorder_EmptyList_HasError()
    {
        var validator = new ReorderPlaylistGamesCommandValidator();
        var command = new ReorderPlaylistGamesCommand(Guid.NewGuid(), Guid.NewGuid(), new List<Guid>());

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.OrderedGameIds);
    }

    [Fact]
    public void Reorder_NullList_HasError()
    {
        var validator = new ReorderPlaylistGamesCommandValidator();
        var command = new ReorderPlaylistGamesCommand(Guid.NewGuid(), Guid.NewGuid(), null!);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.OrderedGameIds);
    }

    #endregion
}
