using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// <see cref="FinalizeSessionCommand"/> is the BODY of the public <c>POST /game-sessions/{id}/finalize</c>
/// (the minimal API binds it with <c>System.Text.Json</c>, web defaults), so <c>NoWinner</c> — added by
/// #4752 — is part of that endpoint's contract. What a client that has never heard of it sends must keep
/// meaning what it meant: the rank-1 participant is the winner.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class FinalizeSessionCommandContractTests
{
    private static readonly Guid SessionId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid FirstParticipant = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static FinalizeSessionCommand Bind(string noWinnerFragment) =>
        JsonSerializer.Deserialize<FinalizeSessionCommand>(
            $$"""
            {
              "sessionId": "{{SessionId}}",
              "finalRanks": { "{{FirstParticipant}}": 1 },
              "requestedBy": "{{Guid.Empty}}"{{noWinnerFragment}}
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void ABodyWithoutNoWinner_BindsAsFalse_SoExistingClientsKeepTheirMeaning()
    {
        var command = Bind(noWinnerFragment: string.Empty);

        command.NoWinner.Should().BeFalse();
        command.FinalRanks.Should().ContainKey(FirstParticipant).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ABodyWithNoWinnerTrue_BindsAsTrue()
    {
        Bind(noWinnerFragment: ", \"noWinner\": true").NoWinner.Should().BeTrue();
    }

    [Fact]
    public void ABodyWithNoWinnerFalse_BindsAsFalse()
    {
        Bind(noWinnerFragment: ", \"noWinner\": false").NoWinner.Should().BeFalse();
    }
}
