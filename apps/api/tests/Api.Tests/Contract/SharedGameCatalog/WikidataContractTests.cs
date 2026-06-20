using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Api.Tests.Contract.SharedGameCatalog;

/// <summary>
/// Contract tests for the upstream Wikidata SPARQL endpoint and the Wikimedia
/// Commons <c>imageinfo</c> API. These tests pin the request/response shape
/// consumed by <see cref="WikidataCatalogProvider.FetchCoverImageAsync"/> and
/// <see cref="WikimediaCommonsClient.FetchLicenseAsync"/> so that an upstream
/// rename (Crispin C-002 concern — issue #2055 AC-G5) breaks the build before
/// production catches it.
/// </summary>
/// <remarks>
/// <para>
/// Strategy: spin up an in-process <see cref="WireMockServer"/> per test
/// scenario, point a real <see cref="HttpClient"/> at it, then inject that
/// client into the actual producer (no mocks of the parser code). The fixture
/// JSONs under <c>Fixtures/Wikidata/</c> are intentionally captured from live
/// API responses so contract drift surfaces as a parser regression.
/// </para>
/// <para>
/// License whitelist scenarios are exercised directly against
/// <see cref="LicenseValidator"/> because the whitelist is a pure helper with
/// no HTTP surface; this gives surgical coverage of DEC-3c without the WireMock
/// orchestration overhead.
/// </para>
/// <para>
/// Fixture refresh: run <c>infra/scripts/wikidata-fixtures/refresh.sh</c>
/// quarterly to pull live SPARQL + Commons responses and diff-review before
/// committing. The script targets the same Catan QID (<c>Q1057010</c>) used
/// by <see cref="FetchCoverImage_SuccessfulP18_ReturnsImageUri"/>.
/// </para>
/// </remarks>
[Trait("Category", "Contract")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2055")]
public sealed class WikidataContractTests : IAsyncLifetime
{
    private const string CatanQid = "Q1057010";

    private WireMockServer _wikidataServer = null!;
    private WireMockServer _commonsServer = null!;

    public ValueTask InitializeAsync()
    {
        _wikidataServer = WireMockServer.Start();
        _commonsServer = WireMockServer.Start();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _wikidataServer?.Stop();
        _wikidataServer?.Dispose();
        _commonsServer?.Stop();
        _commonsServer?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fixture loader — resolves files copied to the test output dir via the
    // <None Include="Fixtures\**\*.*" CopyToOutputDirectory="PreserveNewest" />
    // entry in Api.Tests.csproj.
    // ─────────────────────────────────────────────────────────────────────────
    private static string LoadFixture(string fileName)
    {
        var assemblyDir = Path.GetDirectoryName(typeof(WikidataContractTests).Assembly.Location)
                          ?? throw new InvalidOperationException("Assembly location resolved to null.");
        var path = Path.Combine(assemblyDir, "Fixtures", "Wikidata", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Contract fixture not found: {path}", path);
        }
        return File.ReadAllText(path);
    }

    private WikidataCatalogProvider CreateWikidataProvider()
    {
        var http = new HttpClient { BaseAddress = new Uri(_wikidataServer.Url!) };
        var rateLimiter = new Mock<IWikimediaRateLimiter>();
        rateLimiter.Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>()))
                   .Returns(ValueTask.CompletedTask);
        return new WikidataCatalogProvider(
            http,
            new Mock<ILogger<WikidataCatalogProvider>>().Object,
            rateLimiter.Object);
    }

    private WikimediaCommonsClient CreateCommonsClient()
    {
        var http = new HttpClient { BaseAddress = new Uri(_commonsServer.Url! + "/") };
        var rateLimiter = new Mock<IWikimediaRateLimiter>();
        rateLimiter.Setup(r => r.AcquireAsync(It.IsAny<CancellationToken>()))
                   .Returns(ValueTask.CompletedTask);
        return new WikimediaCommonsClient(
            http,
            rateLimiter.Object,
            new Mock<ILogger<WikimediaCommonsClient>>().Object);
    }

    // =========================================================================
    // Wikidata SPARQL contract — wdt:P18 cover-image lookup
    // =========================================================================

    [Fact]
    public async Task FetchCoverImage_SuccessfulP18_ReturnsImageFilename()
    {
        // Arrange — pin the SPARQL response shape parsed by ParseCoverResponse:
        //   results.bindings[0].image.value → http://commons.wikimedia.org/wiki/Special:FilePath/{filename}
        _wikidataServer
            .Given(Request.Create().WithPath("/sparql").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/sparql-results+json")
                .WithBody(LoadFixture("P18-catan-q1234.json")));

        var provider = CreateWikidataProvider();

        // Act
        var result = await provider.FetchCoverImageAsync(CatanQid, CancellationToken.None);

        // Assert — contract: producer parses the bindings[].image.value IRI,
        // strips the Special:FilePath/ prefix, and returns the URL-encoded
        // filename (preserved verbatim per WikidataCoverImageResult.Filename docs).
        result.Should().NotBeNull();
        result.HasImage.Should().BeTrue("the fixture publishes a wdt:P18 binding");
        result.Filename.Should().Be("Catan-2015-boxed-edition.jpg");
        result.SourceUrl.Should().Be($"https://www.wikidata.org/wiki/{CatanQid}");
    }

    [Fact]
    public async Task FetchCoverImage_NoP18Binding_ReturnsNotFound()
    {
        // Arrange — empty bindings array (valid SPARQL response with no P18 claim).
        _wikidataServer
            .Given(Request.Create().WithPath("/sparql").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/sparql-results+json")
                .WithBody(LoadFixture("P18-no-image.json")));

        var provider = CreateWikidataProvider();

        // Act
        var result = await provider.FetchCoverImageAsync(CatanQid, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.HasImage.Should().BeFalse("the fixture returns zero bindings");
        result.Filename.Should().BeNull();
        result.SourceUrl.Should().Be($"https://www.wikidata.org/wiki/{CatanQid}");
    }

    [Fact]
    public async Task FetchCoverImage_503ServiceUnavailable_ReturnsNotFoundGracefully()
    {
        // Arrange — Wikidata routinely returns 503 under load; producer must
        // degrade gracefully without throwing (caller decides retry, DEC-3f
        // circuit breaker handles repeated failures).
        _wikidataServer
            .Given(Request.Create().WithPath("/sparql").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithHeader("Content-Type", "text/html")
                .WithBody(LoadFixture("P18-503.json")));

        var provider = CreateWikidataProvider();

        // Act
        var act = async () => await provider.FetchCoverImageAsync(CatanQid, CancellationToken.None);

        // Assert — must not throw; producer logs warning + returns NotFound.
        var result = await act.Should().NotThrowAsync();
        result.Subject.HasImage.Should().BeFalse("503 must collapse to NotFound");
        result.Subject.Filename.Should().BeNull();
    }

    // =========================================================================
    // Wikimedia Commons imageinfo contract — license + attribution extraction
    // =========================================================================
    //
    // The graceful-degradation contract (malformed JSON, missing fields, 5xx)
    // is exercised here through the real WikimediaCommonsClient. The pure
    // whitelist verdict is exercised against LicenseValidator directly below.

    [Fact]
    public async Task FetchLicense_MalformedJson_ReturnsNotAvailableGracefully()
    {
        // Arrange — Commons API sometimes returns truncated JSON under load.
        // ParseResponse catches JsonException and returns NotAvailable per
        // WikimediaCommonsClient.FetchLicenseAsync line 127-134.
        _commonsServer
            .Given(Request.Create().WithPath("/w/api.php").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(LoadFixture("commons-malformed.json")));

        var client = CreateCommonsClient();

        // Act
        var act = async () => await client.FetchLicenseAsync("Brass%20Birmingham%20cover.jpg", CancellationToken.None);

        // Assert — must not throw; producer logs warning + returns NotAvailable.
        var result = await act.Should().NotThrowAsync();
        result.Subject.RawLicense.Should().BeNull("malformed JSON must collapse to NotAvailable");
        result.Subject.IsWhitelisted.Should().BeFalse();
        result.Subject.Attribution.Should().BeNull();
    }

    [Fact]
    public async Task FetchLicense_MissingLicenseShortName_ReturnsNotAvailableGracefully()
    {
        // Arrange — Commons occasionally returns extmetadata without
        // LicenseShortName for files lacking machine-readable license templates.
        // ExtractFromPage handles this at WikimediaCommonsClient.cs lines
        // 285-293.
        _commonsServer
            .Given(Request.Create().WithPath("/w/api.php").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(LoadFixture("commons-license-missing.json")));

        var client = CreateCommonsClient();

        // Act
        var result = await client.FetchLicenseAsync("Untitled.jpg", CancellationToken.None);

        // Assert — producer logs warning + returns NotAvailable; never throws.
        result.RawLicense.Should().BeNull("LicenseShortName must be present to surface a verdict");
        result.IsWhitelisted.Should().BeFalse();
        result.Attribution.Should().BeNull();
    }

    [Fact]
    public async Task FetchLicense_PublicDomain_ParsesRawLicenseAndMarksWhitelisted()
    {
        // Arrange — successful Commons response with Public Domain license.
        // Verifies the end-to-end contract: producer parses
        // extmetadata.LicenseShortName.value, runs it through LicenseValidator,
        // and returns the whitelist verdict.
        _commonsServer
            .Given(Request.Create().WithPath("/w/api.php").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(LoadFixture("commons-license-pd.json")));

        var client = CreateCommonsClient();

        // Act
        var result = await client.FetchLicenseAsync("Catan-2015-boxed-edition.jpg", CancellationToken.None);

        // Assert
        result.RawLicense.Should().Be("Public domain");
        result.IsWhitelisted.Should().BeTrue("Public domain is in the DEC-3c whitelist");
        result.Attribution.Should().Be("Asterion Press", "HTML tags must be stripped from extmetadata.Artist");
    }

    [Fact]
    public async Task FetchLicense_CcBySa_ParsesRawLicenseAndMarksWhitelisted()
    {
        // Arrange — successful Commons response with CC BY-SA 4.0 license.
        _commonsServer
            .Given(Request.Create().WithPath("/w/api.php").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(LoadFixture("commons-license-cc-by-sa.json")));

        var client = CreateCommonsClient();

        // Act
        var result = await client.FetchLicenseAsync("Brass%20Birmingham%20cover.jpg", CancellationToken.None);

        // Assert
        result.RawLicense.Should().Be("CC BY-SA 4.0");
        result.IsWhitelisted.Should().BeTrue("CC-BY-SA (any version) is in the DEC-3c whitelist");
        result.Attribution.Should().Be("Roxley Games");
    }

    [Fact]
    public async Task FetchLicense_CcByNc_IsRejectedByWhitelist()
    {
        // Arrange — CC-BY-NC is NOT in the whitelist (non-commercial restriction
        // is incompatible with the catalog's redistribution model).
        _commonsServer
            .Given(Request.Create().WithPath("/w/api.php").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(LoadFixture("commons-license-cc-by-nc.json")));

        var client = CreateCommonsClient();

        // Act
        var result = await client.FetchLicenseAsync("Encumbered%20cover.jpg", CancellationToken.None);

        // Assert — license IS returned, but the whitelist verdict rejects it.
        // Downstream callers see IsWhitelisted=false and skip persistence.
        result.RawLicense.Should().Be("CC BY-NC 4.0");
        result.IsWhitelisted.Should().BeFalse("CC-BY-NC is intentionally excluded from DEC-3c");
        result.Attribution.Should().Be("Non-Commercial Artist");
    }

    // =========================================================================
    // LicenseValidator direct contract — DEC-3c whitelist regex
    // =========================================================================
    //
    // Crispin C-002 concern: if the upstream Commons format ever renames the
    // canonical license short names (e.g. "CC BY-SA 4.0" → "CC-BY-SA-4.0-EN")
    // the regex would silently start failing. These tests pin the EXACT strings
    // the spike sess.46h validated against 13 real boardgame samples.

    [Theory]
    [InlineData("Public domain")]
    [InlineData("PD")]
    [InlineData("CC0")]
    [InlineData("CC BY 2.0")]
    [InlineData("CC-BY-2.0")]
    [InlineData("CC BY-SA 4.0")]
    [InlineData("CC-BY-SA-4.0")]
    public void LicenseValidator_KnownGoodShortNames_AreWhitelisted(string licenseShortName)
    {
        // Act
        var result = LicenseValidator.IsWhitelisted(licenseShortName);

        // Assert — pin the spike sess.46h whitelist (DEC-3c).
        result.Should().BeTrue($"'{licenseShortName}' is in the DEC-3c whitelist");
    }

    [Theory]
    [InlineData("CC BY-NC 4.0")]
    [InlineData("CC BY-ND 4.0")]
    [InlineData("CC BY-NC-SA 4.0")]
    [InlineData("All Rights Reserved")]
    [InlineData("Fair use")]
    [InlineData("GFDL")]
    public void LicenseValidator_KnownBadShortNames_AreRejected(string licenseShortName)
    {
        // Act
        var result = LicenseValidator.IsWhitelisted(licenseShortName);

        // Assert — adversarial fixtures guard against Crispin C-002 future drift.
        result.Should().BeFalse($"'{licenseShortName}' must NOT be in the DEC-3c whitelist");
    }
}
