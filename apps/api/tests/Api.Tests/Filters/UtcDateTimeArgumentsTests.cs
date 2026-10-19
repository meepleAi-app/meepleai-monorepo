using System.Net;
using System.Reflection;
using Api.Filters;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Filters;

/// <summary>
/// #4063 — il punto unico che legge come UTC una data senza designatore di fuso arrivata come
/// argomento di un endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Tre livelli. La regola su un singolo valore (<see cref="UtcDateTimeArguments.AsUtc"/>); l'inventario
/// che dice quali argomenti di un handler sono <c>DateTime</c> e se il filtro sa normalizzarli; e il
/// percorso vero, su un <see cref="TestServer"/> in memoria: binding di ASP.NET → filtro → handler.
/// </para>
/// <para>
/// L'ultimo livello porta il proprio controllo (<see cref="WithoutTheConvention_BindingYieldsUnspecified"/>):
/// la correzione poggia su un comportamento del framework — il binding di <c>DateTime</c> usa
/// <c>AdjustToUniversal</c>, che senza designatore lascia <see cref="DateTimeKind.Unspecified"/>. Se il
/// framework cambiasse, quel test lo direbbe, invece di lasciare verdi gli altri per la ragione sbagliata.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Routing")]
public sealed class UtcDateTimeArgumentsTests
{
    // ── La regola su un valore ──────────────────────────────────────────────────

    [Fact]
    public void AsUtc_Unspecified_KeepsTheWallClockAndMarksItUtc()
    {
        var value = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);

        var result = UtcDateTimeArguments.AsUtc(value);

        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Ticks.Should().Be(value.Ticks, "a date without offset names that instant in UTC: no shift");
    }

    [Fact]
    public void AsUtc_Utc_IsUnchanged()
    {
        var value = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var result = UtcDateTimeArguments.AsUtc(value);

        result.Should().Be(value);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void AsUtc_Local_KeepsTheInstant()
    {
        var instant = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var local = instant.ToLocalTime();

        var result = UtcDateTimeArguments.AsUtc(local);

        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(instant, "a Local value already names an instant: converting must not move it");
    }

    // #4519 — un DateTimeOffset con un offset diverso da zero nomina un istante: va portato a offset
    // zero senza spostarlo, perché Npgsql rifiuta ogni altro offset su timestamptz.
    [Fact]
    public void AsUtc_DateTimeOffsetWithOffset_KeepsTheInstantAtOffsetZero()
    {
        var value = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(1));

        var result = UtcDateTimeArguments.AsUtc(value);

        result.Offset.Should().Be(TimeSpan.Zero);
        result.UtcDateTime.Should().Be(new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc));
        result.Should().Be(value, "DateTimeOffset equality compares instants: converting must not move it");
    }

    [Fact]
    public void AsUtc_DateTimeOffsetAtOffsetZero_IsUnchanged()
    {
        var value = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

        UtcDateTimeArguments.AsUtc(value).Should().Be(value);
        UtcDateTimeArguments.AsUtc(value).Offset.Should().Be(TimeSpan.Zero);
    }

    // ── L'inventario degli argomenti ────────────────────────────────────────────

    private sealed record WindowRecord(DateTime? From = null, DateTime? To = null, int Page = 1);

    // Un [AsParameters] di tipo valore: il getter del contesto ne restituisce una copia inscatolata,
    // quindi la normalizzazione arriva all'handler solo se il filtro riscrive l'argomento.
    private readonly record struct WindowStruct(DateTime? From = null);

    private sealed record OffsetWindow(DateTimeOffset? From = null, int Page = 1);

    private sealed class GetOnlyWindow
    {
        public GetOnlyWindow(DateTime? from) => From = from;

        public DateTime? From { get; }
    }

#pragma warning disable IDE0060, S1172 // i parametri esistono per la firma, non per l'uso
    private static void Handler(int page, DateTime? since, DateTime until, string? search) { }

    private static void HandlerWithSurrogate([AsParameters] WindowRecord window) { }

    private static void HandlerWithGetOnlySurrogate([AsParameters] GetOnlyWindow window) { }

    private static void HandlerWithArray(DateTime[] days) { }

    private static void HandlerWithOffsets(DateTimeOffset? from, DateTimeOffset to) { }

    private static void HandlerWithOffsetSurrogate([AsParameters] OffsetWindow window) { }

    private static void HandlerWithOffsetArray(DateTimeOffset[] moments) { }

    private static void HandlerWithoutDates(int page, string? search) { }
