using Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;
using Api.BoundedContexts.Administration.Infrastructure.DependencyInjection;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// #4311: la scadenza dei testi di <c>ai_request_logs</c> è un servizio periodico, e gli host di integrazione li
/// tolgono tutti: nessun test d'integrazione si accorgerebbe che manca la registrazione, o che il contenitore non
/// sa costruirlo. Un servizio che non si costruisce, d'altra parte, fa fallire l'avvio dell'API.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4311")]
public sealed class AiRequestLogRetentionRegistrationTests
{
    [Fact]
    public void AddAdministrationContext_RegistersTheRetentionAsAHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddAdministrationContext(configuration);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(AiRequestLogRetentionService));
    }

    [Fact]
    public void TheContainer_CanBuildTheRetention_WithItsOptionalBatchSizeLeftToTheDefault()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddHostedService<AiRequestLogRetentionService>();
        using var provider = services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().ToList();

        hosted.Should().ContainSingle().Which.Should().BeOfType<AiRequestLogRetentionService>();
    }

    [Fact]
    public void TheDefaultRetention_IsDeclaredInAppsettings_AndMatchesTheCode()
    {
        // Il valore che il codice usa se la chiave manca e quello scritto in appsettings.json non devono divergere:
        // chi legge il file deve vedere la scadenza che gira davvero.
        var appsettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        File.Exists(appsettings).Should().BeTrue("the test host copies appsettings.json next to the assembly");

        var configuration = new ConfigurationBuilder().AddJsonFile(appsettings, optional: false).Build();

        configuration.GetValue<int?>(AiRequestLogRetentionService.RetentionDaysKey)
            .Should().Be(AiRequestLogRetentionService.DefaultRetentionDays);
    }
}
