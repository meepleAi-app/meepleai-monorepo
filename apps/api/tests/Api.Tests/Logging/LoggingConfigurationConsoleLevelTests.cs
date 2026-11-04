using Api.Logging;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Serilog.Events;
using Xunit;

namespace Api.Tests.Logging;

/// <summary>
/// Il livello del sink console è sovrascrivibile da configurazione (#3768).
/// </summary>
/// <remarks>
/// <para>
/// Era cablato per ambiente — <c>Information</c> su staging, <c>Warning</c> in produzione — senza
/// alcun modo di alzarlo. Conseguenza pratica: una riga di diagnostica emessa a <c>Debug</c> viene
/// generata e poi scartata prima di arrivare a stdout, quindi `docker logs` non la mostra **mai**.
/// </para>
/// <para>
/// È costato due tentativi di raccolta del dump <c>[RAG-TUNE]</c> su staging: alzare
/// <c>Logging__LogLevel__Default=Debug</c> funziona sul livello globale e non serve a nulla, perché
/// il filtro che conta è a valle. In produzione il problema è peggiore: con il sink a
/// <c>Warning</c> nemmeno gli <c>Information</c> sono visibili, e una diagnosi su un incidente
/// reale parte cieca.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "3768")]
public sealed class LoggingConfigurationConsoleLevelTests
{
    [Fact]
    public void WithoutOverride_StagingKeepsInformation()
    {
        ResolveConsoleLevel("Staging", null).Should().Be(LogEventLevel.Information);
    }

    [Fact]
    public void WithoutOverride_ProductionKeepsWarning()
    {
        ResolveConsoleLevel("Production", null).Should().Be(LogEventLevel.Warning);
    }

    [Fact]
    public void WithoutOverride_DevelopmentKeepsDebug()
    {
        ResolveConsoleLevel("Development", null).Should().Be(LogEventLevel.Debug);
    }

    [Fact]
    public void WithOverride_StagingCanBeLoweredToDebug()
    {
        // Il caso che sblocca la diagnosi: alzare la verbosità su staging senza toccare il codice.
        ResolveConsoleLevel("Staging", "Debug").Should().Be(LogEventLevel.Debug);
    }

    [Fact]
    public void WithOverride_ProductionCanBeLoweredForAnIncident()
    {
        ResolveConsoleLevel("Production", "Information").Should().Be(LogEventLevel.Information);
    }

    [Fact]
    public void WithOverride_TheLevelCanAlsoBeRaised()
    {
        // L'override vale in entrambe le direzioni: serve anche per zittire un ambiente rumoroso.
        ResolveConsoleLevel("Development", "Warning").Should().Be(LogEventLevel.Warning);
    }

    [Fact]
    public void WithUnparsableOverride_FallsBackToTheEnvironmentDefault()
    {
        // Un valore scritto male non deve spegnere il logging né far esplodere l'avvio: si torna
        // al default dell'ambiente, che è il comportamento di oggi.
        ResolveConsoleLevel("Staging", "verboso-per-favore").Should().Be(LogEventLevel.Information);
    }

    [Fact]
    public void WithEmptyOverride_FallsBackToTheEnvironmentDefault()
    {
        ResolveConsoleLevel("Production", "  ").Should().Be(LogEventLevel.Warning);
    }

    private static LogEventLevel ResolveConsoleLevel(string environmentName, string? configuredLevel)
    {
        var settings = new Dictionary<string, string?>();
        if (configuredLevel is not null)
        {
            settings["Logging:Console:MinimumLevel"] = configuredLevel;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return LoggingConfiguration.GetConsoleLogLevel(environmentName, configuration);
    }
}
