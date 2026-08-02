using System.Net;
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Unit tests for <see cref="SlackWebhookClient"/> covering the per-call
/// webhook URL contract and the resilience guarantees expected by
/// <c>ChannelDispatchHandler</c> (Issue #1840 SP5 F4-C7).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "1840")]
public class SlackWebhookClientTests
{
    // A genuinely-public IP literal. An IP literal keeps the SSRF guard's DNS resolution
    // offline/deterministic in unit tests (Dns.GetHostAddressesAsync on a literal returns it
    // without a DNS query). NB: RFC 5737 TEST-NET ranges are NOT usable here — the IANA-driven
    // SsrfPolicy (#3495) correctly blocks them as reserved; 8.8.8.8 is public and, with the mock
    // handler below, is never actually dialed.
    private const string PublicWebhookUrl = "https://8.8.8.8/services/T1/B1/abc";

    private static SlackWebhookClient CreateClient(Mock<HttpMessageHandler> handler)
    {
        var httpClient = new HttpClient(handler.Object);
        return new SlackWebhookClient(httpClient, new Mock<ILogger<SlackWebhookClient>>().Object);
    }

    private static Mock<HttpMessageHandler> HandlerReturning(HttpStatusCode statusCode, string? body = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body ?? string.Empty)
            });
        return handler;
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulResponse_ReturnsSuccess()
    {
        var handler = HandlerReturning(HttpStatusCode.OK, "ok");
        var client = CreateClient(handler);

        var result = await client.SendAsync(
            PublicWebhookUrl,
            new SlackMessage("Hello"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task SendAsync_With401_ReturnsFailureWithStatusCode()
    {
        var handler = HandlerReturning(HttpStatusCode.Unauthorized, "invalid_token");
        var client = CreateClient(handler);

        var result = await client.SendAsync(
            PublicWebhookUrl,
            new SlackMessage("Hello", Severity: "warning"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendAsync_OnNetworkException_ReturnsFailureWithoutThrowing()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("simulated network failure"));

        var client = CreateClient(handler);

        var result = await client.SendAsync(
            PublicWebhookUrl,
            new SlackMessage("Hello"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("simulated network failure");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SendAsync_WithEmptyWebhookUrl_ReturnsFailureWithoutCallingHttp(string? webhookUrl)
    {
        var handler = HandlerReturning(HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SendAsync(webhookUrl!, new SlackMessage("Hello"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Webhook URL");

        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithRelativeUri_ReturnsFailureWithoutCallingHttp()
    {
        var handler = HandlerReturning(HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.SendAsync("not-a-uri", new SlackMessage("Hello"), CancellationToken.None);

        result.Success.Should().BeFalse();

        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionAsync_OnSuccess_ReturnsOk()
    {
        var handler = HandlerReturning(HttpStatusCode.OK);
        var client = CreateClient(handler);

        var result = await client.TestConnectionAsync(
            PublicWebhookUrl,
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Connection OK");
        result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task TestConnectionAsync_OnFailure_ReturnsErrorMessage()
    {
        var handler = HandlerReturning(HttpStatusCode.NotFound, "no_service");
        var client = CreateClient(handler);

        var result = await client.TestConnectionAsync(
            PublicWebhookUrl,
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrEmpty();
        result.StatusCode.Should().Be(404);
    }

    // ---- SSRF guard (#2655 finding #11) ----

    [Theory]
    [InlineData("http://203.0.113.10/services/T1/B1/abc")]  // non-HTTPS scheme
    [InlineData("ftp://203.0.113.10/services/T1/B1/abc")]    // non-HTTP scheme
    public async Task SendAsync_NonHttpsUrl_BlockedWithoutPosting(string webhookUrl)
    {
        var handler = HandlerReturning(HttpStatusCode.OK, "ok");
        var client = CreateClient(handler);

        var result = await client.SendAsync(webhookUrl, new SlackMessage("Hello"), CancellationToken.None);

        result.Success.Should().BeFalse("a non-HTTPS webhook URL must be blocked by the SSRF guard");
        handler.Protected().Verify(
            "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData("https://127.0.0.1/services/T1/B1/abc")]     // loopback
    [InlineData("https://169.254.169.254/latest")]           // cloud metadata endpoint
    [InlineData("https://10.0.0.5/services/T1/B1/abc")]      // RFC 1918 private
    public async Task SendAsync_PrivateOrReservedIp_BlockedWithoutPosting(string webhookUrl)
    {
        var handler = HandlerReturning(HttpStatusCode.OK, "ok");
        var client = CreateClient(handler);

        var result = await client.SendAsync(webhookUrl, new SlackMessage("Hello"), CancellationToken.None);

        result.Success.Should().BeFalse("a webhook URL resolving to a private/reserved IP must be blocked by the SSRF guard");
        handler.Protected().Verify(
            "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }
}
