using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Tests for <see cref="ProposePrivateGameCommandValidator"/> (#5074). The description rule needs the stored game
/// and is tested on the handler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class ProposePrivateGameCommandValidatorTests
{
    private readonly ProposePrivateGameCommandValidator _validator = new();

    [Fact]
    public void Validate_WithIdsAndNoNotes_Passes()
    {
        var result = _validator.TestValidate(new ProposePrivateGameCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_Fails()
    {
        var result = _validator.TestValidate(new ProposePrivateGameCommand(Guid.Empty, Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validate_WithEmptyPrivateGameId_Fails()
    {
        var result = _validator.TestValidate(new ProposePrivateGameCommand(Guid.NewGuid(), Guid.Empty));

        result.ShouldHaveValidationErrorFor(x => x.PrivateGameId);
    }

    [Fact]
    public void Validate_WithNotesOverTheLimit_Fails()
    {
        var command = new ProposePrivateGameCommand(Guid.NewGuid(), Guid.NewGuid(), new string('n', 2001));

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Validate_WithNotesAtTheLimit_Passes()
    {
        var command = new ProposePrivateGameCommand(Guid.NewGuid(), Guid.NewGuid(), new string('n', 2000));

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }
}
