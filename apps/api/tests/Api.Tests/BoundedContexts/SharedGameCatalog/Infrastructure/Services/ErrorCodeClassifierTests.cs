using System.Net;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="ErrorCodeClassifier"/> (#1907). Each test maps a representative
/// exception shape onto the expected machine-readable error code so the admin Failed Items
/// panel and the <c>enrichment_attempts.error_code</c> column stay stable as new exception
/// types appear inside <see cref="Api.Infrastructure.BackgroundServices.BggImportQueueBackgroundService"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1907")]
public sealed class ErrorCodeClassifierTests
{
    [Fact]
    public void Classify_NullException_Throws()
    {
        var act = () => ErrorCodeClassifier.Classify(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Classify_HttpRequestException_429_ReturnsRateLimit()
    {
        var ex = new HttpRequestException("rate-limited", null, HttpStatusCode.TooManyRequests);
        ErrorCodeClassifier.Classify(ex).Should().Be(ErrorCodeClassifier.BggRateLimit);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "BGG_SERVER_ERROR_500")]
    [InlineData(HttpStatusCode.BadGateway, "BGG_SERVER_ERROR_502")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "BGG_SERVER_ERROR_503")]
    [InlineData(HttpStatusCode.GatewayTimeout, "BGG_SERVER_ERROR_504")]
    public void Classify_HttpRequestException_5xx_ReturnsServerErrorWithStatus(
        HttpStatusCode status, string expected)
    {
        var ex = new HttpRequestException("server-error", null, status);
        ErrorCodeClassifier.Classify(ex).Should().Be(expected);
    }

    [Fact]
    public void Classify_HttpRequestException_NoStatus_ReturnsTimeout()
    {
        // DNS failure / TCP reset / TLS error → no status code on the exception
        var ex = new HttpRequestException("dns-failure");
        ErrorCodeClassifier.Classify(ex).Should().Be(ErrorCodeClassifier.BggTimeout);
    }

    [Fact]
    public void Classify_HttpRequestException_4xx_NotMappedToRateLimit_ReturnsTimeout()
    {
        // 4xx other than 429 falls through to the no-mapping default. Currently
        // collapses to BGG_TIMEOUT (transient retry class) rather than UNKNOWN so
        // the existing retry/backoff logic exercises it. If we ever need a distinct
        // 4xx category, split here.
        var ex = new HttpRequestException("not-found", null, HttpStatusCode.NotFound);
        ErrorCodeClassifier.Classify(ex).Should().Be(ErrorCodeClassifier.BggTimeout);
    }

    [Fact]
    public void Classify_TaskCanceledException_ReturnsTimeout()
    {
        ErrorCodeClassifier.Classify(new TaskCanceledException()).Should().Be(ErrorCodeClassifier.BggTimeout);
    }

    [Fact]
    public void Classify_OperationCanceledException_ReturnsTimeout()
    {
        ErrorCodeClassifier.Classify(new OperationCanceledException()).Should().Be(ErrorCodeClassifier.BggTimeout);
    }

    [Fact]
    public void Classify_JsonException_ReturnsSchemaMismatch()
    {
        var ex = new System.Text.Json.JsonException("bad shape");
        ErrorCodeClassifier.Classify(ex).Should().Be(ErrorCodeClassifier.SchemaMismatch);
    }

    [Fact]
    public void Classify_XmlException_ReturnsSchemaMismatch()
    {
        var ex = new System.Xml.XmlException("bad xml");
        ErrorCodeClassifier.Classify(ex).Should().Be(ErrorCodeClassifier.SchemaMismatch);
    }

    [Fact]
    public void Classify_InvalidOperationException_ReturnsSchemaMismatch()
    {
        // Domain factory invariants (EnrichmentAttempt.RecordSuccess etc.) raise InvalidOperationException
        // for missing fields — semantically these are schema-mismatch failures too.
        var ex = new InvalidOperationException("missing field");
        ErrorCodeClassifier.Classify(ex).Should().Be(ErrorCodeClassifier.SchemaMismatch);
    }

    [Fact]
    public void Classify_UnknownException_ReturnsUnknown()
    {
        var ex = new NotSupportedException("alien failure");
        ErrorCodeClassifier.Classify(ex).Should().Be(ErrorCodeClassifier.Unknown);
    }
}
