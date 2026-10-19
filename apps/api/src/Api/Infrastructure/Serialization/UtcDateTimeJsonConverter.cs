using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Filters;

namespace Api.Infrastructure.Serialization;

/// <summary>
/// #4519 — un <see cref="DateTime"/> letto da un corpo JSON arriva all'handler in UTC. In scrittura non
/// cambia nulla.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il difetto.</b> System.Text.Json legge <c>"2026-01-01"</c> e <c>"2026-01-01T10:00:00"</c> come
/// <see cref="DateTimeKind.Unspecified"/>, e <c>"2026-01-01T10:00:00+01:00"</c> come
/// <see cref="DateTimeKind.Local"/> (convertito all'ora del server). Npgsql rifiuta entrambi su una
/// colonna <c>timestamp with time zone</c>: <c>SaveChanges</c> lancia una <c>DbUpdateException</c> e la
/// richiesta risponde 500. Misurato su <c>POST /api/v1/admin/financial-ledger</c>.
/// </para>
/// <para>
/// <b>La regola</b> è quella di #4063 per la query, <see cref="UtcDateTimeArguments.AsUtc(DateTime)"/>:
/// senza designatore di fuso l'orologio scritto resta quello e il fuso è UTC; con un offset il valore
/// nomina già un istante, e viene convertito senza spostarlo.
/// </para>
/// <para>
/// <b>La scrittura delega al formato di serie</b> (<see cref="Utf8JsonWriter.WriteStringValue(DateTime)"/>,
/// lo stesso che usa il convertitore di System.Text.Json): le risposte restano identiche byte per
/// byte, compreso un valore <c>Unspecified</c> che resta senza designatore. Lo fissano
/// <c>UtcDateTimeJsonConverterTests</c> e un test sulla forma di una risposta vera.
/// </para>
/// <para>
/// <b>Dove è registrato.</b> Solo nelle opzioni JSON delle minimal API (<c>ConfigureHttpJsonOptions</c>
/// in <c>Program.cs</c>): binding dei corpi, <c>Request.ReadFromJsonAsync</c>, <c>Results.*</c>,
/// <c>Response.WriteAsJsonAsync</c>. Non lo vedono SignalR (ha le proprie opzioni), l'outbox
/// (<c>DomainEventJsonOptions</c>), gli eventi SSE (<c>SseJsonOptions</c>) né i client HTTP verso
/// servizi esterni.
/// </para>
/// <para>
/// <b>Un orario locale voluto</b> (un orologio da interpretare con un fuso dichiarato a parte) non deve
/// passare da qui: una proprietà con un proprio <see cref="JsonConverterAttribute"/> vince sulle
/// opzioni. Oggi nessun corpo ne ha uno; il test d'inventario
/// <c>BodyDateWithoutOffsetIntegrationTests</c> elenca ogni data dei corpi e fallisce se una sfugge.
/// </para>
/// </remarks>
internal sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UtcDateTimeArguments.AsUtc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);

    /// <summary>
    /// Una data come chiave di un dizionario: la legge il convertitore di serie (è ciò che fa la base),
    /// poi vale la stessa regola. La scrittura della chiave resta quella di serie.
    /// </summary>
    public override DateTime ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        UtcDateTimeArguments.AsUtc(base.ReadAsPropertyName(ref reader, typeToConvert, options));
}
