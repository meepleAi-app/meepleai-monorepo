using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.SystemConfiguration;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perché e le condizioni per
/// condividere il database stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perché condividere il database è sicuro QUI.</b> Ogni test crea la sua configurazione, con una chiave
/// e un id suoi, e legge dall'outbox solo le righe che citano quell'id. Nessun conteggio e nessuna lista completa.
/// </para>
/// </summary>
public sealed class ConfigurationDomainEventsHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "cfg_domain_events");

/// <summary>
/// #4608: gli eventi di dominio che una configurazione scrive nell'outbox, dal comando alla riga.
/// </summary>
/// <remarks>
/// <para>
/// Prima della correzione, <c>ConfigurationRepository.MapToDomain</c> ricostruiva la riga con il costruttore
/// pubblico di <c>SystemConfiguration</c>, che solleva <c>ConfigurationCreatedEvent</c>. Ogni update, activate,
/// deactivate e rollback scriveva quindi <c>[Created, evento vero]</c>, e il gestore generico dell'audit
/// (<c>DomainEventAuditHandler</c>) registrava una riga «creata» falsa, attribuita a chi aveva creato la
/// configurazione e non a chi l'aveva modificata. <c>DeleteAsync</c> non consegnava al collector il
/// <c>ConfigurationDeletedEvent</c>: la cancellazione non lasciava traccia.
/// </para>
/// <para>
/// I test mandano il comando vero a <c>IMediator</c> (handler, repository, unità di lavoro) e leggono
/// <c>domain_event_outbox</c>, dove <c>SaveChangesAsync</c> scrive gli eventi nella stessa transazione della riga.
/// Non guardano l'audit: nell'host dei test nessun gestore di eventi di dominio parte, perché manca il processore
/// dell'outbox. Il tipo che l'outbox registra per questi eventi è il nome completo, perché non sono nel registro
/// degli alias; il test lo chiede al registro, così segue il registro se un giorno li aggiunge.
/// </para>
/// <para>
/// Ogni asserzione sugli eventi di un'operazione confronta l'outbox <b>prima e dopo</b> l'operazione, per id di
/// configurazione: dice cosa ha scritto quell'operazione, non cosa c'era già.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4608")]
public sealed class ConfigurationDomainEventsIntegrationTests : IClassFixture<ConfigurationDomainEventsHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConfigurationDomainEventsIntegrationTests(ConfigurationDomainEventsHostFixture host)
    {
        _factory = host.Factory;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ========================================
    // CREAZIONE: l'evento che deve restare
    // ========================================

    [Fact]
    public async Task Create_WritesExactlyOneCreatedEvent()
    {
        var admin = await CreateUserAsync();

        // L'id nasce dentro il comando: non c'è un «prima», e l'outbox di quell'id è vuota per costruzione.
        var created = await SendAsync(NewCreateCommand(admin));

        (await OutboxEventTypesAsync(created.Id)).Should().BeEquivalentTo([EventType<ConfigurationCreatedEvent>()]);
    }

    // ========================================
    // MODIFICA: solo l'evento vero (il difetto: [Created, evento vero])
    // ========================================

    [Fact]
    public async Task UpdateValue_WritesOnlyTheUpdatedEvent()
    {
        var admin = await CreateUserAsync();
        var config = await SendAsync(NewCreateCommand(admin));

        var written = await EventsWrittenByAsync(config.Id, () => SendAsync(new UpdateConfigValueCommand(config.Id, "v2", admin)));

        written.Should().BeEquivalentTo([EventType<ConfigurationUpdatedEvent>()],
            "an update records the update: the configuration was created long before, and said so then");
    }

    [Fact]
    public async Task Deactivate_WritesOnlyTheToggledEvent()
    {
        var admin = await CreateUserAsync();
        var config = await SendAsync(NewCreateCommand(admin));

        var written = await EventsWrittenByAsync(config.Id, () => SendAsync(new ToggleConfigurationCommand(config.Id, IsActive: false)));

        written.Should().BeEquivalentTo([EventType<ConfigurationToggledEvent>()]);
    }

    [Fact]
    public async Task Activate_AfterDeactivate_WritesOnlyTheToggledEvent()
    {
        var admin = await CreateUserAsync();
        var config = await SendAsync(NewCreateCommand(admin));
        await SendAsync(new ToggleConfigurationCommand(config.Id, IsActive: false));

        var written = await EventsWrittenByAsync(config.Id, () => SendAsync(new ToggleConfigurationCommand(config.Id, IsActive: true)));

        written.Should().BeEquivalentTo([EventType<ConfigurationToggledEvent>()]);
    }

    [Fact]
    public async Task Toggle_ToTheStateTheConfigurationAlreadyHas_WritesNoEvent()
    {
        // Activating an active configuration changes nothing, so the aggregate raises nothing. Before #4608 this
        // still wrote one event: the «created» that the rebuilt aggregate carried.
        var admin = await CreateUserAsync();
        var config = await SendAsync(NewCreateCommand(admin));

        var written = await EventsWrittenByAsync(config.Id, () => SendAsync(new ToggleConfigurationCommand(config.Id, IsActive: true)));

        written.Should().BeEmpty("nothing happened to the configuration");
    }

    [Fact]
    public async Task Rollback_WritesOnlyTheUpdatedEvent()
    {
        var admin = await CreateUserAsync();
        var config = await SendAsync(NewCreateCommand(admin));
        await SendAsync(new UpdateConfigValueCommand(config.Id, "v2", admin));

        var written = await EventsWrittenByAsync(config.Id, () => SendAsync(new RollbackConfigCommand(config.Id, TargetVersion: 1, admin)));

        written.Should().BeEquivalentTo([EventType<ConfigurationUpdatedEvent>()], "a rollback is an update");
    }

    [Fact]
    public async Task BulkUpdate_WritesOneUpdatedEventPerConfiguration_AndNoCreatedEvent()
    {
        var admin = await CreateUserAsync();
        var first = await SendAsync(NewCreateCommand(admin));
        var second = await SendAsync(NewCreateCommand(admin));

        var before = await OutboxRowIdsAsync(first.Id, second.Id);
        await SendAsync(new BulkUpdateConfigsCommand([new ConfigurationUpdate(first.Id, "v2"), new ConfigurationUpdate(second.Id, "v2")], admin));
        var written = (await OutboxRowsAsync(first.Id, second.Id)).Where(r => !before.Contains(r.Id)).Select(r => r.EventType).ToList();

        written.Should().BeEquivalentTo([EventType<ConfigurationUpdatedEvent>(), EventType<ConfigurationUpdatedEvent>()]);
    }

    [Fact]
    public async Task ReadingAConfigurationAndSavingItBack_WritesNoEvent()
    {
        // The root of the defect: the aggregate that comes out of the repository is already carrying an event.
        var admin = await CreateUserAsync();
        var config = await SendAsync(NewCreateCommand(admin));

        var written = await EventsWrittenByAsync(config.Id, async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IConfigurationRepository>();
            var loaded = await repository.GetByIdAsync(config.Id, Ct);
            await repository.UpdateAsync(loaded!, Ct);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        });

        written.Should().BeEmpty("a configuration that is read and saved unchanged has no history to add");
    }

    // ========================================
    // CANCELLAZIONE: l'evento che mancava
    // ========================================

    [Fact]
    public async Task Delete_WritesTheDeletedEvent()
    {
        var admin = await CreateUserAsync();
        var config = await SendAsync(NewCreateCommand(admin));

        var deleted = false;
        var written = await EventsWrittenByAsync(config.Id, async () => deleted = await SendAsync(new DeleteConfigurationCommand(config.Id)));

        using (new AssertionScope())
        {
            deleted.Should().BeTrue("setup: the configuration existed");
            (await RowExistsAsync(config.Id)).Should().BeFalse("setup: the row is gone");
            written.Should().BeEquivalentTo([EventType<ConfigurationDeletedEvent>()],
                "a deletion leaves its trace: before #4608 the event stayed on the aggregate and never reached the outbox");
        }
    }

    // ========================================
    // SUPPORTO
    // ========================================

    private static CreateConfigurationCommand NewCreateCommand(Guid author) => new(
        Key: $"test.domain-events.{Guid.NewGuid():N}",
        Value: "v1",
        ValueType: "string",
        Description: "Test #4608",
        Category: "General",
        Environment: "All",
        RequiresRestart: false,
        CreatedByUserId: author);

    /// <summary>Il tipo con cui l'outbox registra <typeparamref name="TEvent"/>: l'alias del registro, o il nome completo.</summary>
    private static string EventType<TEvent>() where TEvent : IDomainEvent =>
        EventTypeRegistry.AliasByType.TryGetValue(typeof(TEvent), out var alias) ? alias : typeof(TEvent).FullName!;

    private async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> command)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, Ct);
    }

    private async Task<Guid> CreateUserAsync()
    {
        // The author of a configuration row is a foreign key to users.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        return userId;
    }

    private async Task<bool> RowExistsAsync(Guid configId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.Set<SystemConfigurationEntity>().AsNoTracking().AnyAsync(c => c.Id == configId, Ct);
    }

    /// <summary>Tipo degli eventi che <paramref name="action"/> ha scritto nell'outbox per la configurazione <paramref name="configId"/>.</summary>
    private async Task<List<string>> EventsWrittenByAsync(Guid configId, Func<Task> action)
    {
        var before = await OutboxRowIdsAsync(configId);
        await action();
        return (await OutboxRowsAsync(configId)).Where(r => !before.Contains(r.Id)).Select(r => r.EventType).ToList();
    }

    private async Task<List<string>> OutboxEventTypesAsync(Guid configId) =>
        (await OutboxRowsAsync(configId)).Select(r => r.EventType).ToList();

    private async Task<HashSet<Guid>> OutboxRowIdsAsync(params Guid[] configIds) =>
        (await OutboxRowsAsync(configIds)).Select(r => r.Id).ToHashSet();

    private async Task<List<(Guid Id, string EventType)>> OutboxRowsAsync(params Guid[] configIds)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        // The payload is jsonb, which LIKE does not accept: the filter on the id runs in memory. The table only
        // holds what the tests of this class wrote.
        var rows = await db.DomainEventOutbox.AsNoTracking()
            .Select(o => new { o.Id, o.EventType, o.PayloadJson })
            .ToListAsync(Ct);
        var needles = configIds.Select(id => id.ToString()).ToArray();
        return rows
            .Where(o => needles.Any(n => o.PayloadJson.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .Select(o => (o.Id, o.EventType))
            .ToList();
    }
}
