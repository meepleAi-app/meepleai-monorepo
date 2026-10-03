using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// #4050 — il meccanismo su cui poggia <see cref="SharedHostPerTestDatabaseFixture"/>.
/// </summary>
/// <remarks>
/// Sono <c>Category=Unit</c>: non servono né Docker né un host. Se il meccanismo si rompesse, il
/// sintomo altrimenti sarebbe che ogni test di una classe convertita gira contro il database del
/// primo — cioè test che passano o falliscono per ragioni che non hanno nulla a che vedere con il
/// loro oggetto, su un gate che costa 90 minuti. Qui costa millisecondi.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4050")]
public sealed class MutableConnectionStringSourceTests
{
    private const string Key = "ConnectionStrings:DefaultConnection";

    private static (IConfiguration Config, MutableConnectionStringSource Source) BuildLikeTheFactory(
        string baked)
    {
        // Rispecchia l'ordine di IntegrationWebApplicationFactory: prima il dizionario in memoria
        // con la stringa «cablata», poi la sorgente mutabile. L'ordine È la cosa sotto test.
        var source = new MutableConnectionStringSource();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Key] = baked })
            .Add(source)
            .Build();
        return (config, source);
    }

    [Fact]
    public void SenzaValoreImpostato_VinceLaStringaCablata()
    {
        // Il caso che garantisce di non rompere i chiamanti esistenti: una sorgente vuota non deve
        // cancellare la connessione passata a Create().
        var (config, _) = BuildLikeTheFactory("Host=cablata");

        config.GetConnectionString("DefaultConnection").Should().Be("Host=cablata");
    }

    [Fact]
    public void ImpostataDopoLaCostruzione_VinceSullaStringaCablata()
    {
        // 🔴 L'asserzione centrale: la sorgente è aggiunta PER ULTIMA, quindi vince. Se qualcuno la
        // spostasse prima dell'AddInMemoryCollection, questo test cadrebbe — ed è l'unico modo di
        // accorgersene senza far girare il gate da 90 minuti.
        var (config, source) = BuildLikeTheFactory("Host=cablata");

        source.Provider.SetConnectionString("Host=primo_test");

        config.GetConnectionString("DefaultConnection").Should().Be("Host=primo_test");
    }

    [Fact]
    public void CambiataDiNuovo_LaLetturaSuccessivaVedeIlNuovoValore()
    {
        // È il ciclo reale: un database per test, letto a ogni scope.
        var (config, source) = BuildLikeTheFactory("Host=cablata");

        source.Provider.SetConnectionString("Host=t1");
        var primo = config.GetConnectionString("DefaultConnection");
        source.Provider.SetConnectionString("Host=t2");
        var secondo = config.GetConnectionString("DefaultConnection");

        primo.Should().Be("Host=t1");
        secondo.Should().Be("Host=t2",
            "senza questo ogni test girerebbe contro il database del primo, e il sintomo sarebbe "
            + "un fallimento che non parla del test");
    }

    [Fact]
    public void IlCambioNotificaIToken_perChiSiERegistrato()
    {
        // GetConnectionString rilegge i provider a ogni chiamata e non ha bisogno della notifica.
        // Ma chi si è registrato su IOptionsMonitor o GetReloadToken resterebbe sul valore vecchio
        // SENZA dirlo: un difetto silenzioso, quindi va fissato.
        var (config, source) = BuildLikeTheFactory("Host=cablata");
        var notificato = false;
        config.GetReloadToken().RegisterChangeCallback(_ => notificato = true, null);

        source.Provider.SetConnectionString("Host=nuovo");

        notificato.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnValoreVuoto_EUnErrore_nonUnAzzeramento(string? valore)
    {
        // Azzerare silenziosamente farebbe ricadere la lettura sulla stringa cablata, cioè sul
        // database di un altro test: meglio un'eccezione qui che un test verde per il motivo
        // sbagliato.
        var (_, source) = BuildLikeTheFactory("Host=cablata");

        var act = () => source.Provider.SetConnectionString(valore!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CurrentRiportaCioCheEStatoImpostato()
    {
        var (_, source) = BuildLikeTheFactory("Host=cablata");

        source.Provider.Current.Should().BeNull();
        source.Provider.SetConnectionString("Host=x");
        source.Provider.Current.Should().Be("Host=x");
    }
}
