using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Infrastructure.Persistence;
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
/// #3857 — aggiungere una fase a un toolbox falliva con <b>409 concurrent_edit</b> su un toolbox
/// appena creato, senza alcuna richiesta concorrente.
///
/// <para>
/// Non era concorrenza. Il DbContext ha come default <c>QueryTrackingBehavior.NoTracking</c>
/// (PERF-06), quindi il grafo letto dal repository era scollegato; su un grafo scollegato
/// <c>DbSet.Update()</c> marca <b>Modified</b> ogni entita' con la chiave valorizzata, e la fase
/// appena creata ne ha una generata dal client. EF emetteva un UPDATE su una riga inesistente,
/// zero righe aggiornate, <c>DbUpdateConcurrencyException</c> — che l'endpoint traduceva in 409.
/// </para>
/// <para>
/// Il test riproduce <b>NoTracking</b> nel contesto: senza, il difetto non si manifesta affatto e
/// il test passerebbe anche sul codice rotto. E rilegge da un contesto nuovo, perche' un'asserzione
/// sullo stesso contesto non distingue "salvato" da "aggiunto alla collezione in memoria".
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameToolbox")]
[Trait("Issue", "3857")]
public sealed class ToolboxChildWritesPersistIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ToolboxChildWritesPersistIntegrationTests(SharedTestcontainersFixture fixture)
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

            // Riproduce PERF-06 (InfrastructureServiceExtensions.cs:180). Senza, il contesto di
            // test traccia per default e il difetto sparisce.
            o.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

            o.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        return services.BuildServiceProvider().GetRequiredService<MeepleAiDbContext>();
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_toolbox_children_{Guid.NewGuid():N}";
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
    public async Task AggiungerePiuFasi_LeSalvaTutte()
    {
        Guid toolboxId;

        await using (var db = NuovoContesto())
        {
            var toolbox = Toolbox.Create("Toolbox di prova");
            db.Set<Toolbox>().Add(toolbox);
            await db.SaveChangesAsync(TestCancellationToken);
            toolboxId = toolbox.Id;
        }

        // Due fasi in due unita' di lavoro distinte: la seconda prova anche che la prima non
        // abbia lasciato il grafo in uno stato che rompe la successiva.
        foreach (var nome in new[] { "Preparazione", "Turno" })
        {
            await using var db = NuovoContesto();
            var handler = new AddPhaseCommandHandler(new ToolboxRepository(db, new Mock<IDomainEventCollector>().Object));

            var azione = async () => await handler.Handle(
                new AddPhaseCommand(toolboxId, nome), TestCancellationToken);

            await azione.Should().NotThrowAsync(
                $"aggiungere la fase '{nome}' rispondeva 409 su un conflitto inesistente (#3857)");
        }

        await using (var db = NuovoContesto())
        {
            var salvate = await db.Set<Toolbox>()
                .Include(t => t.Phases)
                .Where(t => t.Id == toolboxId)
                .SelectMany(t => t.Phases)
                .Select(p => p.Name)
                .ToListAsync(TestCancellationToken);

            salvate.Should().BeEquivalentTo(["Preparazione", "Turno"],
                "le fasi devono essere INSERITE, non aggiornate: su un grafo scollegato venivano " +
                "marcate Modified e l'UPDATE non trovava alcuna riga");
        }
    }
}
