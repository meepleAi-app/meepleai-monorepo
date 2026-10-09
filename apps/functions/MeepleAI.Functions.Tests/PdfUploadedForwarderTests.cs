using System.Net;
using System.Text;
using FluentAssertions;
using MeepleAI.Functions.Contracts;
using MeepleAI.Functions.Forwarding;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace MeepleAI.Functions.Tests;

public sealed class PdfUploadedForwarderTests
{
    private const string NotFoundBody = "{\"outcome\":\"not_found\"}";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string? body = null) =>
        new(status) { Content = new StringContent(body ?? string.Empty, Encoding.UTF8, "application/json") };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "HttpClient lives for the duration of the test and is owned by the stubbed factory")]
    private static (PdfUploadedForwarder Sut, StubHandler Handler) Build(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api.test") };
        client.DefaultRequestHeaders.Add(PdfUploadedForwarder.TokenHeader, "t");
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(PdfUploadedForwarder.HttpClientName).Returns(client);
        return (new PdfUploadedForwarder(factory, NullLogger<PdfUploadedForwarder>.Instance), handler);
    }

    private static PdfUploadedEventV1 Event() =>
        PdfUploadedContract.TryParse(BinaryData.FromString(ContractFixture.ReadV1())).Event!;

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task ForwardAsync_DefinitiveStatus_IsSettled(HttpStatusCode status)
    {
        var body = status == HttpStatusCode.NotFound ? NotFoundBody : null;
        var (sut, handler) = Build(_ => Response(status, body));

        var outcome = await sut.ForwardAsync(Event(), TestContext.Current.CancellationToken);

        outcome.Should().Be(ForwardOutcome.Settled);
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be(PdfUploadedForwarder.Route);
        handler.LastRequest.Headers.GetValues(PdfUploadedForwarder.TokenHeader).Should().ContainSingle().Which.Should().Be("t");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"error\":\"nope\"}")]
    public async Task ForwardAsync_404WithoutOutcome_IsTransient(string body)
    {
        var (sut, _) = Build(_ => Response(HttpStatusCode.NotFound, body));

        (await sut.ForwardAsync(Event(), TestContext.Current.CancellationToken)).Should().Be(ForwardOutcome.TransientFailure);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ForwardAsync_ServerError_IsTransient(HttpStatusCode status)
    {
        var (sut, _) = Build(_ => Response(status));

        (await sut.ForwardAsync(Event(), TestContext.Current.CancellationToken)).Should().Be(ForwardOutcome.TransientFailure);
    }

    [Fact]
    public async Task ForwardAsync_NetworkFailure_IsTransient()
    {
        var (sut, _) = Build(_ => throw new HttpRequestException("refused"));

        (await sut.ForwardAsync(Event(), TestContext.Current.CancellationToken)).Should().Be(ForwardOutcome.TransientFailure);
    }
}
