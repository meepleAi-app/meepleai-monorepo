using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.EntityRelationships;

/// <summary>
/// #3858 — <c>DELETE /library/entity-links/{id}</c> rispondeva 204 <b>senza cancellare</b>.
///
/// <para>
/// Il MeepleAiDbContext ha come default <c>QueryTrackingBehavior.NoTracking</c> (PERF-06): il
/// collegamento letto dal repository non era tracciato, il <c>link.Delete()</c> dell'handler
/// mutava un oggetto scollegato e <c>SaveChangesAsync</c> non trovava nulla da scrivere.
/// </para>
/// <para>
/// Il fallimento aveva <b>la forma esatta del successo</b>: 204, nessuna eccezione, nessuna riga
/// nei log. Per questo il test non guarda il valore di ritorno dell'handler — non c'e' — ma
/// <b>rilegge dal database con un contesto nuovo</b>. Un'asserzione sullo stesso contesto passerebbe
/// anche sul codice difettoso, perche' l'oggetto in memoria e' stato mutato per davvero.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "EntityRelationships")]
[Trait("Issue", "3858")]
public sealed class EntityLinkDeletePersistsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;
    private readonly Guid _ownerId = Guid.NewGuid();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public EntityLinkDeletePersistsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private MeepleAiDbContext NuovoContesto()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IMediator>(new Mock<IMediator>().Object);
        services.AddSingleton<IDomainEventCollector>(new Mock<IDomainEventCollector>().Object);
        services.AddDbContext<MeepleAiDbContext>(o =>
        {
            o.UseNpgsql(_connectionString, x => x.UseVector());

            // Riproduce PERF-06 (InfrastructureServiceExtensions.cs:180). Senza questa riga il
            // contesto di test traccia per default e il difetto NON si riproduce: la prima
            // versione di questo test passava anche sul codice rotto.
            o.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

            o.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        return services.BuildServiceProvider().GetRequiredService<MeepleAiDbContext>();
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_entity_link_delete_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        await using var db = NuovoContesto();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task Delete_MarcaLaCancellazioneSulDatabase()
    {
        Guid linkId;

        await using (var db = NuovoContesto())
        {
            var link = EntityLink.Create(
                sourceEntityType: MeepleEntityType.Game,
                sourceEntityId: Guid.NewGuid(),
                targetEntityType: MeepleEntityType.Game,
                targetEntityId: Guid.NewGuid(),
                linkType: EntityLinkType.ExpansionOf,
                scope: EntityLinkScope.User,
                ownerUserId: _ownerId);
            db.EntityLinks.Add(link);
            await db.SaveChangesAsync(TestCancellationToken);
            linkId = link.Id;
        }

        await using (var db = NuovoContesto())
        {
            var handler = new DeleteEntityLinkCommandHandler(
                new EntityLinkRepository(db, new Mock<IDomainEventCollector>().Object),
                new UnitOfWork(db),
                NullLogger<DeleteEntityLinkCommandHandler>.Instance);

            await handler.Handle(
                new DeleteEntityLinkCommand(linkId, _ownerId, IsAdmin: false),
                TestCancellationToken);
        }

        // Contesto nuovo: e' l'unico modo di distinguere "cancellato" da "mutato in memoria".
        await using (var db = NuovoContesto())
        {
            var riga = await db.EntityLinks
                .IgnoreQueryFilters()
                .SingleAsync(x => x.Id == linkId, TestCancellationToken);

            riga.IsDeleted.Should().BeTrue(
                "il DELETE rispondeva 204 lasciando is_deleted a false (#3858)");
            riga.DeletedAt.Should().NotBeNull();

            // Il filtro globale deve nasconderlo: e' cio' che l'utente vede ricaricando la pagina.
            var visibile = await db.EntityLinks
                .AnyAsync(x => x.Id == linkId, TestCancellationToken);
            visibile.Should().BeFalse("dopo la cancellazione il collegamento non deve piu' comparire");
        }
    }
}
