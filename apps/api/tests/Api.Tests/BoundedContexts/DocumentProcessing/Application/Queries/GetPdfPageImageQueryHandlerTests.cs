using System.Net;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Issue #3578 — "dependency not deployed here" must not be reported as a server bug.
///
/// `smoldocling-service` is deliberately absent from staging (the 256M model is impractical on CPU),
/// so GET /api/v1/ingest/pdf/{id}/page-image used to answer 500 with a socket error. A 500 says
/// "the server is broken" and forces whoever investigates to read container logs to discover that a
/// service is simply missing. 503 says "this dependency is unavailable", which is both true and
/// actionable.
///
/// The boundary matters: only transport failures, upstream 5xx and timeouts become 503. An upstream
/// 4xx means WE sent something wrong — that is a real bug and must keep surfacing as such, otherwise
/// this mapping would bury it.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class GetPdfPageImageQueryHandlerTests
{
    private static readonly Guid PdfId = Guid.Parse("aaaaaaaa-1111-4000-8000-000000000001");
    private const string StoredFileId = "728c58a174b4425ab5330a64276eecad";

    private static PdfDocument CreatePdf() =>
        new(
            PdfId,
            Guid.NewGuid(),
            new FileName("rulebook.pdf"),
            $"pdfs/{PdfId:N}/{StoredFileId}_rulebook.pdf",
            new FileSize(1024),
            Guid.NewGuid());

    private static GetPdfPageImageQueryHandler CreateHandler(HttpMessageHandler httpHandler)
    {
        var repo = new Mock<IPdfDocumentRepository>();
        repo.Setup(r => r.GetByIdAsync(PdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePdf());

        var blob = new Mock<IBlobStorageService>();
        blob.Setup(b => b.RetrieveAsync(
                It.IsAny<string>(), BlobCategory.Pdf, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([0x25, 0x50, 0x44, 0x46]));

        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("http://smoldocling-service:8002") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("SmolDoclingService")).Returns(httpClient);

        return new GetPdfPageImageQueryHandler(
            repo.Object,
            blob.Object,
            factory.Object,
            NullLogger<GetPdfPageImageQueryHandler>.Instance);
    }

    private static GetPdfPageImageQuery Query() => new(PdfId, 1);

    [Fact]
    public async Task Handle_ServiceUnreachable_Throws503NotAServerError()
    {
        // The exact shape of a missing container: a transport failure, no StatusCode.
        var handler = CreateHandler(new ThrowingHandler(
            new HttpRequestException("Resource temporarily unavailable (smoldocling-service:8002)")));

        var act = () => handler.Handle(Query(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task Handle_ServiceReturns5xx_Throws503()
    {
        var handler = CreateHandler(new StatusHandler(HttpStatusCode.InternalServerError));

        var act = () => handler.Handle(Query(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task Handle_ServiceTimesOut_Throws503()
    {
        // HttpClient surfaces its own timeout as TaskCanceledException with an uncancelled caller token.
        var handler = CreateHandler(new ThrowingHandler(new TaskCanceledException("The request timed out.")));

        var act = () => handler.Handle(Query(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task Handle_CallerCancels_PropagatesCancellation_NotA503()
    {
        // 🔴 A disconnected client must NOT be reported as "the dependency is down".
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = CreateHandler(new ThrowingHandler(new TaskCanceledException("cancelled")));

        var act = () => handler.Handle(Query(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handle_ServiceReturns404_StillThrowsNotFound()
    {
        // Pre-existing contract: a page outside the document is a 404, not an outage.
        var handler = CreateHandler(new StatusHandler(HttpStatusCode.NotFound));

        var act = () => handler.Handle(Query(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ServiceReturns400_DoesNotMasqueradeAs503()
    {
        // 🔴 An upstream 4xx means WE sent a bad request — a real bug. Mapping it to 503 would bury it.
        var handler = CreateHandler(new StatusHandler(HttpStatusCode.BadRequest));

        var act = () => handler.Handle(Query(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsImageBytes()
    {
        var handler = CreateHandler(new StatusHandler(HttpStatusCode.OK, [0xFF, 0xD8, 0xFF]));

        var bytes = await handler.Handle(Query(), CancellationToken.None);

        bytes.Should().Equal(0xFF, 0xD8, 0xFF);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _toThrow;

        public ThrowingHandler(Exception toThrow) => _toThrow = toThrow;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw _toThrow;
        }
    }

    private sealed class StatusHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly byte[] _body;

        public StatusHandler(HttpStatusCode status, byte[]? body = null)
        {
            _status = status;
            _body = body ?? [];
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status) { Content = new ByteArrayContent(_body) });
    }
}
