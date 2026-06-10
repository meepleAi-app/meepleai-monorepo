using System.Net;
using System.Text;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="WikimediaCommonsClient"/> — Phase B foundation for
/// issue #1823 L2 cover enrichment (ADR DEC-3b/3c/3e).
/// </summary>
/// <remarks>
/// <para>
/// Validates the contract documented on
/// <see cref="IWikimediaCommonsClient.FetchLicenseAsync"/>:
/// <list type="bullet">
///   <item><c>filename</c> arrives URL-encoded (extracted from a Wikidata <c>wdt:P18</c>
///   IRI returning a Special:FilePath redirect). The client MUST decode via
///   <see cref="Uri.UnescapeDataString"/> before constructing the API call.</item>
///   <item>Rate limiter acquires BEFORE the HTTP call (5 RPS shared cap, DEC-3e).</item>
///   <item>License is validated against <see cref="LicenseValidator"/> whitelist (DEC-3c).</item>
///   <item>5xx, malformed JSON, missing page, missing metadata all return
///   <see cref="CommonsLicenseResult.NotAvailable"/> with a warning log — never throw.</item>
///   <item><see cref="OperationCanceledException"/> is rethrown (cancellation respected).</item>
/// </list>
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1823")]
public class WikimediaCommonsClientTests
{
    private const string CommonsBaseUrl = "https://commons.wikimedia.org/";

