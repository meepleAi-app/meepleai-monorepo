using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for StartGameNightSessionCommandValidator.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class StartGameNightSessionCommandValidatorTests
{
    private readonly StartGameNightSessionCommandValidator _validator = new();

    [Fact]
    public void Valid_Command_Passes()
    {
        var command = new StartGameNightSessionCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_GameNightId_Fails()
    {
        var command = new StartGameNightSessionCommand(
            Guid.Empty, Guid.NewGuid(), "Catan", Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameNightId);
    }

    [Fact]
    public void Empty_GameId_Fails()
    {
        var command = new StartGameNightSessionCommand(
            Guid.NewGuid(), Guid.Empty, "Catan", Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void Empty_GameTitle_Fails()
    {
        var command = new StartGameNightSessionCommand(
            Guid.NewGuid(), Guid.NewGuid(), "", Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameTitle);
    }

    [Fact]
    public void GameTitle_Exceeds200Chars_Fails()
    {
        var command = new StartGameNightSessionCommand(
            Guid.NewGuid(), Guid.NewGuid(), new string('A', 201), Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.GameTitle);
    }

    [Fact]
    public void Empty_UserId_Fails()
    {
        var command = new StartGameNightSessionCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    // #4780 PR 4: a seat per colour, so no more guests than colours.
    [Fact]
    public void GuestInvitationIds_UpToOnePerColour_Pass_AndOneMore_Fails()
    {
        var colours = Enum.GetValues<Api.BoundedContexts.GameManagement.Domain.Enums.PlayerColor>().Length;
        StartGameNightSessionCommand WithGuests(int count) => new(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.NewGuid(),
            GuestInvitationIds: Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList());

        _validator.TestValidate(WithGuests(colours)).ShouldNotHaveValidationErrorFor(x => x.GuestInvitationIds);
        _validator.TestValidate(WithGuests(colours + 1)).ShouldHaveValidationErrorFor(x => x.GuestInvitationIds);
    }
}
