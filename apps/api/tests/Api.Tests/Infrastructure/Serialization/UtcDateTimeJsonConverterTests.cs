using System.Globalization;
using System.Text.Json;
using Api.Infrastructure.Serialization;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Infrastructure.Serialization;

/// <summary>
/// #4519 — i convertitori che portano a UTC le date lette da un corpo JSON, e che in scrittura non
/// cambiano nulla.
/// </summary>
/// <remarks>
/// Tre prove diverse. La lettura si prova sul valore (<see cref="DateTimeKind"/>, offset, orologio). La
/// scrittura si prova per confronto con i convertitori di serie di System.Text.Json: stessi byte,
/// perché i convertitori stanno nelle opzioni che serializzano ogni risposta delle minimal API. E il
/// documento OpenAPI, generato dalle stesse opzioni, si prova per confronto con quello senza
/// convertitori.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Routing")]
public sealed class UtcDateTimeJsonConverterTests
{
    private static readonly JsonSerializerOptions WithConverters = CreateWithConverters();

    private static readonly JsonSerializerOptions BuiltIn = new();

    private static JsonSerializerOptions CreateWithConverters()
    {
        var options = new JsonSerializerOptions();
        UtcDateTimeJson.AddConverters(options);
        return options;
    }

    private sealed record Body(DateTime Date, DateTime? Until, string Note);

    private sealed record OffsetBody(DateTimeOffset At, DateTimeOffset? Maybe);

    private static string Iso(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static string Iso(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    // ── Il fuso dell'host ───────────────────────────────────────────────────────

    /// <summary>
    /// Il guard dello step di <c>dev-fast.yml</c> che rilancia queste classi con <c>TZ=Asia/Tokyo</c>
    /// (#4519). Alcuni rami dei convertitori e del filtro si vedono solo se il fuso dell'host non è UTC;
    /// se quel fuso non venisse applicato, lo step passerebbe senza distinguere nulla. Fuori dallo step
    /// la variabile non c'è e qui non c'è niente da controllare.
    /// </summary>
    [Fact]
    public void HostTimeZone_IsTheOneTheCiStepAsksFor()
    {
        var expected = Environment.GetEnvironmentVariable("EXPECTED_LOCAL_UTC_OFFSET");
        if (string.IsNullOrEmpty(expected))
        {
            return;
        }

        TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc))
            .Should().Be(TimeSpan.Parse(expected, CultureInfo.InvariantCulture),
                $"the step sets TZ to a non-UTC zone; the host reports «{TimeZoneInfo.Local.Id}»");
    }

    // ── DateTime: lettura ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-01-01", "2026-01-01T00:00:00.0000000Z")]
    [InlineData("2026-01-01T10:00:00", "2026-01-01T10:00:00.0000000Z")]
    [InlineData("2026-01-01T10:00:00.1234567", "2026-01-01T10:00:00.1234567Z")]
    [InlineData("2026-01-01T10:00:00Z", "2026-01-01T10:00:00.0000000Z")]
    [InlineData("2026-01-01T10:00:00+01:00", "2026-01-01T09:00:00.0000000Z")]
    [InlineData("2026-01-01T10:00:00-05:00", "2026-01-01T15:00:00.0000000Z")]
    public void Read_AnyForm_IsUtc(string written, string expected)
    {
        var value = JsonSerializer.Deserialize<DateTime>($"\"{written}\"", WithConverters);

        value.Kind.Should().Be(DateTimeKind.Utc);
        Iso(value).Should().Be(expected,
            "without an offset the written clock is read as UTC (#4063); with an offset it names an instant");
    }

    [Fact]
    public void Read_WithoutTheConverter_IsUnspecifiedOrLocal()
    {
        // Il comportamento di serie su cui poggia #4519: se cambiasse, il convertitore andrebbe riesaminato.
        JsonSerializer.Deserialize<DateTime>("\"2026-01-01\"", BuiltIn).Kind.Should().Be(DateTimeKind.Unspecified);
        JsonSerializer.Deserialize<DateTime>("\"2026-01-01T10:00:00+01:00\"", BuiltIn).Kind.Should().Be(DateTimeKind.Local);
    }

