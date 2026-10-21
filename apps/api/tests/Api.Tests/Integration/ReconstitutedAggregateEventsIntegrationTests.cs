using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perché e le condizioni per
/// condividere il database stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perché condividere il database è sicuro QUI.</b> Ogni test crea il suo utente e i suoi aggregati, con id
/// suoi, e legge dall'outbox solo le righe che citano quegli id. Nessun conteggio e nessuna lista completa.
/// </para>
/// </summary>
public sealed class ReconstitutedAggregateEventsHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "reconstituted_events");

/// <summary>
/// #4613: gli eventi che un aggregato letto dal repository e salvato di nuovo scrive nell'outbox, su Postgres.
/// </summary>
/// <remarks>
/// <para>
/// Prima della correzione, il repository ricostruiva <c>GameSession</c>, <c>ChatSession</c>, <c>AgentSession</c> e
/// <c>OAuthAccount</c> con il costruttore pubblico, che solleva l'evento «creato». Ogni aggregato letto lo portava già,
/// e il primo <c>UpdateAsync</c> lo consegnava al collector insieme all'evento vero: nell'outbox finivano due righe, e
/// la prima era falsa. Per <c>ChatSession</c> l'evento è anche nel registro degli alias: finiva in
/// <c>domain_event_logs</c>, da cui legge il feed attività.
/// </para>
/// <para>
/// Ogni test crea l'aggregato dal repository (che scrive l'evento «creato» vero), lo rilegge in un altro scope, fa un
/// cambiamento vero e confronta l'outbox <b>prima e dopo</b> il salvataggio, per id: dice cosa ha scritto quel
/// salvataggio, non cosa c'era già. Nell'host dei test nessun gestore di eventi di dominio parte, perché manca il
/// processore dell'outbox: i test guardano le righe, non l'audit.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Issue", "4613")]
public sealed class ReconstitutedAggregateEventsIntegrationTests : IClassFixture<ReconstitutedAggregateEventsHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ReconstitutedAggregateEventsIntegrationTests(ReconstitutedAggregateEventsHostFixture host)
    {
        _factory = host.Factory;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ========================================
    // GameSession
    // ========================================

    [Fact]
    public async Task GameSession_ReadAndStarted_WritesOnlyTheStartedEvent()
    {
        var (userId, gameId) = await SeedUserAndGameAsync();
        var sessionId = Guid.NewGuid();
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IGameSessionRepository>()
                .AddAsync(new GameSession(sessionId, gameId, [new SessionPlayer("Alice", 1)], userId), Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });
        (await OutboxEventTypesAsync(sessionId)).Should().BeEquivalentTo([EventType<GameSessionCreatedEvent>()], "setup: the creation is recorded once, by the repository");

        var written = await EventsWrittenByAsync([sessionId], async () => await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IGameSessionRepository>();
            var session = await repository.GetByIdAsync(sessionId, Ct);
            session!.Start();
            await repository.UpdateAsync(session, Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }));

        written.Should().BeEquivalentTo([EventType<GameSessionStartedEvent>()],
            "a start records the start: the session was created long ago, and said so then");
    }

    // ========================================
    // ChatSession
    // ========================================

    [Fact]
    public async Task ChatSession_ReadAndGivenAMessage_WritesOnlyTheMessageAddedEvent_AndNoSecondCreatedRowInTheLog()
    {
        var (userId, gameId) = await SeedUserAndGameAsync();
        var sessionId = Guid.NewGuid();
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IChatSessionRepository>()
                .AddAsync(new ChatSession(sessionId, userId, gameId, "titolo", gameName: "Gioco di prova"), Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });
        (await OutboxEventTypesAsync(sessionId)).Should().BeEquivalentTo([EventType<ChatSessionCreatedEvent>()], "setup: the creation is recorded once, by the repository");
        (await CreatedLogRowsAsync(sessionId)).Should().Be(1, "setup: and so is its row in the activity log");

        var written = await EventsWrittenByAsync([sessionId], async () => await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IChatSessionRepository>();
            var session = await repository.GetByIdAsync(sessionId, Ct);
            session!.AddUserMessage("una domanda");
            await repository.UpdateAsync(session, Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }));

        // Entrambe le asserzioni, anche se la prima fallisce: la seconda è l'effetto che si vede nel feed.
        using (new AssertionScope())
        {
            written.Should().BeEquivalentTo([EventType<ChatSessionMessageAddedEvent>()],
                "a message records the message: the chat was created long ago, and said so then");
            (await CreatedLogRowsAsync(sessionId)).Should().Be(1,
                "chat.session.created feeds the activity feed: a second row would show the chat as created again at every message");
        }
    }

    // ========================================
    // AgentSession
    // ========================================

    [Fact]
    public async Task AgentSession_ReadAndEnded_WritesOnlyTheEndedEvent()
    {
        var (userId, gameId) = await SeedUserAndGameAsync();
        var gameSessionId = Guid.NewGuid();
        var agentDefinitionId = await SeedAgentDefinitionAsync();
        var agentSessionId = Guid.NewGuid();
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IGameSessionRepository>()
                .AddAsync(new GameSession(gameSessionId, gameId, [new SessionPlayer("Alice", 1)], userId), Ct);
            await sp.GetRequiredService<IAgentSessionRepository>()
                .AddAsync(new AgentSession(agentSessionId, agentDefinitionId, gameSessionId, userId, gameId, GameState.Initial(userId)), Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });
        (await OutboxEventTypesAsync(agentSessionId)).Should().BeEquivalentTo([EventType<AgentSessionCreatedEvent>()], "setup: the creation is recorded once, by the repository");

        var written = await EventsWrittenByAsync([agentSessionId], async () => await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IAgentSessionRepository>();
            var session = await repository.GetByIdAsync(agentSessionId, Ct);
            session!.End();
            await repository.UpdateAsync(session, Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }));

        written.Should().BeEquivalentTo([EventType<AgentSessionEndedEvent>()],
            "ending records the end: the agent session was created long ago, and said so then");
    }

    [Fact]
    public async Task AgentSession_ReadAndGivenAnotherDefinition_SavesTheDefinition()
    {
        var (userId, gameId) = await SeedUserAndGameAsync();
        var gameSessionId = Guid.NewGuid();
        var firstDefinition = await SeedAgentDefinitionAsync();
        var secondDefinition = await SeedAgentDefinitionAsync();
        var agentSessionId = Guid.NewGuid();
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IGameSessionRepository>()
                .AddAsync(new GameSession(gameSessionId, gameId, [new SessionPlayer("Alice", 1)], userId), Ct);
            await sp.GetRequiredService<IAgentSessionRepository>()
                .AddAsync(new AgentSession(agentSessionId, firstDefinition, gameSessionId, userId, gameId, GameState.Initial(userId)), Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IAgentSessionRepository>();
            var session = await repository.GetByIdAsync(agentSessionId, Ct);
            session!.UpdateAgentDefinition(secondDefinition);
            await repository.UpdateAsync(session, Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        var reread = await InScopeAsync(sp => sp.GetRequiredService<IAgentSessionRepository>().GetByIdAsync(agentSessionId, Ct));
        reread!.AgentDefinitionId.Should().Be(secondDefinition,
            "the command changed the aggregate and announced it; the row has to follow");
    }

    // ========================================
    // OAuthAccount
    // ========================================

    [Fact]
    public async Task OAuthAccount_ReadAndTokensRefreshed_WritesOnlyTheTokensRefreshedEvent()
    {
        var (userId, _) = await SeedUserAndGameAsync();
        var accountId = Guid.NewGuid();
        await InScopeAsync(async sp =>
        {
            await sp.GetRequiredService<IOAuthAccountRepository>()
                .AddAsync(new OAuthAccount(accountId, userId, "github", $"gh-{Guid.NewGuid():N}", "token-cifrato"), Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });
        // L'evento «collegato» porta l'utente, non l'id del collegamento: si cerca per utente.
        (await OutboxEventTypesAsync(userId)).Should().BeEquivalentTo([EventType<OAuthAccountLinkedEvent>()], "setup: the link is recorded once, by the repository");

        var written = await EventsWrittenByAsync([accountId, userId], async () => await InScopeAsync(async sp =>
        {
            var repository = sp.GetRequiredService<IOAuthAccountRepository>();
            var account = await repository.GetByIdAsync(accountId, Ct);
            account!.UpdateTokens("token-nuovo", "refresh-nuovo", DateTime.UtcNow.AddHours(1));
            await repository.UpdateAsync(account, Ct);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }));

        written.Should().BeEquivalentTo([EventType<OAuthTokensRefreshedEvent>()],
            "a refresh records the refresh: the account was linked long ago, and said so then");
    }

    // ========================================
    // SUPPORTO
    // ========================================

    /// <summary>Il tipo con cui l'outbox registra <typeparamref name="TEvent"/>: l'alias del registro, o il nome completo.</summary>
    private static string EventType<TEvent>() where TEvent : IDomainEvent =>
        EventTypeRegistry.AliasByType.TryGetValue(typeof(TEvent), out var alias) ? alias : typeof(TEvent).FullName!;

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    private async Task InScopeAsync(Func<IServiceProvider, Task> action) =>
        await InScopeAsync(async sp =>
        {
            await action(sp);
            return 0;
        });

    private async Task<(Guid UserId, Guid GameId)> SeedUserAndGameAsync()
    {
        return await InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MeepleAiDbContext>();
            var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
            var gameId = await TestSessionHelper.SeedSharedGameAsync(db, $"Gioco {Guid.NewGuid():N}");
            return (userId, gameId);
        });
    }

    private async Task<Guid> SeedAgentDefinitionAsync()
    {
        return await InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MeepleAiDbContext>();
            var definition = AgentDefinition.Create(
                name: $"Agente {Guid.NewGuid():N}",
                description: "Test #4613",
                config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));
            definition.Activate();
            db.AgentDefinitions.Add(definition);
            await db.SaveChangesAsync(Ct);
            return definition.Id;
        });
    }

    /// <summary>Righe di <c>domain_event_logs</c> «chat.session.created» di una chat.</summary>
    private async Task<int> CreatedLogRowsAsync(Guid sessionId) =>
        await InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MeepleAiDbContext>();
            return await db.DomainEventLogs.AsNoTracking()
                .CountAsync(l => l.AggregateId == sessionId && l.EventType == "chat.session.created", Ct);
        });

    /// <summary>Tipo degli eventi che <paramref name="action"/> ha scritto nell'outbox per gli id dati.</summary>
    private async Task<List<string>> EventsWrittenByAsync(Guid[] ids, Func<Task> action)
    {
        var before = (await OutboxRowsAsync(ids)).Select(r => r.Id).ToHashSet();
        await action();
        return (await OutboxRowsAsync(ids)).Where(r => !before.Contains(r.Id)).Select(r => r.EventType).ToList();
    }

    private async Task<List<string>> OutboxEventTypesAsync(params Guid[] ids) =>
        (await OutboxRowsAsync(ids)).Select(r => r.EventType).ToList();

    private async Task<List<(Guid Id, string EventType)>> OutboxRowsAsync(params Guid[] ids)
    {
        return await InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<MeepleAiDbContext>();
            // The payload is jsonb, which LIKE does not accept: the filter on the id runs in memory. The table only
            // holds what the tests of this class wrote.
            var rows = await db.DomainEventOutbox.AsNoTracking()
                .Select(o => new { o.Id, o.EventType, o.PayloadJson })
                .ToListAsync(Ct);
            var needles = ids.Select(id => id.ToString()).ToArray();
            return rows
                .Where(o => needles.Any(n => o.PayloadJson.Contains(n, StringComparison.OrdinalIgnoreCase)))
                .Select(o => (o.Id, o.EventType))
                .ToList();
        });
    }
}