#pragma warning restore IDE0060, S1172

    private static MethodInfo Method(string name) =>
        typeof(UtcDateTimeArgumentsTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void FindSites_TopLevelDateTimeAndNullable_AreFoundAndNormalized()
    {
        var sites = UtcDateTimeArguments.FindSites(Method(nameof(Handler)));

        sites.Should().BeEquivalentTo(new[]
        {
            new DateTimeArgumentSite("since", null, IsNormalized: true),
            new DateTimeArgumentSite("until", null, IsNormalized: true),
        });
    }

    [Fact]
    public void FindSites_AsParametersRecord_FindsItsDateTimeProperties()
    {
        var sites = UtcDateTimeArguments.FindSites(Method(nameof(HandlerWithSurrogate)));

        sites.Should().BeEquivalentTo(new[]
        {
            new DateTimeArgumentSite("window", "From", IsNormalized: true),
            new DateTimeArgumentSite("window", "To", IsNormalized: true),
        });
    }

    [Fact]
    public void FindSites_GetOnlyProperty_IsReportedAsNotNormalized()
    {
        var sites = UtcDateTimeArguments.FindSites(Method(nameof(HandlerWithGetOnlySurrogate)));

        sites.Should().ContainSingle()
            .Which.Should().Be(new DateTimeArgumentSite("window", "From", IsNormalized: false),
                "the filter cannot write a get-only property: the inventory test must see it, not skip it");
    }

    [Fact]
    public void FindSites_DateTimeArray_IsReportedAsNotNormalized()
    {
        var sites = UtcDateTimeArguments.FindSites(Method(nameof(HandlerWithArray)));

        sites.Should().ContainSingle().Which.Should().Be(new DateTimeArgumentSite("days", null, IsNormalized: false));
    }

    [Fact]
    public void FindSites_DateTimeOffsetArgumentsAndProperties_AreFoundAndNormalized()
    {
        UtcDateTimeArguments.FindSites(Method(nameof(HandlerWithOffsets))).Should().BeEquivalentTo(new[]
        {
            new DateTimeArgumentSite("from", null, IsNormalized: true),
            new DateTimeArgumentSite("to", null, IsNormalized: true),
        });

        UtcDateTimeArguments.FindSites(Method(nameof(HandlerWithOffsetSurrogate))).Should().BeEquivalentTo(new[]
        {
            new DateTimeArgumentSite("window", "From", IsNormalized: true),
        });
    }

    [Fact]
    public void FindSites_DateTimeOffsetArray_IsReportedAsNotNormalized()
    {
        var sites = UtcDateTimeArguments.FindSites(Method(nameof(HandlerWithOffsetArray)));

        sites.Should().ContainSingle().Which.Should().Be(new DateTimeArgumentSite("moments", null, IsNormalized: false));
    }

    [Fact]
    public void CreateFilter_HandlerWithoutDates_ReturnsNextUnchanged()
    {
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(null);
        var context = new EndpointFilterFactoryContext
        {
            MethodInfo = Method(nameof(HandlerWithoutDates)),
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
        };

        var result = UtcDateTimeArguments.CreateFilter(context, next);

        // Restituire `next` stesso è ciò che permette a RequestDelegateFactory di saltare la pipeline
        // dei filtri: sugli endpoint senza date, il filtro di gruppo non costa nulla per richiesta.
        result.Should().BeSameAs(next);
    }

    // ── Il percorso vero: binding → filtro → handler ────────────────────────────

    private static async Task<WebApplication> StartAppAsync(bool withConvention)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        var group = app.MapGroup("/probe");
        if (withConvention)
        {
            group.WithUtcDateTimeArguments();
        }

        group.MapGet("/nullable", (DateTime? at) => Describe(at));
        group.MapGet("/required", ([FromQuery] DateTime at) => Describe(at));
        group.MapGet("/surrogate", ([AsParameters] WindowRecord window) => Describe(window.From));
        group.MapGet("/struct-surrogate", ([AsParameters] WindowStruct window) => Describe(window.From));
        group.MapGet("/offset", (DateTimeOffset? at) => DescribeOffset(at));
        group.MapGet("/offset-surrogate", ([AsParameters] OffsetWindow window) => DescribeOffset(window.From));
        group.MapGet("/plain", (int page) => page.ToString(System.Globalization.CultureInfo.InvariantCulture));

        await app.StartAsync();
        return app;
    }

    private static string Describe(DateTime? value) =>
        value is null ? "null" : $"{value.Value.Kind}|{value.Value:O}";

    private static string DescribeOffset(DateTimeOffset? value) =>
        value is null ? "null" : value.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<(HttpStatusCode Status, string Body)> GetAsync(WebApplication app, string pathAndQuery)
    {
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(new Uri(pathAndQuery, UriKind.Relative));
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/probe/nullable?at=2026-01-01", "Utc|2026-01-01T00:00:00.0000000Z")]
    [InlineData("/probe/nullable?at=2026-01-01T10:00:00", "Utc|2026-01-01T10:00:00.0000000Z")]
    [InlineData("/probe/required?at=2026-01-01T10:00:00", "Utc|2026-01-01T10:00:00.0000000Z")]
    [InlineData("/probe/surrogate?from=2026-01-01T10:00:00", "Utc|2026-01-01T10:00:00.0000000Z")]
    [InlineData("/probe/struct-surrogate?from=2026-01-01T10:00:00", "Utc|2026-01-01T10:00:00.0000000Z")]
    public async Task WithTheConvention_DateWithoutOffset_ReachesTheHandlerAsUtc(string pathAndQuery, string expected)
    {
        await using var app = await StartAppAsync(withConvention: true);

        var (status, body) = await GetAsync(app, pathAndQuery);

        status.Should().Be(HttpStatusCode.OK);
        body.Should().Be(expected);
    }

    [Theory]
    [InlineData("/probe/nullable?at=2026-01-01T10:00:00Z", "Utc|2026-01-01T10:00:00.0000000Z")]
    [InlineData("/probe/nullable?at=2026-01-01T10:00:00%2B01:00", "Utc|2026-01-01T09:00:00.0000000Z")]
    [InlineData("/probe/nullable", "null")]
    public async Task WithTheConvention_OtherFormsAreUnchanged(string pathAndQuery, string expected)
    {
        await using var app = await StartAppAsync(withConvention: true);

        var (status, body) = await GetAsync(app, pathAndQuery);

        status.Should().Be(HttpStatusCode.OK);
        body.Should().Be(expected, "a value with an offset already bound as UTC, and an absent value stays absent");
    }

    [Theory]
    [InlineData("/probe/nullable?at=notadate")]
    [InlineData("/probe/required?at=notadate")]
    [InlineData("/probe/surrogate?from=notadate")]
    public async Task WithTheConvention_NotADate_IsStillRejectedByBinding(string pathAndQuery)
    {
        await using var app = await StartAppAsync(withConvention: true);

        var (status, _) = await GetAsync(app, pathAndQuery);

        status.Should().Be(HttpStatusCode.BadRequest, "the filter runs after binding: it never sees a non-date");
    }

    /// <summary>
    /// #4519 — un <see cref="DateTimeOffset"/> in query con offset diverso da zero arriva all'handler a
    /// offset zero, stesso istante. Senza offset il binding lo legge già a offset zero con lo stesso
    /// orologio (la regola di #4063): resta com'è.
    /// </summary>
    [Theory]
    [InlineData("/probe/offset?at=2026-01-01T10:00:00%2B01:00", "2026-01-01T09:00:00.0000000+00:00")]
    [InlineData("/probe/offset?at=2026-01-01T10:00:00-05:00", "2026-01-01T15:00:00.0000000+00:00")]
    [InlineData("/probe/offset-surrogate?from=2026-01-01T10:00:00%2B01:00", "2026-01-01T09:00:00.0000000+00:00")]
    [InlineData("/probe/offset?at=2026-01-01T10:00:00", "2026-01-01T10:00:00.0000000+00:00")]
    [InlineData("/probe/offset?at=2026-01-01", "2026-01-01T00:00:00.0000000+00:00")]
    [InlineData("/probe/offset?at=2026-01-01T10:00:00Z", "2026-01-01T10:00:00.0000000+00:00")]
    [InlineData("/probe/offset", "null")]
    public async Task WithTheConvention_DateTimeOffset_ReachesTheHandlerAtOffsetZero(string pathAndQuery, string expected)
    {
        await using var app = await StartAppAsync(withConvention: true);

        var (status, body) = await GetAsync(app, pathAndQuery);

        status.Should().Be(HttpStatusCode.OK);
        body.Should().Be(expected);
    }

    [Fact]
    public async Task WithoutTheConvention_DateTimeOffsetKeepsTheOffsetOfTheQuery()
    {
        await using var app = await StartAppAsync(withConvention: false);

        var (status, body) = await GetAsync(app, "/probe/offset?at=2026-01-01T10:00:00%2B01:00");

        status.Should().Be(HttpStatusCode.OK);
        body.Should().Be("2026-01-01T10:00:00.0000000+01:00",
            "the binding keeps the offset written in the query, and Npgsql rejects it on timestamptz (#4519); " +
            "if the framework changes, the DateTimeOffset branch of the filter must be re-examined");
    }

    [Fact]
    public async Task WithoutTheConvention_BindingYieldsUnspecified()
    {
        await using var app = await StartAppAsync(withConvention: false);

        var (status, body) = await GetAsync(app, "/probe/nullable?at=2026-01-01T10:00:00");

        status.Should().Be(HttpStatusCode.OK);
        body.Should().Be("Unspecified|2026-01-01T10:00:00.0000000",
            "this is the framework behaviour #4063 rests on; if it changes, the fix must be re-examined");
    }

    [Fact]
    public async Task WithTheConvention_EveryEndpointOfTheGroupCarriesTheMarker()
    {
        await using var app = await StartAppAsync(withConvention: true);

        var endpoints = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        endpoints.Should().HaveCount(7);
        endpoints.Should().OnlyContain(e => e.Metadata.GetMetadata<UtcDateTimeArgumentsMetadata>() != null,
            "the marker is what the inventory test reads to know an endpoint goes through the filter");
    }
}
