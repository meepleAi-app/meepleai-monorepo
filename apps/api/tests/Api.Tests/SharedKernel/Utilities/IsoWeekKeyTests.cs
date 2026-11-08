using Api.SharedKernel.Utilities;
using Api.Tests.Constants;
using FluentAssertions;
using System.Globalization;
using Xunit;

namespace Api.Tests.SharedKernel.Utilities;

/// <summary>
/// #4541: la chiave della settimana ISO 8601 usata dai contatori settimanali su Redis.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class IsoWeekKeyTests
{
    // Valori scritti a mano e verificati con System.Globalization.ISOWeek.
    [Theory]
    [InlineData("2026-12-27", "2026-W52")] // domenica
    [InlineData("2026-12-28", "2026-W53")] // lunedi': il 2026 comincia di giovedi' e ha 53 settimane
    [InlineData("2026-12-29", "2026-W53")]
    [InlineData("2026-12-30", "2026-W53")]
    [InlineData("2026-12-31", "2026-W53")] // giovedi': decide l'anno della settimana
    [InlineData("2027-01-01", "2026-W53")]
    [InlineData("2027-01-02", "2026-W53")]
    [InlineData("2027-01-03", "2026-W53")] // domenica
    [InlineData("2027-01-04", "2027-W01")] // lunedi'
    [InlineData("2024-12-29", "2024-W52")]
    [InlineData("2024-12-30", "2025-W01")] // il giovedi' e' il 2/1/2025
    [InlineData("2024-12-31", "2025-W01")]
    [InlineData("2025-12-29", "2026-W01")]
    [InlineData("2025-12-30", "2026-W01")]
    [InlineData("2025-12-31", "2026-W01")]
    [InlineData("2029-12-31", "2030-W01")]
    [InlineData("2021-12-29", "2021-W52")] // il 2021 ha 52 settimane
    [InlineData("2022-01-01", "2021-W52")]
    [InlineData("2022-01-03", "2022-W01")]
    [InlineData("2020-12-29", "2020-W53")]
    [InlineData("2021-01-03", "2020-W53")]
    [InlineData("2025-01-06", "2025-W02")] // due cifre anche sotto 10
    [InlineData("2025-11-22", "2025-W47")]
    public void For_ReturnsTheIsoWeek(string day, string expected)
    {
        var date = DateTime.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        IsoWeekKey.For(date).Should().Be(expected);
        IsoWeekKey.For(date.AddHours(23).AddMinutes(59).AddSeconds(59)).Should().Be(expected, "conta il giorno, non l'ora");
    }

    // La proprieta' che serve alle quote, verificata senza ISOWeek: la chiave cambia di lunedi' e
    // solo di lunedi', come il reset settimanale (lunedi' alle 00:00 UTC). Il vecchio calcolo la
    // cambiava anche di martedi', giovedi' o venerdi' a cavallo d'anno.
    [Fact]
    public void For_ChangesOnMondayAndOnlyOnMonday_FromYear2000To2100()
    {
        var previous = IsoWeekKey.For(new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        var wrong = new List<string>();

        for (var day = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc); day.Year <= 2100; day = day.AddDays(1))
        {
            var key = IsoWeekKey.For(day);
            var changed = key != previous;
            if (changed != (day.DayOfWeek == DayOfWeek.Monday))
                wrong.Add($"{day:yyyy-MM-dd} {day.DayOfWeek}: {previous} -> {key}");
            previous = key;
        }

        wrong.Should().BeEmpty();
    }

    // Seconda verifica senza ISOWeek, dalla definizione: la settimana appartiene all'anno del suo
    // giovedi', e il numero e' la posizione di quel giovedi' nell'anno.
    [Fact]
    public void For_MatchesTheThursdayRule_FromYear2000To2100()
    {
        var wrong = new List<string>();

        for (var day = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc); day.Year <= 2100; day = day.AddDays(1))
        {
            var daysFromMonday = ((int)day.DayOfWeek + 6) % 7;
            var thursday = day.AddDays(3 - daysFromMonday);
            var expected = string.Create(
                CultureInfo.InvariantCulture, $"{thursday.Year}-W{(thursday.DayOfYear - 1) / 7 + 1:D2}");

            var key = IsoWeekKey.For(day);
            if (key != expected)
                wrong.Add($"{day:yyyy-MM-dd}: {key}, atteso {expected}");
        }

        wrong.Should().BeEmpty();
    }
}
