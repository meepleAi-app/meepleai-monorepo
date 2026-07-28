using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Health probe for the Unstructured extractor (Stage 1 of the 3-stage PDF pipeline).
/// Exercises the real <c>/api/v1/extract</c> call path with a tiny embedded known PDF and
/// asserts the response carries at least one structured <c>elements[]</c> entry — the Python
/// service's own <c>/health</c> route is liveness-only and does not exercise extraction, so it
/// cannot detect a stale/misconfigured deployment that silently degrades to flat, headingless
/// chunking (#3269). Any failure (network error, non-success status, malformed JSON, or an
/// empty/missing elements array) degrades to "unhealthy" rather than throwing, so callers such
/// as the bulk re-index orchestrator can safely gate on the result.
/// </summary>
internal sealed class UnstructuredExtractorHealthProbe : IPdfExtractorHealthProbe
{
    /// <summary>
    /// Minimal single-page PDF used as the probe payload. No xref table is included — PDF
    /// parsers used by the Unstructured service tolerate this via object-scanning recovery,
    /// and a full byte-accurate xref adds no value for a throwaway health-check payload.
    /// </summary>
    private static readonly byte[] ProbePdfBytes = Encoding.ASCII.GetBytes(
        "%PDF-1.4\n" +
        "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
        "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
        "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R " +
        "/Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n" +
        "4 0 obj\n<< /Length 58 >>\nstream\nBT /F1 24 Tf 10 100 Td (Health Check) Tj ET\nendstream\nendobj\n" +
        "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n" +
        "trailer\n<< /Root 1 0 R >>\n%%EOF");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UnstructuredExtractorHealthProbe> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public UnstructuredExtractorHealthProbe(
        IHttpClientFactory httpClientFactory,
        ILogger<UnstructuredExtractorHealthProbe> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("UnstructuredService");
            using var content = PrepareMultipartContent();
            using var response = await client.PostAsync("/api/v1/extract", content, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Unstructured extractor health probe failed: HTTP {StatusCode}",
                    response.StatusCode);
                return false;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var extractionResponse = JsonSerializer.Deserialize<UnstructuredExtractionResponse>(jsonContent, _jsonOptions);

            if (extractionResponse?.Elements is not { Count: > 0 })
            {
                _logger.LogWarning(
                    "Unstructured extractor health probe returned no structured elements — " +
                    "extractor may be stale/misconfigured and would produce flat, headingless chunks");
                return false;
            }

            return true;
        }
#pragma warning disable CA1031
        // INFRASTRUCTURE HEALTH-PROBE PATTERN: this is a best-effort refuse-to-run gate for the
        // bulk re-index orchestrator (Task C2) - any failure (network error, timeout, malformed
        // JSON, cancellation) must degrade to "unhealthy" (false), never throw, so callers can
        // safely gate a batch operation on the result without needing their own try/catch.
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unstructured extractor health probe threw an exception");
            return false;
        }
#pragma warning restore CA1031
    }

    private static MultipartFormDataContent PrepareMultipartContent()
    {
        var content = new MultipartFormDataContent();
#pragma warning disable CA2000 // Dispose objects before losing scope
        // OWNERSHIP TRANSFER: MultipartFormDataContent takes ownership of added content and disposes them when it is disposed
        var streamContent = new StreamContent(new MemoryStream(ProbePdfBytes));
        var strategyContent = new StringContent("fast");
        var languageContent = new StringContent("ita");
#pragma warning restore CA2000

        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", "health-probe.pdf");
        content.Add(strategyContent, "strategy");
        content.Add(languageContent, "language");

        return content;
    }
}