    [Fact]
    public void Read_PropertiesOfAPositionalRecord_AreUtc_IncludingNullable()
    {
        var body = JsonSerializer.Deserialize<Body>(
            """{"Date":"2026-01-01","Until":"2026-01-02T10:00:00+01:00","Note":"x"}""", WithConverters)!;

        body.Date.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        body.Date.Kind.Should().Be(DateTimeKind.Utc);
        body.Until.Should().Be(new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc),
            "System.Text.Json wraps the DateTime converter for DateTime? too");
        body.Until!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Read_NullableNull_StaysNull()
    {
        var body = JsonSerializer.Deserialize<Body>("""{"Date":"2026-01-01","Until":null,"Note":"x"}""", WithConverters)!;

        body.Until.Should().BeNull();
    }

    [Fact]
    public void Read_ElementsOfACollection_AreUtc()
    {
        var days = JsonSerializer.Deserialize<List<DateTime>>("""["2026-01-01","2026-01-02T10:00:00+01:00"]""", WithConverters)!;

        days.Should().OnlyContain(d => d.Kind == DateTimeKind.Utc);
        days.Should().Equal(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Read_DictionaryKey_IsUtc_AndWritesLikeTheBuiltIn()
    {
        var read = JsonSerializer.Deserialize<Dictionary<DateTime, int>>("""{"2026-01-01T10:00:00":1}""", WithConverters)!;

        read.Keys.Should().ContainSingle().Which.Should().Be(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        read.Keys.Single().Kind.Should().Be(DateTimeKind.Utc);

        var toWrite = new Dictionary<DateTime, int> { [new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified)] = 1 };
        JsonSerializer.Serialize(toWrite, WithConverters).Should().Be(JsonSerializer.Serialize(toWrite, BuiltIn));
    }

    /// <summary>
    /// Un valore con offset vicino ai limiti di <see cref="DateTime"/>. System.Text.Json lo legge come
    /// <see cref="DateTimeKind.Local"/> passando dall'ora del server, e lì il valore viene tagliato al
    /// limite: riportarlo a UTC sposterebbe l'istante in silenzio. L'istante esatto lo dà l'offset scritto.
    /// </summary>
    /// <remarks>
    /// Discrimina solo su un host con offset positivo (l'ora locale supera <see cref="DateTime.MaxValue"/>):
    /// in CI lo garantisce lo step che rilancia queste classi con <c>TZ=Asia/Tokyo</c>.
    /// </remarks>
    [Theory]
    [InlineData("9999-12-31T23:30:00+00:00", "9999-12-31T23:30:00.0000000Z")]
    [InlineData("9999-12-31T20:00:00-03:00", "9999-12-31T23:00:00.0000000Z")]
    public void Read_OffsetNearMaxValue_KeepsTheExactInstant(string written, string expected)
    {
        var value = JsonSerializer.Deserialize<DateTime>($"\"{written}\"", WithConverters);

        value.Kind.Should().Be(DateTimeKind.Utc);
        Iso(value).Should().Be(expected);
    }

    // ── DateTimeOffset: lettura ─────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-01-01", "2026-01-01T00:00:00.0000000+00:00")]
    [InlineData("2026-01-01T10:00:00", "2026-01-01T10:00:00.0000000+00:00")]
    [InlineData("2026-01-01T10:00:00.1234567", "2026-01-01T10:00:00.1234567+00:00")]
    [InlineData("2026-01-01T10:00:00Z", "2026-01-01T10:00:00.0000000+00:00")]
    [InlineData("2026-01-01T10:00:00+01:00", "2026-01-01T09:00:00.0000000+00:00")]
    [InlineData("2026-01-01T10:00:00-05:00", "2026-01-01T15:00:00.0000000+00:00")]
    public void ReadOffset_AnyForm_IsAtOffsetZero(string written, string expected)
    {
        var value = JsonSerializer.Deserialize<DateTimeOffset>($"\"{written}\"", WithConverters);

        Iso(value).Should().Be(expected,
            "without an offset the written clock is UTC, as the query binding reads it; with an offset it names " +
            "an instant, moved to offset zero because Npgsql rejects any other offset on timestamptz");
    }

    [Fact]
    public void ReadOffset_WithoutTheConverter_KeepsTheOffset_AndGivesTheServerOffsetToABareValue()
    {
        // Il comportamento di serie su cui poggia #4519: se cambiasse, il convertitore andrebbe riesaminato.
        JsonSerializer.Deserialize<DateTimeOffset>("\"2026-01-01T10:00:00+01:00\"", BuiltIn).Offset
            .Should().Be(TimeSpan.FromHours(1));
        JsonSerializer.Deserialize<DateTimeOffset>("\"2026-01-01T10:00:00\"", BuiltIn).Offset
            .Should().Be(TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified)),
                "a bare value gets the server's offset: the same body would name different instants on different hosts");
    }

