using System.Net;
using System.Net.Http.Json;
using MeepleAI.Functions.Contracts;
using Microsoft.Extensions.Logging;

namespace MeepleAI.Functions.Forwarding;

public sealed class PdfUploadedForwarder : IPdfUploadedForwarder
{
    public const string HttpClientName = "MeepleAiApi";
    public const string TokenHeader = "X-Internal-Service-Token";
    public const string Route = "/api/v1/internal/pdf-events/uploaded";

    private const string NotFoundOutcome = "not_found";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PdfUploadedForwarder> _logger;

    public PdfUploadedForwarder(IHttpClientFactory httpClientFactory, ILogger<PdfUploadedForwarder> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ForwardOutcome> ForwardAsync(PdfUploadedEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var payload = new { eventId = integrationEvent.EventId, pdfDocumentId = integrationEvent.PdfDocumentId, contractVersion = integrationEvent.ContractVersion };

        try
        {
            using var response = await client.PostAsJsonAsync(Route, payload, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Un 404 con esito "not_found" e' la risposta dell'handler (pdf inesistente: definitivo).
                // Un 404 senza esito e' il filtro del token: l'API non ha INTERNAL_SERVICE_TOKEN e l'intero
                // gruppo interno e' nascosto. Settled scarterebbe ogni evento in silenzio.
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (body.Contains(NotFoundOutcome, StringComparison.Ordinal))
                {
                    _logger.LogInformation("PdfUploaded {EventId} forwarded: 404 {Outcome}", integrationEvent.EventId, NotFoundOutcome);
                    return ForwardOutcome.Settled;
                }

                _logger.LogWarning(
                    "PdfUploaded {EventId} got 404 without an outcome: the API has no INTERNAL_SERVICE_TOKEN configured (internal group hidden); will retry",
                    integrationEvent.EventId);
                return ForwardOutcome.TransientFailure;
            }

            if (response.IsSuccessStatusCode
                || response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            {
                _logger.LogInformation("PdfUploaded {EventId} forwarded: {Status}", integrationEvent.EventId, (int)response.StatusCode);
                return ForwardOutcome.Settled;
            }

            _logger.LogWarning("PdfUploaded {EventId} forward got {Status}: will retry", integrationEvent.EventId, (int)response.StatusCode);
            return ForwardOutcome.TransientFailure;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "PdfUploaded {EventId} forward failed: will retry", integrationEvent.EventId);
            return ForwardOutcome.TransientFailure;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "PdfUploaded {EventId} forward timed out: will retry", integrationEvent.EventId);
            return ForwardOutcome.TransientFailure;
        }
    }
}
