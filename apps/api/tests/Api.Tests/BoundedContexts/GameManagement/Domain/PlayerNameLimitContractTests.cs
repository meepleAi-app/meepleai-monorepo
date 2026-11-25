using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

/// <summary>
/// #5150 — one limit for the name of a player, from the seat to the start of the game. The seat at the live
/// table took 100 characters; the start copies the active players into the correlated <c>GameSession</c>,
/// whose <c>SessionPlayer</c> takes 50: a player named with 51 to 100 characters was seated without errors,
/// and then the game did not start. The user decided on 50 everywhere (2026-11-25).
/// </summary>
/// <remarks>
/// Every step a player's name goes through is here: the seat (constructor, rename, the command that adds
/// it), the player of the correlated game session, its winner, and the tracking participant whose name the
/// go-live seats at the live table. Each one takes the limit and refuses one more.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "5150")]
public sealed class PlayerNameLimitContractTests
{
    private const int Limit = 50;

    public static TheoryData<string> Steps() => new()
    {
        "live seat", "live rename", "add-player command", "correlated game player", "correlated game winner",
        "tracking participant",
    };

    [Theory]
    [MemberData(nameof(Steps))]
    public void EveryStep_TakesANameAtTheLimit(string step)
    {
        Accepts(step, new string('n', Limit)).Should().BeTrue($"{step} must take a name of {Limit} characters");
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public void EveryStep_RefusesANameOneOverTheLimit(string step)
    {
        Accepts(step, new string('n', Limit + 1)).Should().BeFalse($"{step} must refuse a name of {Limit + 1} characters");
    }

    private static bool Accepts(string step, string name)
    {
        try
        {
            switch (step)
            {
                case "live seat":
                    _ = Seat(name);
                    return true;
                case "live rename":
                    Seat("Marta").UpdateDisplayName(name);
                    return true;
                case "add-player command":
                    return new AddPlayerToLiveSessionCommandValidator().Validate(
                        new AddPlayerToLiveSessionCommand(Guid.NewGuid(), name, PlayerColor.Red)).IsValid;
                case "correlated game player":
                    _ = new SessionPlayer(name, playerOrder: 1);
                    return true;
                case "correlated game winner":
                    var game = new GameSession(Guid.NewGuid(), Guid.NewGuid(), [new SessionPlayer("Marta", 1)]);
                    game.Start();
                    game.Complete(name);
                    return true;
                case "tracking participant":
                    _ = ParticipantInfo.Create(name, isOwner: false, joinOrder: 1);
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "unknown step");
            }
        }
        catch (Exception ex) when (ex is not ArgumentOutOfRangeException { ParamName: "step" })
        {
            return false;
        }
    }

    private static LiveSessionPlayer Seat(string name) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, name, PlayerColor.Red, PlayerRole.Player, DateTime.UtcNow);
}
