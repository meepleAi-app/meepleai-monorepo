using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>#3435 (SP4): the smoldocling /extract-image client maps the snake_case JSON contract.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class SmoldoclingTableExtractorTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _json;
        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpStatusCode status, string json)
        {
            _status = status;
            _json = json;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                // Materialize the multipart body (proves it was built without throwing).
                await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            };
        }
    }

    private static (ISmolDoclingTableExtractor client, StubHandler handler) Create(HttpStatusCode status, string json)
    {
        var handler = new StubHandler(status, json);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://smoldocling-test") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(SmoldoclingTableExtractor.NamedClientKey)).Returns(http);
        return (new SmoldoclingTableExtractor(factory.Object, NullLogger<SmoldoclingTableExtractor>.Instance), handler);
    }

    [Fact]
    public async Task ExtractTableAsync_MapsTableResponse_AndPostsToExtractImage()
    {
        const string json = """
        {"is_table":true,"reason":"table-otsl","markdown":"| a |","bbox":[0.1,0.2,0.9,0.6],"confidence":0.9,"prefiltered":false,"degenerated":false,"colorfulness":5.0,"duration_ms":3000}
        """;
        var (client, handler) = Create(HttpStatusCode.OK, json);

        var result = await client.ExtractTableAsync(new byte[] { 1, 2, 3 }, prefilter: null, CancellationToken.None);

        result.IsTable.Should().BeTrue();
        result.Reason.Should().Be("table-otsl");
        result.Markdown.Should().Be("| a |");
        result.Confidence.Should().Be(0.9);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/v1/extract-image");
    }

    [Fact]
    public async Task ExtractTableAsync_MapsNonTableResponse()
    {
        const string json = """
        {"is_table":false,"reason":"no-otsl","markdown":"","confidence":0.0,"prefiltered":false,"degenerated":false,"colorfulness":80.0,"duration_ms":1000}
        """;
        var (client, _) = Create(HttpStatusCode.OK, json);

        var result = await client.ExtractTableAsync(new byte[] { 1 }, prefilter: false, CancellationToken.None);

        result.IsTable.Should().BeFalse();
        result.Reason.Should().Be("no-otsl");
        result.Markdown.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractTableAsync_ThrowsOnServerError()
    {
        var (client, _) = Create(HttpStatusCode.InternalServerError, "boom");

        var act = () => client.ExtractTableAsync(new byte[] { 1 }, null, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
