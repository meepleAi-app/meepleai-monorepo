using System.Globalization;

namespace Api.SharedKernel.Utilities;

/// <summary>
/// La settimana ISO 8601 di una data, come chiave dei contatori settimanali: <c>yyyy-Www</c>
/// (es. <c>2026-W53</c>). Unica fonte per le chiavi settimanali su Redis (#4541).
/// </summary>
/// <remarks>
/// <para>Le settimane ISO vanno da lunedi' a domenica, e una settimana appartiene all'anno del suo
/// giovedi': da lunedi' 28/12/2026 a domenica 3/1/2027 e' tutto <c>2026-W53</c>, e lunedi'
/// 30/12/2024 e' gia' <c>2025-W01</c>. Quindi la chiave cambia solo di lunedi', insieme al reset
/// settimanale delle quote (lunedi' alle 00:00 UTC).</para>
/// <para>Non usare <see cref="Calendar.GetWeekOfYear"/> con <see cref="CalendarWeekRule.FirstFourDayWeek"/>:
/// non e' ISO 8601 (non da' mai la settimana 1 a un giorno di fine dicembre), e unito all'anno di
/// calendario cambia chiave a meta' settimana. Le correzioni a mano su quel valore sbagliavano in
/// altri giorni (#4541).</para>
/// <para>Conta solo la data: il chiamante passa l'istante gia' in UTC.</para>
/// </remarks>
internal static class IsoWeekKey
{
    public static string For(DateTime date) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{ISOWeek.GetYear(date)}-W{ISOWeek.GetWeekOfYear(date):D2}");
}