    [Fact]
    public void ReadOffset_PropertiesOfAPositionalRecord_AreAtOffsetZero_IncludingNullable()
    {
        var body = JsonSerializer.Deserialize<OffsetBody>(
            """{"At":"2026-01-01T10:00:00+01:00","Maybe":"2026-01-01"}""", WithConverters)!;

        Iso(body.At).Should().Be("2026-01-01T09:00:00.0000000+00:00");
        Iso(body.Maybe!.Value).Should().Be("2026-01-01T00:00:00.0000000+00:00");

        JsonSerializer.Deserialize<OffsetBody>("""{"At":"2026-01-01","Maybe":null}""", WithConverters)!
            .Maybe.Should().BeNull();
    }

    [Theory]
    [InlineData("9999-12-31T23:30:00+00:00", "9999-12-31T23:30:00.0000000+00:00")]
    [InlineData("9999-12-31T20:00:00-03:00", "9999-12-31T23:00:00.0000000+00:00")]
    public void ReadOffset_OffsetNearMaxValue_KeepsTheExactInstant(string written, string expected)
    {
        Iso(JsonSerializer.Deserialize<DateTimeOffset>($"\"{written}\"", WithConverters)).Should().Be(expected);
    }

    // ── Gli errori restano quelli di serie ──────────────────────────────────────

    [Theory]
    [InlineData("\"notadate\"")]
    [InlineData("\"2026-13-01\"")]
    [InlineData("20260101")]
    [InlineData("true")]
    public void Read_NotADate_FailsLikeTheBuiltInConverter(string json)
    {
        Record.Exception(() => JsonSerializer.Deserialize<DateTime>(json, BuiltIn)).Should().BeOfType<JsonException>();
        Record.Exception(() => JsonSerializer.Deserialize<DateTime>(json, WithConverters)).Should().BeOfType<JsonException>(
            "a non-date must stay a JSON error (a 400 from body binding), not become a value or a 500");

        Record.Exception(() => JsonSerializer.Deserialize<DateTimeOffset>(json, BuiltIn)).Should().BeOfType<JsonException>();
        Record.Exception(() => JsonSerializer.Deserialize<DateTimeOffset>(json, WithConverters)).Should().BeOfType<JsonException>();
    }

    // ── Scrittura ───────────────────────────────────────────────────────────────

