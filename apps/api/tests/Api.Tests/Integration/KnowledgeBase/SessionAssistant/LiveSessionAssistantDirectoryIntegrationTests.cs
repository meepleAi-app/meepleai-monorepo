using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase.SessionAssistant;

/// <summary>
/// Scostamento S1 del piano 2, contro il dominio vero: il round che il lettore deriva segue l'indice di
/// turno a base 1 di <c>LiveGameSession</c>, e il giocatore di turno è quello che dice il dominio.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class LiveSessionAssistantDirectoryIntegrationTests(SessionAssistantHostFixture host)
    : IClassFixture<SessionAssistantHostFixture>, IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await host.BeginTestAsync();
        host.Llm.Reset();
        host.Hub.Reset();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<AssistantSessionState> ReadAsync(Guid sessionId, Guid ownerId)
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAssistantSessionDirectory>()
            .CreateReader(sessionId, ownerId).GetStateAsync(Ct);
    }

    [Fact]
    public async Task TheRound_FollowsTheTurnOrderOfTheRealSession()
    {
        var (owner, _) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        var table = await SessionAssistantTestData.CreateTableAsync(host.Factory, owner, start: true, Ct, "Marco", "Giulia", "Luca");
        await SessionAssistantTestData.SendAsync(
            host.Factory, new RecordLiveSessionScoreCommand(table.SessionId, table.Players["Marco"], 1, "punti", 7), Ct);

        // Appena avviata: indice 1, tocca al primo, primo giro.
        var first = await ReadAsync(table.SessionId, owner);
        first.CurrentTurnIndex.Should().Be(1);
        first.CurrentRound.Should().Be(1);
        first.CurrentTurnPlayerId.Should().Be(table.Players["Marco"]);

        // Due passaggi: tocca all'ultimo, ed è ancora il primo giro (con la formula a base 0 era il secondo).
        await SessionAssistantTestData.SendAsync(host.Factory, new AdvanceLiveSessionTurnCommand(table.SessionId), Ct);
        await SessionAssistantTestData.SendAsync(host.Factory, new AdvanceLiveSessionTurnCommand(table.SessionId), Ct);
        var last = await ReadAsync(table.SessionId, owner);
        last.CurrentTurnIndex.Should().Be(3);
        last.CurrentRound.Should().Be(1);
        last.CurrentTurnPlayerId.Should().Be(table.Players["Luca"]);

        // Un altro passaggio: si ricomincia dal primo, secondo giro.
        await SessionAssistantTestData.SendAsync(host.Factory, new AdvanceLiveSessionTurnCommand(table.SessionId), Ct);
        var second = await ReadAsync(table.SessionId, owner);
        second.CurrentTurnIndex.Should().Be(4);
        second.CurrentRound.Should().Be(2);
        second.CurrentTurnPlayerId.Should().Be(table.Players["Marco"]);

        second.Players.Select(p => p.Name).Should().BeEquivalentTo("Marco", "Giulia", "Luca");
        second.Players.Single(p => p.Name == "Marco").Total.Should().Be(7);
        second.Dimensions.Should().Equal("punti");
        second.Scores.Should().Equal(new AssistantScoreCell(table.Players["Marco"], 1, "punti", 7));
        second.Status.Should().Be("InProgress");
        second.GameId.Should().BeNull();
        second.HouseRules.Should().BeEmpty();
    }

    [Fact]
    public async Task Access_FollowsTheLifeOfTheSession()
    {
        var (owner, _) = await SessionAssistantTestData.SeedHostAsync(host.Factory, Ct);
        var table = await SessionAssistantTestData.CreateTableAsync(host.Factory, owner, start: false, Ct, "Marco");

        async Task<AssistantSessionAccess> AccessAsync()
        {
            await using var scope = host.Factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IAssistantSessionDirectory>().GetAccessAsync(table.SessionId, Ct);
        }

        (await AccessAsync()).Should().Be(new AssistantSessionAccess(owner, "Created", true));

        await SessionAssistantTestData.SendAsync(
            host.Factory,
            new StartLiveSessionCommand(table.SessionId, owner, Api.SharedKernel.Domain.ValueObjects.UserTier.Free, Api.SharedKernel.Domain.ValueObjects.Role.User),
            Ct);
        (await AccessAsync()).Should().Be(new AssistantSessionAccess(owner, "InProgress", true));

        await SessionAssistantTestData.SendAsync(host.Factory, new CompleteLiveSessionCommand(table.SessionId), Ct);
        (await AccessAsync()).Should().Be(new AssistantSessionAccess(owner, "Completed", false));
    }

    [Fact]
    public async Task Access_ToAnUnknownSession_IsNotFound()
    {
        await using var scope = host.Factory.Services.CreateAsyncScope();
        var directory = scope.ServiceProvider.GetRequiredService<IAssistantSessionDirectory>();

        var act = () => directory.GetAccessAsync(Guid.NewGuid(), Ct);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
