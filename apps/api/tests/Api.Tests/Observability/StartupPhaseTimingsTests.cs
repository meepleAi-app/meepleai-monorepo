using System;
using System.Globalization;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.Observability;

/// <summary>
/// #4572: le fasi dell'avvio che Program registra e che le fixture di test riportano nella riga
/// <c>host-phases</c>. Un formato che cambiasse col locale renderebbe illeggibili le righe greppate
/// dai log di CI.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Area", "Observability")]
public class StartupPhaseTimingsTests
{
    [Fact]
    public void Mark_ChiudeLeFasiInOrdine_ConLaDurataDallaChiamataPrecedente()
    {
        var time = new FakeTimeProvider();
        var timings = new StartupPhaseTimings(time);

        time.Advance(TimeSpan.FromSeconds(8));
        timings.Mark("build");
        time.Advance(TimeSpan.FromSeconds(30));
        timings.Mark("seeding");

        timings.Phases.Should().Equal(("build", 8.0), ("seeding", 30.0));
    }

    [Fact]
    public void ToString_UsaIlPuntoQualunqueSiaLaCultura()
    {
        var time = new FakeTimeProvider();
        var timings = new StartupPhaseTimings(time);
        time.Advance(TimeSpan.FromMilliseconds(1200));
        timings.Mark("build");
        time.Advance(TimeSpan.FromMilliseconds(100));
        timings.Mark("views");

        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            timings.ToString().Should().Be("build=1.2 views=0.1");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Phases_SenzaMark_EVuoto()
    {
        new StartupPhaseTimings(new FakeTimeProvider()).Phases.Should().BeEmpty();
    }
}
