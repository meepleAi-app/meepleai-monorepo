using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.EventHandlers;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Configuration;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.EventHandlers;

/// <summary>
/// #4698: una configurazione creata dopo una lettura a vuoto restava invisibile per la durata della cache.
/// </summary>
/// <remarks>
/// <see cref="ConfigurationService"/> mette in cache anche il «non trovato». <c>ConfigurationUpdated</c>,
/// <c>ConfigurationToggled</c> e <c>ConfigurationDeleted</c> invalidavano la voce; <c>ConfigurationCreated</c> no, quindi
/// il toggle della registrazione, la prima volta che si accende su un ambiente senza la riga, non aveva effetto per
/// cinque minuti e un secondo clic tentava di creare di nuovo la riga (23505, HTTP 500).
/// I test usano la cache vera e il servizio vero: con un doppio, o col passthrough dell'host di integrazione, la voce
/// non sarebbe mai vecchia.
/// <para>
/// Ogni voce del lettore porta il tag <c>config:category:general</c>, e l'invalidazione per tag scade qualunque voce
/// con quel tag: da sola copre tutto, e coprirebbe anche un'invalidazione per chiave sbagliata. Per questo i test sulle
/// chiavi spengono i tag (<c>EnableTags = false</c>) e lasciano al solo ciclo per chiave la possibilità di farli
/// passare; un test a parte accende i tag e prova il caso che le chiavi non possono nominare.
/// </para>
/// <para>
/// Il gestore gira dopo il commit: in linea in sviluppo, dall'outbox (polling di pochi secondi) in staging e produzione.
/// Questi test chiamano <c>Handle</c> direttamente e non vedono quella finestra.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public sealed class ConfigurationCreatedEventHandlerTests
{
    private const string Key = "Registration:PublicEnabled";

    private sealed class Fixture
    {
        public required ConfigurationService Service { get; init; }
        public required ConfigurationCreatedEventHandler Handler { get; init; }
        public required Action<ConfigurationDto?> SetRow { get; init; }
    }

    private static Fixture Create(string hostEnvironment, bool enableTags = false)
    {
        ConfigurationDto? row = null;

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<GetConfigByKeyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => row);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(hostEnvironment);

        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        var hybrid = services.BuildServiceProvider().GetRequiredService<HybridCache>();
        var cache = new HybridCacheService(
            hybrid,
            Options.Create(new HybridCacheConfiguration { EnableTags = enableTags }),
            NullLogger<HybridCacheService>.Instance);

        return new Fixture
        {
            Service = new ConfigurationService(
                mediator.Object, cache, NullLogger<ConfigurationService>.Instance, environment.Object),
            Handler = new ConfigurationCreatedEventHandler(
                TestDbContextFactory.CreateInMemoryDbContext(),
                cache,
                NullLogger<ConfigurationCreatedEventHandler>.Instance),
            SetRow = value => row = value,
        };
    }

    private static ConfigurationDto Row(string environment, string value = "true") =>
        new(
            Id: Guid.NewGuid(),
            Key: Key,
            Value: value,
            ValueType: "Boolean",
            Description: "Controls whether public registration is enabled",
            Category: "Authentication",
            IsActive: true,
            RequiresRestart: false,
            Environment: environment,
            Version: 1,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);

    private static ConfigurationCreatedEvent Created(string environment) =>
        new(
            configurationId: Guid.NewGuid(),
            key: new ConfigKey(Key),
            value: "true",
            valueType: "Boolean",
            category: "Authentication",
            environment: environment,
            requiresRestart: false,
            createdByUserId: Guid.NewGuid());

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task AKeyCreatedAfterAMiss_IsVisibleOnceTheHandlerRuns(string hostEnvironment)
    {
        var f = Create(hostEnvironment);

        // Il lettore trova la chiave assente, e la voce «assente» finisce in cache.
        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeFalse();

        // Il toggle scrive la riga. Senza un'invalidazione la cache continua a dire «assente»: è il controllo che
        // rende significativo il passo dopo.
        f.SetRow(Row(hostEnvironment));
        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeFalse(
            "prima dell'evento la lettura a vuoto è in cache");

        await f.Handler.Handle(Created(hostEnvironment), CancellationToken.None);

        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeTrue(
            "dopo la creazione la chiave deve essere visibile appena gira il gestore, non alla scadenza della cache");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task ARowForAllEnvironments_IsVisibleToEveryHost(string hostEnvironment)
    {
        // Una riga con ambiente «All» vale per ogni host, ma il lettore la cerca (e mette in cache l'assenza) sotto
        // il nome dell'ambiente dell'host: l'invalidazione deve raggiungere quel nome, non solo «All».
        var f = Create(hostEnvironment);
        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeFalse();
        f.SetRow(Row("All"));
        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeFalse(
            "prima dell'evento la lettura a vuoto è in cache");

        await f.Handler.Handle(Created("All"), CancellationToken.None);

        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeTrue();
    }

    [Fact]
    public async Task ACreationInACustomEnvironment_IsVisibleToThatHost()
    {
        // Un ambiente che non è uno dei tre noti (per esempio un ambiente di anteprima) ha la sua voce in cache:
        // l'ambiente dell'evento va invalidato anche se non sta nell'elenco fisso.
        var f = Create("Preview");
        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeFalse();
        f.SetRow(Row("Preview"));
        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeFalse(
            "prima dell'evento la lettura a vuoto è in cache");

        await f.Handler.Handle(Created("Preview"), CancellationToken.None);

        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeTrue();
    }

    [Fact]
    public async Task ARowForAllEnvironments_ReachesAHostNoKeyNamesThroughTheTag()
    {
        // Host con un nome che né l'elenco fisso né l'evento («All») nominano: le chiavi non lo raggiungono, resta il
        // tag. Qui i tag sono accesi, perché è l'unico test in cui il tag è l'unico modo di passare.
        var f = Create("Preview", enableTags: true);
        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeFalse();
        f.SetRow(Row("All"));
        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeFalse(
            "prima dell'evento la lettura a vuoto è in cache");

        await f.Handler.Handle(Created("All"), CancellationToken.None);

        (await f.Service.GetValueAsync(Key, defaultValue: false)).Should().BeTrue();
    }
}
