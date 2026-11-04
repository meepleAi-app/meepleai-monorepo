using Api.BoundedContexts.GameToolbox.Adapters;
using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Infrastructure.Persistence;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.GameToolbox;

/// <summary>
/// #3856 — creare un mazzo di carte in un toolbox rispondeva 500.
///
/// <para>
/// <c>CardDeckAdapter</c> passava l'id del <b>toolbox</b> alla factory che costruisce un mazzo di
/// <b>sessione</b>, e la chiave esterna verso le sessioni non poteva che rifiutarlo:
/// <c>23503 violates foreign key constraint FK_SessionDecks_session_tracking_sessions_SessionId</c>.
/// </para>
/// <para>
/// Un mazzo appartiene ora a una sessione <b>oppure</b> a un toolbox, con il vincolo
/// <c>CK_SessionDecks_Owner</c> a impedire sia il doppio proprietario sia l'assenza di proprietario
/// — lo stesso schema di <c>CK_UserLibraryEntry_GameSource</c>.
/// </para>
/// <para>
/// Il test gira su Postgres perche' cio' che si vuole provare e' proprio il comportamento dei
/// vincoli: su un provider in memoria non esistono, e il difetto non si manifesterebbe.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameToolbox")]
[Trait("Issue", "3856")]
public sealed class ToolboxCardDeckOwnershipIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ToolboxCardDeckOwnershipIntegrationTests(SharedTestcontainersFixture fixture)
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
            o.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // PERF-06, come in produzione
            o.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        return services.BuildServiceProvider().GetRequiredService<MeepleAiDbContext>();
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_toolbox_decks_{Guid.NewGuid():N}";
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

    private async Task<Guid> CreaToolboxAsync()
    {
        await using var db = NuovoContesto();
        var toolbox = Toolbox.Create("Toolbox con mazzi");
        db.Set<Toolbox>().Add(toolbox);
        await db.SaveChangesAsync(TestCancellationToken);
        return toolbox.Id;
    }

    [Fact]
    public async Task CreareUnMazzoInUnToolbox_LoSalvaConIlToolboxComeProprietario()
    {
        var toolboxId = await CreaToolboxAsync();

        await using (var db = NuovoContesto())
        {
            var collector = new Mock<IDomainEventCollector>().Object;
            var handler = new CreateCardDeckCommandHandler(
                new ToolboxRepository(db, collector),
                new CardDeckAdapter(new SessionDeckRepository(db, collector)));

            var azione = async () => await handler.Handle(
                new CreateCardDeckCommand(toolboxId, "Mazzo di prova"), TestCancellationToken);

            await azione.Should().NotThrowAsync(
                "passare l'id del toolbox alla factory delle sessioni faceva fallire la chiave " +
                "esterna e l'endpoint rispondeva 500 (#3856)");
        }

        await using (var db = NuovoContesto())
        {
            var mazzi = await db.SessionDecks
                .Where(d => d.ToolboxId == toolboxId)
                .ToListAsync(TestCancellationToken);

            mazzi.Should().HaveCount(1);
            mazzi[0].SessionId.Should().BeNull("il mazzo appartiene a un toolbox, non a una sessione");
            mazzi[0].Cards.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task UnMazzoSenzaProprietario_VieneRifiutatoDalDatabase()
    {
        await using var db = NuovoContesto();

        // Il vincolo deve valere anche per chi scrive aggirando le factory: e' l'unica garanzia
        // che sopravvive a un percorso di codice nuovo.
        var azione = async () => await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO session_tracking."SessionDecks"
                ("Id","SessionId","ToolboxId","Name","DeckType","CreatedAt","IsDeleted")
            VALUES (gen_random_uuid(), NULL, NULL, 'Orfano', 0, NOW(), false);
            """,
            TestCancellationToken);

        (await azione.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("23514",
                "CK_SessionDecks_Owner deve impedire un mazzo senza proprietario");
    }
}