    public static TheoryData<DateTime> DateTimesToWrite() => new()
    {
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 1, 10, 30, 15, 123, DateTimeKind.Utc).AddTicks(4567),
        new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified),
        new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local),
        DateTime.MinValue,
        DateTime.MaxValue,
    };

    public static TheoryData<DateTimeOffset> DateTimeOffsetsToWrite() => new()
    {
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 1, 1, 10, 30, 15, 123, TimeSpan.FromHours(1)).AddTicks(4567),
        new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-5)),
        DateTimeOffset.MinValue,
        DateTimeOffset.MaxValue,
    };

    [Theory]
    [MemberData(nameof(DateTimesToWrite))]
    public void Write_DateTime_IsByteForByteTheBuiltInOutput(DateTime value)
    {
        JsonSerializer.Serialize(value, WithConverters).Should().Be(JsonSerializer.Serialize(value, BuiltIn),
            "the converter sits in the options that serialize every minimal API response: writing must not change");
    }

    [Theory]
    [MemberData(nameof(DateTimeOffsetsToWrite))]
    public void Write_DateTimeOffset_IsByteForByteTheBuiltInOutput(DateTimeOffset value)
    {
        JsonSerializer.Serialize(value, WithConverters).Should().Be(JsonSerializer.Serialize(value, BuiltIn),
            "a response keeps the offset it had: the converters only change how a request is read");
    }

    [Fact]
    public void Write_RecordsWithNullables_AreTheBuiltInOutput()
    {
        var body = new Body(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified), null, "x");
        var offsets = new OffsetBody(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(1)), null);

        JsonSerializer.Serialize(body, WithConverters).Should().Be(JsonSerializer.Serialize(body, BuiltIn));
        JsonSerializer.Serialize(offsets, WithConverters).Should().Be(JsonSerializer.Serialize(offsets, BuiltIn));
    }

    // ── Il documento OpenAPI ────────────────────────────────────────────────────

    private sealed record OpenApiProbe(
        DateTime Date,
        DateTime? Until,
        DateTimeOffset At,
        DateTimeOffset? Maybe,
        List<DateTime> Days,
        DateTime?[] MaybeDays,
        IReadOnlyList<DateTimeOffset> Moments,
        Dictionary<string, DateTime> ByName,
        string Note,
        DateTime? Later = null,
        DateTimeOffset? Sooner = null);

    /// <summary>
    /// Microsoft.AspNetCore.OpenApi genera gli schemi dalle stesse opzioni JSON delle minimal API, e per
    /// un tipo con un convertitore custom non conosce la forma: senza
    /// <see cref="UtcDateTimeJson.ConfigureOpenApi"/> lo schema di una data perde <c>"type"</c>. Il
    /// documento deve restare identico a quello senza convertitori.
    /// </summary>
    [Fact]
    public async Task OpenApiDocument_WithConvertersAndTheSchemaFix_IsIdenticalToTheBuiltIn()
    {
        var builtIn = await OpenApiDocumentAsync(withConverters: false, withSchemaFix: false);
        var fixedUp = await OpenApiDocumentAsync(withConverters: true, withSchemaFix: true);

        builtIn.Should().Contain("\"format\": \"date-time\"", "guard: the probe must produce date-time schemas at all");
        builtIn.Should().Contain("\"name\": \"days\"", "guard: the query parameters must be in the document");
        builtIn.Should().Contain("\"name\": \"day\"", "guard: the route parameter must be in the document");
        (fixedUp == builtIn).Should().BeTrue(
            "the converters must not change the OpenAPI document. Built-in:\n" + builtIn + "\nWith converters:\n" + fixedUp);
    }

    /// <summary>
    /// La riparazione è inerte senza i convertitori: il documento resta quello di serie. È ciò che permette
    /// al test d'integrazione <c>OpenApiDocument_IsTheSameWithoutTheConverters</c> di confrontare il
    /// documento vero dell'API con quello di serie, e non con la riparazione applicata a schemi intatti.
    /// </summary>
    [Fact]
    public async Task OpenApiDocument_SchemaFixWithoutConverters_ChangesNothing()
    {
        var builtIn = await OpenApiDocumentAsync(withConverters: false, withSchemaFix: false);
        var fixOnly = await OpenApiDocumentAsync(withConverters: false, withSchemaFix: true);

        (fixOnly == builtIn).Should().BeTrue("without the converters the schema fix must not touch anything");
    }

    /// <summary>
    /// Il controllo opposto: senza la riparazione lo schema cambia davvero. Se un giorno il generatore
    /// riconoscesse da solo i tipi con convertitore, questo test lo direbbe e la riparazione si potrebbe
    /// togliere.
    /// </summary>
    [Fact]
    public async Task OpenApiDocument_WithConvertersButNoSchemaFix_LosesTheDateType()
    {
        var builtIn = await OpenApiDocumentAsync(withConverters: false, withSchemaFix: false);
        var broken = await OpenApiDocumentAsync(withConverters: true, withSchemaFix: false);

        (broken == builtIn).Should().BeFalse("without the schema fix the date schemas lose their \"type\"");
    }

    private static async Task<string> OpenApiDocumentAsync(bool withConverters, bool withSchemaFix)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        // Dal method group e non con una chiamata diretta: il generatore di sorgenti di
        // Microsoft.AspNetCore.OpenApi intercetta le chiamate dirette ad AddOpenApi, e nel progetto
        // di test gli intercettori non sono abilitati.
        Func<IServiceCollection, Action<OpenApiOptions>, IServiceCollection> addOpenApi =
            OpenApiServiceCollectionExtensions.AddOpenApi;
        addOpenApi(builder.Services, options =>
        {
            if (withSchemaFix)
            {
                UtcDateTimeJson.ConfigureOpenApi(options);
            }
        });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            if (withConverters)
            {
                UtcDateTimeJson.AddConverters(options.SerializerOptions);
            }
        });

        await using var app = builder.Build();
        app.MapOpenApi();
        app.MapPost("/probe", (OpenApiProbe body) => Results.Ok(body));
        app.MapGet("/probe/{id}", (int id) => Results.Ok(new OpenApiProbe(DateTime.UtcNow, null, DateTimeOffset.UtcNow, null, [], [], [], [], "x")))
            .Produces<OpenApiProbe>();

        // I parametri data in query e in route: la forma più frequente nell'API. Anche i loro schemi
        // vengono dalle opzioni JSON.
        app.MapGet("/probe/query", (DateTime? from, DateTime to, DateTimeOffset? at, DateTime[]? days) => Results.Ok());
        app.MapGet("/probe/day/{day}", (DateTime day, DateTimeOffset since) => Results.Ok());
        await app.StartAsync();

        using var client = app.GetTestClient();
        return await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));
    }
}