    private static WikimediaCommonsClient CreateClient(
        HttpMessageHandler handler,
        IWikimediaRateLimiter? rateLimiter = null)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(CommonsBaseUrl)
        };

        return new WikimediaCommonsClient(
            http,
            rateLimiter ?? new Mock<IWikimediaRateLimiter>().Object,
            new Mock<ILogger<WikimediaCommonsClient>>().Object);
    }

    private static Mock<HttpMessageHandler> HandlerReturning(HttpStatusCode statusCode, string body)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        return handler;
    }

    private static Mock<HttpMessageHandler> HandlerCapturing(
        HttpStatusCode statusCode,
        string body,
        List<HttpRequestMessage> captured)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured.Add(req))
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        return handler;
    }

    /// <summary>
    /// Canonical Commons imageinfo response wrapped around the supplied
    /// <c>LicenseShortName</c> and <c>Artist</c>. Page id <c>12345</c> mirrors a
    /// real successful lookup.
    /// </summary>
    private static string BuildImageInfoResponse(string licenseShortName, string? artistHtml = null)
    {
        var artistFragment = artistHtml is null
            ? string.Empty
            : $",\"Artist\":{{\"value\":\"{artistHtml}\",\"source\":\"commons-desc-page\"}}";

        return $@"{{
  ""query"": {{
    ""pages"": {{
      ""12345"": {{
        ""pageid"": 12345,
        ""ns"": 6,
        ""title"": ""File:Brass Birmingham cover.jpg"",
        ""imageinfo"": [
          {{
            ""extmetadata"": {{
              ""LicenseShortName"": {{ ""value"": ""{licenseShortName}"", ""source"": ""commons-desc-page"" }}
              {artistFragment}
            }}
          }}
        ]
      }}
    }}
  }}
}}";
    }

    // ──────────────────────────────────────────────────────────────────────
    // URL decoding — DEC-3b pitfall: filename from wdt:P18 IRI is URL-encoded.
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchLicenseAsync_UrlEncodedFilename_DecodesBeforeApiCall()
    {
        var captured = new List<HttpRequestMessage>();
        var handler = HandlerCapturing(HttpStatusCode.OK,
            BuildImageInfoResponse("CC BY-SA 4.0"),
            captured);
        var client = CreateClient(handler.Object);

        // Input matches the URL-encoded form returned by a wdt:P18 Special:FilePath
        // redirect (spaces → %20). The API title parameter must arrive as
        // "File:Brass Birmingham cover.jpg" — the API will re-encode the spaces
        // as part of the query string, but the underlying title MUST be decoded.
        await client.FetchLicenseAsync("Brass%20Birmingham%20cover.jpg", CancellationToken.None);

        captured.Should().HaveCount(1);
        var requestUri = captured[0].RequestUri!.ToString();

        // The query string ends up percent-encoded again by the URL builder, but
        // the LOGICAL title value should be the decoded "File:Brass Birmingham cover.jpg".
        // Match against either the literal decoded form or the standard re-encoding
        // (%20 or '+' — both are valid query-string encodings of space).
        var titleValue = ExtractTitleParam(requestUri);
        titleValue.Should().Be("File:Brass Birmingham cover.jpg",
            "the Commons API expects the raw filename, not the URL-encoded form from wdt:P18");
    }

    [Fact]
    public async Task FetchLicenseAsync_PlainFilename_PassesThroughUnchanged()
    {
        var captured = new List<HttpRequestMessage>();
        var handler = HandlerCapturing(HttpStatusCode.OK,
            BuildImageInfoResponse("CC0"),
            captured);
        var client = CreateClient(handler.Object);

        await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        captured.Should().HaveCount(1);
        ExtractTitleParam(captured[0].RequestUri!.ToString())
            .Should().Be("File:Cover.jpg",
                "plain filenames without escape sequences pass through unchanged");
    }

    // ──────────────────────────────────────────────────────────────────────
    // License validation — DEC-3c whitelist
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CC BY-SA 4.0")]
    [InlineData("CC BY 2.0")]
    [InlineData("CC0")]
    [InlineData("Public domain")]
    public async Task FetchLicenseAsync_WhitelistedLicense_ReturnsFoundWithIsWhitelistedTrue(string license)
    {
        var handler = HandlerReturning(HttpStatusCode.OK, BuildImageInfoResponse(license));
        var client = CreateClient(handler.Object);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.RawLicense.Should().Be(license);
        result.IsWhitelisted.Should().BeTrue();
    }

    [Theory]
    [InlineData("All rights reserved")]
    [InlineData("CC BY-NC 4.0")]
    [InlineData("CC BY-ND 3.0")]
    [InlineData("Fair use")]
    public async Task FetchLicenseAsync_NonWhitelistedLicense_ReturnsFoundWithIsWhitelistedFalse(string license)
    {
        var handler = HandlerReturning(HttpStatusCode.OK, BuildImageInfoResponse(license));
        var client = CreateClient(handler.Object);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.RawLicense.Should().Be(license);
        result.IsWhitelisted.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Page / metadata absence
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchLicenseAsync_MissingPage_ReturnsNotAvailable()
    {
        // Commons API returns pageId "-1" when the file does not exist.
        const string Body = @"{
  ""query"": {
    ""pages"": {
      ""-1"": {
        ""ns"": 6,
        ""title"": ""File:Missing.jpg"",
        ""missing"": """"
      }
    }
  }
}";
        var handler = HandlerReturning(HttpStatusCode.OK, Body);
        var client = CreateClient(handler.Object);

        var result = await client.FetchLicenseAsync("Missing.jpg", CancellationToken.None);

        result.RawLicense.Should().BeNull();
        result.IsWhitelisted.Should().BeFalse();
        result.Attribution.Should().BeNull();
        result.Should().Be(CommonsLicenseResult.NotAvailable());
    }

    [Fact]
    public async Task FetchLicenseAsync_MissingExtmetadata_ReturnsNotAvailable()
    {
        // imageinfo present but extmetadata.LicenseShortName is missing.
        const string Body = @"{
  ""query"": {
    ""pages"": {
      ""12345"": {
        ""pageid"": 12345,
        ""title"": ""File:Cover.jpg"",
        ""imageinfo"": [
          {
            ""extmetadata"": {}
          }
        ]
      }
    }
  }
}";
        var handler = HandlerReturning(HttpStatusCode.OK, Body);
        var client = CreateClient(handler.Object);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.Should().Be(CommonsLicenseResult.NotAvailable());
    }

    [Fact]
    public async Task FetchLicenseAsync_MissingImageinfoArray_ReturnsNotAvailable()
    {
        // pages.{id} present but no imageinfo array.
        const string Body = @"{
  ""query"": {
    ""pages"": {
      ""12345"": {
        ""pageid"": 12345,
        ""title"": ""File:Cover.jpg""
      }
    }
  }
}";
        var handler = HandlerReturning(HttpStatusCode.OK, Body);
        var client = CreateClient(handler.Object);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.Should().Be(CommonsLicenseResult.NotAvailable());
    }

    // ──────────────────────────────────────────────────────────────────────
    // Rate limiter — DEC-3e: acquire BEFORE HTTP call
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchLicenseAsync_AcquiresRateLimiterBeforeHttpCall()
    {
        var sequence = new List<string>();

        var rateLimiter = new Mock<IWikimediaRateLimiter>();
        rateLimiter.Setup(rl => rl.AcquireAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("acquire"))
            .Returns(ValueTask.CompletedTask);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => sequence.Add("http"))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(BuildImageInfoResponse("CC0"),
                    Encoding.UTF8, "application/json")
            });

        var client = CreateClient(handler.Object, rateLimiter.Object);

        await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        sequence.Should().Equal(new[] { "acquire", "http" },
            because: "DEC-3e mandates the 5 RPS token bucket is consumed BEFORE the outbound HTTP request to coordinate the shared cap across Wikidata SPARQL + Commons API consumers");
        rateLimiter.Verify(rl => rl.AcquireAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────
    // HTTP failure modes
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task FetchLicenseAsync_Http5xx_LogsWarningAndReturnsNotAvailable(HttpStatusCode status)
    {
        var logger = new Mock<ILogger<WikimediaCommonsClient>>();
        var handler = HandlerReturning(status, "{}");

        var http = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri(CommonsBaseUrl)
        };
        var client = new WikimediaCommonsClient(
            http,
            new Mock<IWikimediaRateLimiter>().Object,
            logger.Object);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.Should().Be(CommonsLicenseResult.NotAvailable(),
            "5xx is a server-side outage — caller decides retry, client never throws upstream");
        logger.Invocations
            .Should().Contain(i => i.Method.Name == nameof(ILogger.Log)
                && (LogLevel)i.Arguments[0] == LogLevel.Warning,
                $"5xx ({(int)status}) must surface a warning log line for observability");
    }

    [Fact]
    public async Task FetchLicenseAsync_HttpCancellation_RethrowsOperationCanceledException()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("caller cancelled"));

        var client = CreateClient(handler.Object);

        await client.Invoking(c => c.FetchLicenseAsync("Cover.jpg", CancellationToken.None))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FetchLicenseAsync_MalformedJson_LogsWarningAndReturnsNotAvailable()
    {
        var logger = new Mock<ILogger<WikimediaCommonsClient>>();
        var handler = HandlerReturning(HttpStatusCode.OK, "<<< not json >>>");

        var http = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri(CommonsBaseUrl)
        };
        var client = new WikimediaCommonsClient(
            http,
            new Mock<IWikimediaRateLimiter>().Object,
            logger.Object);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.Should().Be(CommonsLicenseResult.NotAvailable());
        logger.Invocations
            .Should().Contain(i => i.Method.Name == nameof(ILogger.Log)
                && (LogLevel)i.Arguments[0] == LogLevel.Warning);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Attribution extraction
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FetchLicenseAsync_ExtractsArtistAttribution_StripsHtmlTags()
    {
        var handler = HandlerReturning(HttpStatusCode.OK,
            BuildImageInfoResponse("CC BY-SA 4.0", artistHtml: "<a href=\\\"//commons.wikimedia.org/wiki/User:JohnDoe\\\">John Doe</a>"));
        var client = CreateClient(handler.Object);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.Attribution.Should().Be("John Doe",
            "Artist field arrives HTML-encoded from Commons; the inner text is the attribution to display in shared_games.cover_attribution");
    }

    [Fact]
    public async Task FetchLicenseAsync_NoArtistField_ReturnsNullAttribution()
    {
        var handler = HandlerReturning(HttpStatusCode.OK, BuildImageInfoResponse("CC0"));
        var client = CreateClient(handler.Object);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.Attribution.Should().BeNull("CC0 / public domain images typically omit the Artist field");
        result.IsWhitelisted.Should().BeTrue();
        result.RawLicense.Should().Be("CC0");
    }

    [Fact]
    public async Task FetchLicenseAsync_PlainTextArtist_ReturnsAsIs()
    {
        var handler = HandlerReturning(HttpStatusCode.OK,
            BuildImageInfoResponse("CC BY 4.0", artistHtml: "Jane Smith"));
        var client = CreateClient(handler.Object);

        var result = await client.FetchLicenseAsync("Cover.jpg", CancellationToken.None);

        result.Attribution.Should().Be("Jane Smith");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the decoded <c>titles=</c> query parameter from a request URI.
    /// The Commons API call is constructed as
    /// <c>w/api.php?action=query&amp;...&amp;titles={Uri.EscapeDataString("File:" + decoded)}&amp;format=json</c>;
    /// this helper re-decodes the percent-encoding so assertions can match
    /// against the logical title string.
    /// </summary>
    private static string ExtractTitleParam(string requestUri)
    {
        var queryStart = requestUri.IndexOf('?');
        if (queryStart < 0) return string.Empty;

        var query = requestUri[(queryStart + 1)..];
        foreach (var pair in query.Split('&'))
        {
            var equalsIndex = pair.IndexOf('=');
            if (equalsIndex < 0) continue;

            var key = pair[..equalsIndex];
            if (!string.Equals(key, "titles", StringComparison.Ordinal)) continue;

            var rawValue = pair[(equalsIndex + 1)..];
            // Decode '+' as space (form encoding) before percent decoding.
            return Uri.UnescapeDataString(rawValue.Replace('+', ' '));
        }

        return string.Empty;
    }
}
