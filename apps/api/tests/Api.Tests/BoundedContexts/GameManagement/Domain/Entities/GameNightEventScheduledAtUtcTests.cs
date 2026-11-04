using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// #4055. <c>ScheduledAt</c> entra nell'aggregato con l'offset scelto dal client — il contratto
/// pubblico e' <c>DateTimeOffset</c> su DTO di routing e command — ma la colonna e'
/// <c>timestamptz</c> e Npgsql rifiuta qualunque offset diverso da zero. Prima del fix,
/// <c>POST/PUT /api/v1/game-nights</c> con <c>+01:00</c> o <c>-05:00</c> rispondeva 500 da
/// <c>SaveChangesAsync</c>, cioe' DOPO i validator (misurato sullo stack locale il 2026-10-04:
/// <c>Z</c> e <c>+00:00</c> → 201, <c>+01:00</c> e <c>-05:00</c> → 500).
/// </summary>
/// <remarks>
/// <para>
/// Questi test non toccano Npgsql: fissano l'invariante che il fix introduce — lo stato
/// dell'aggregato e' un istante in UTC — che e' la condizione sotto cui la scrittura e' lecita.
/// Falliscono sulla versione non corretta sull'asserzione <c>Offset == TimeSpan.Zero</c>.
/// La riproduzione del 500 vero, contro un PostgreSQL reale, sta in
/// <c>Integration/GameManagement/GameNightScheduledAtTimezoneTests</c>.
/// </para>
/// <para>
/// L'asserzione sull'istante e' il controllo di non-regressione: passa anche senza il fix, e serve
/// a provare che la normalizzazione non sposta il momento scelto dall'organizzatore.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4055")]
public sealed class GameNightEventScheduledAtUtcTests
{
    private const string Title = "Serata di prova";

    /// <summary>
    /// Gli stessi casi della issue #4055, piu' un offset a mezz'ora (India) perche' il fix non
    /// deve assumere offset interi, e uno che cambia giorno (<c>-05:00</c> alle 14 locali e'
    /// 19:00Z dello stesso giorno; <c>-05:00</c> alle 22 locali e' 03:00Z del giorno dopo).
    /// </summary>
    public static TheoryData<int, int, int, int, string> NonUtcOffsets => new()
    {
        // offsetHours, offsetMinutes, localHour, localMinute, istante UTC atteso
        { 1, 0, 20, 0, "2026-12-20T19:00:00Z" },
        { 2, 0, 21, 0, "2026-12-20T19:00:00Z" },
        { -5, 0, 14, 0, "2026-12-20T19:00:00Z" },
        { -5, 0, 22, 0, "2026-12-21T03:00:00Z" },
        { 5, 30, 0, 30, "2026-12-19T19:00:00Z" },
    };

    private static DateTimeOffset Local(int offsetHours, int offsetMinutes, int hour, int minute) =>
        new(2026, 12, 20, hour, minute, 0, new TimeSpan(offsetHours, offsetMinutes, 0));

    [Theory]
    [MemberData(nameof(NonUtcOffsets))]
    public void Create_WithNonUtcOffset_StoresTheSameInstantInUtc(
        int offsetHours, int offsetMinutes, int localHour, int localMinute, string expectedUtc)
    {
        var scheduledAt = Local(offsetHours, offsetMinutes, localHour, localMinute);
        var expected = DateTimeOffset.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture);

        var night = GameNightEvent.Create(Guid.NewGuid(), Title, scheduledAt);

        // Il fix: nessun offset residuo puo' raggiungere la colonna timestamptz.
        night.ScheduledAt.Offset.Should().Be(TimeSpan.Zero);
        // Non-regressione: l'istante scelto non si muove.
        night.ScheduledAt.Should().Be(expected);
        night.ScheduledAt.UtcDateTime.Should().Be(expected.UtcDateTime);
    }

    [Theory]
    [MemberData(nameof(NonUtcOffsets))]
    public void Update_WithNonUtcOffset_StoresTheSameInstantInUtc(
        int offsetHours, int offsetMinutes, int localHour, int localMinute, string expectedUtc)
    {
        var night = GameNightEvent.Create(
            Guid.NewGuid(), Title, new DateTimeOffset(2026, 11, 1, 18, 0, 0, TimeSpan.Zero));
        var scheduledAt = Local(offsetHours, offsetMinutes, localHour, localMinute);
        var expected = DateTimeOffset.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture);

        night.Update(Title, description: null, scheduledAt, location: null, maxPlayers: null, gameIds: null);

        night.ScheduledAt.Offset.Should().Be(TimeSpan.Zero);
        night.ScheduledAt.Should().Be(expected);
        night.ScheduledAt.UtcDateTime.Should().Be(expected.UtcDateTime);
    }

    [Fact]
    public void Create_WithUtcInput_IsUnchanged()
    {
        var scheduledAt = new DateTimeOffset(2026, 12, 20, 19, 0, 0, TimeSpan.Zero);

        var night = GameNightEvent.Create(Guid.NewGuid(), Title, scheduledAt);

        night.ScheduledAt.Should().Be(scheduledAt);
        night.ScheduledAt.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Update_WithUtcInput_IsUnchanged()
    {
        var night = GameNightEvent.Create(
            Guid.NewGuid(), Title, new DateTimeOffset(2026, 11, 1, 18, 0, 0, TimeSpan.Zero));
        var scheduledAt = new DateTimeOffset(2026, 12, 20, 19, 0, 0, TimeSpan.Zero);

        night.Update(Title, description: null, scheduledAt, location: null, maxPlayers: null, gameIds: null);

        night.ScheduledAt.Should().Be(scheduledAt);
        night.ScheduledAt.Offset.Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// <c>CreateAdHoc</c> passa <c>DateTimeOffset.UtcNow</c>, quindi era gia' lecito: il test lo
    /// fissa perche' il fix vive nel costruttore che anche questa factory attraversa.
    /// </summary>
    [Fact]
    public void CreateAdHoc_StoresUtcInstant()
    {
        var night = GameNightEvent.CreateAdHoc(Guid.NewGuid(), Title, Guid.NewGuid());

        night.ScheduledAt.Offset.Should().Be(TimeSpan.Zero);
    }
}
