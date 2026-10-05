using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Issue #4076 — il verificatore della sonda sui prerequisiti Docker.
///
/// <para>🔴 Questa classe esiste per una ragione precisa: una sonda di prerequisito che
/// risponde «assente» a tutto fa saltare ogni test che la consulta, e <b>i salti non hanno una
/// baseline come i fallimenti</b> — quindi il danno resta invisibile. È la lezione di #3978,
/// dove sedici salti in più su un fondo di 64 non hanno spostato nulla che qualcuno contasse.</para>
///
/// <para>⚠️ <c>DockerImageProbe</c> <b>ritorna</b> la decisione invece di lanciare
/// <c>SkipException</c>, e questi test sono il motivo: un helper che salta al posto del chiamante
/// farebbe saltare anche il proprio verificatore, che diventerebbe verde per niente.</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4076")]
public sealed class DockerImageProbeTests
{
    [Fact]
    public void FindFirstPresent_WithNoCandidates_ReturnsNull()
    {
        DockerImageProbe.FindFirstPresent().Should().BeNull();
    }

    [Fact]
    public void FindFirstPresent_WithOnlyBlankCandidates_ReturnsNull()
    {
        // Gli ingressi vuoti non devono diventare una chiamata a `docker image inspect ""`, che
        // uscirebbe comunque != 0 ma spenderebbe un processo per nulla.
        DockerImageProbe.FindFirstPresent("", "   ", null!).Should().BeNull();
    }

    [Fact]
    public void FindFirstPresent_WithAnImageThatCannotExist_ReturnsNull()
    {
        // 🔴 Il nome contiene un segmento che nessun registry potrebbe ospitare, quindi l'esito
        // non dipende da cosa c'è sulla macchina. Il punto è che la sonda NON prova a scaricarlo:
        // `docker image inspect` interroga solo il locale, ed è la differenza che rende
        // classificabile il motivo del salto («assente in locale» ≠ «assente nel registry»).
        DockerImageProbe
            .FindFirstPresent("meepleai-test-immagine-che-non-esiste-4076:latest")
            .Should()
            .BeNull();
    }

    [Fact]
    public void FindFirstPresent_ReturnsTheFirstCandidateThatExists_NotMerelyTheFirst()
    {
        // 🔴 Il controllo al rovescio, e serve davvero: senza di esso una sonda che ritornasse
        // sempre `candidateTags[0]` passerebbe tutti i test qui sopra tranne questo, e ogni test
        // che la consulta proverebbe a partire con un'immagine inesistente — cioè il difetto di
        // #4076 esattamente al contrario.
        //
        // L'immagine `postgres` è il prerequisito L1 di questa suite: Testcontainers la usa per
        // ogni test di integrazione, quindi se Docker funziona qui essa è presente. Se Docker non
        // c'è, entrambi i rami ritornano null e l'asserzione sotto lo tollera — questo test non
        // deve diventare un secondo gate su Docker.
        var presente = DockerImageProbe.FindFirstPresent("postgres:16-alpine", "postgres:16");

        if (presente is null)
        {
            // Docker assente o nessuna delle due immagini scaricata: niente da verificare qui.
            return;
        }

        var conUnFalsoDavanti = DockerImageProbe.FindFirstPresent(
            "meepleai-test-immagine-che-non-esiste-4076:latest",
            presente);

        conUnFalsoDavanti.Should().Be(presente);
    }

    [Fact]
    public void TestcontainersConfiguration_UnstructuredImage_UsesTheInfraPrefix()
    {
        // Fissa il fatto su cui la sonda di `Performance_P95Latency_WithRealServices` si appoggia:
        // la costante dichiara il tag `infra-…`, mentre Docker Compose produce `meepleai-…`
        // (`infra/docker-compose.yml` porta `name: meepleai`). La sonda accetta entrambi proprio
        // per questo, e se qualcuno allineasse le costanti al prefisso di Compose questo test lo
        // segnalerebbe — così la lista dei candidati viene aggiornata insieme, invece di
        // diventare un doppione silenzioso.
        TestcontainersConfiguration.UnstructuredImage.Should().StartWith("infra-");
    }
}
