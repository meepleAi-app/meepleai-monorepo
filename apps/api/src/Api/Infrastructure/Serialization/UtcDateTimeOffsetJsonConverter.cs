using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Filters;

namespace Api.Infrastructure.Serialization;

/// <summary>
/// #4519 — un <see cref="DateTimeOffset"/> letto da un corpo JSON arriva all'handler a offset zero. In
/// scrittura non cambia nulla.
/// </summary>
/// <remarks>
/// <para>
/// Il gemello di <see cref="UtcDateTimeJsonConverter"/>, per due difetti di System.Text.Json:
/// conserva un offset diverso da zero (<c>+01:00</c>), che Npgsql rifiuta su una colonna
/// <c>timestamp with time zone</c>; e a un valore <b>senza</b> offset applica l'offset del
/// <b>server</b>, quindi lo stesso corpo nomina istanti diversi a seconda di dove gira l'API. Il binding
/// della query fa invece offset zero con lo stesso orologio (verificato in
/// <c>UtcDateTimeArgumentsTests</c>): qui vale la stessa regola.
/// </para>
/// <para>
/// Il <see cref="DateTimeKind"/> del <see cref="DateTime"/> letto dallo stesso token distingue le due
/// forme: <see cref="DateTimeKind.Unspecified"/> vuol dire nessun designatore di fuso.
/// </para>
/// <para>
/// Le chiavi di dizionario <see cref="DateTimeOffset"/> restano al convertitore di serie: nessun corpo
/// ne ha, e il test d'inventario le segnalerebbe.
/// </para>
/// </remarks>
internal sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TryGetDateTime(out var written) && written.Kind == DateTimeKind.Unspecified)
        {
            return new DateTimeOffset(written.Ticks, TimeSpan.Zero);
        }

        return UtcDateTimeArguments.AsUtc(reader.GetDateTimeOffset());
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
