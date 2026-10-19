using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Infrastructure.Serialization;

/// <summary>
/// #4519 — il punto unico che porta a UTC le date dei corpi JSON: registra i due convertitori nelle
/// opzioni JSON delle minimal API, e ripara lo schema OpenAPI che i convertitori altrimenti cambiano.
/// </summary>
internal static class UtcDateTimeJson
{
    /// <summary>
    /// Aggiunge <see cref="UtcDateTimeJsonConverter"/> e <see cref="UtcDateTimeOffsetJsonConverter"/>.
    /// Valgono anche per <c>DateTime?</c> e <c>DateTimeOffset?</c>: System.Text.Json avvolge il
    /// convertitore del tipo valore.
    /// </summary>
    public static void AddConverters(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Converters.Add(new UtcDateTimeJsonConverter());
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
    }

    /// <summary>
    /// Ripristina gli schemi OpenAPI delle date: <c>"type": "string"</c> (con <c>"null"</c> per i
    /// nullable) e, per una collezione o un dizionario di date, lo schema degli elementi.
    /// </summary>
    /// <remarks>
    /// Microsoft.AspNetCore.OpenApi genera gli schemi dalle stesse opzioni JSON, e per un tipo con un
    /// convertitore custom non sa quale forma abbia. Misurato confrontando il documento con e senza i
    /// convertitori: lo schema di un <see cref="DateTime"/> perdeva <c>"type"</c>, e anche
    /// <c>"format": "date-time"</c> quando il parametro del record ha un default (<c>= null</c>);
    /// quello di una <c>List&lt;DateTime&gt;</c> perdeva <c>"items"</c> intero, che non passa
    /// nemmeno da un trasformatore e va quindi rifatto dal genitore. Un parametro nullable in query
    /// resta <c>"string"</c> senza <c>"null"</c>, come nello schema di serie. Senza i convertitori nelle
    /// opzioni la riparazione non fa nulla.
    /// I test che lo fissano sono <c>UtcDateTimeJsonConverterTests</c> (un documento di prova) e
    /// <c>BodyDateWithoutOffsetIntegrationTests</c> (il documento vero dell'API). Il documento lo
    /// leggono Scalar e lo script degli schemi zod del frontend (<c>pnpm generate:api</c>).
    /// </remarks>
    public static void ConfigureOpenApi(OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddSchemaTransformer((schema, context, _) =>
        {
            var info = context.JsonTypeInfo;

            // Ripara solo ciò che i convertitori rompono: senza di loro lo schema di serie resta
            // intatto, ed è ciò che permette ai test di confrontare con il documento vero.
            if (!UsesUtcConverters(info.Options))
            {
                return Task.CompletedTask;
            }

            if (DateSchemaType(info.Type) is { } dateType)
            {
                // Un parametro nullable in query, route o header resta "string": lo schema di serie mette
                // l'opzionalità nel parametro, non nel tipo. Una proprietà nullable (anche del corpo, che
                // per ApiExplorer è a sua volta un parametro) ha ["null","string"].
                var isNonBodyParameter = context.JsonPropertyInfo is null
                    && context.ParameterDescription is { } parameter
                    && parameter.Source != BindingSource.Body;
                schema.Type = isNonBodyParameter ? dateType & ~JsonSchemaType.Null : dateType;
                schema.Format ??= DateTimeFormat;
            }
            else if (info.ElementType is { } element && DateSchemaType(element) is { } elementType)
            {
                if (info.Kind == JsonTypeInfoKind.Enumerable && schema.Items is null)
                {
                    schema.Items = DateSchema(elementType);
                }
                else if (info.Kind == JsonTypeInfoKind.Dictionary && schema.AdditionalProperties is null)
                {
                    schema.AdditionalProperties = DateSchema(elementType);
                }
            }

            return Task.CompletedTask;
        });
    }

    private const string DateTimeFormat = "date-time";

    private static bool UsesUtcConverters(JsonSerializerOptions options) =>
        options.Converters.Any(c => c is UtcDateTimeJsonConverter or UtcDateTimeOffsetJsonConverter);

    private static OpenApiSchema DateSchema(JsonSchemaType type) => new() { Type = type, Format = DateTimeFormat };

    /// <summary>Il <c>"type"</c> che lo schema di una data avrebbe senza convertitore, o <c>null</c>.</summary>
    private static JsonSchemaType? DateSchemaType(Type type)
    {
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return JsonSchemaType.String;
        }

        if (type == typeof(DateTime?) || type == typeof(DateTimeOffset?))
        {
            return JsonSchemaType.String | JsonSchemaType.Null;
        }

        return null;
    }
}
