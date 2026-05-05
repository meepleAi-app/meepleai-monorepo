using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// HTTP client adapter for the SmolDocling /api/v1/preprocess endpoint.
/// Sends raw image bytes as multipart form data and maps the JSON response to
/// <see cref="PhotoPreprocessResult"/>.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.4b.
/// </summary>
/// <remarks>
/// Named client: "smoldocling-photo-preprocessor" (registered in
/// <see cref="DependencyInjection.DocumentProcessingServiceExtensions"/>).
/// No Polly retry — simple 30-second timeout is sufficient for Sprint 1.
/// Retry can be added in Phase 2+ if required.
/// </remarks>
internal sealed class SmoldoclingPhotoPreprocessor : IPhotoPreprocessor
{
    private const string PreprocessEndpoint = "/api/v1/preprocess";
    private const string NamedClientKey = "smoldocling-photo-preprocessor";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SmoldoclingPhotoPreprocessor> _logger;

    public SmoldoclingPhotoPreprocessor(
        IHttpClientFactory factory,
        ILogger<SmoldoclingPhotoPreprocessor> logger)
    {
        _httpClient = factory.CreateClient(NamedClientKey);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PhotoPreprocessResult> PreprocessAsync(
        byte[] imageData,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);

        _logger.LogDebug(
            "Sending {Bytes} bytes to smoldocling {Endpoint}",
            imageData.Length,
            PreprocessEndpoint);

        using var content = BuildMultipartContent(imageData);

        try
        {
            var response = await _httpClient
                .PostAsync(PreprocessEndpoint, content, ct)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var dto = await response.Content
                .ReadFromJsonAsync<SmoldoclingPreprocessDto>(cancellationToken: ct)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Empty response from smoldocling preprocess endpoint.");

            return new PhotoPreprocessResult(
                ProcessedImage: Convert.FromBase64String(dto.ProcessedImageBase64),
                ExtractedText: dto.ExtractedText,
                ConfidenceScore: dto.Confidence,
                DetectedOrientation: ParseOrientation(dto.Orientation),
                IsBlankPage: dto.IsBlank,
                Warnings: dto.Warnings ?? Array.Empty<string>()
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Smoldocling preprocess request failed (endpoint: {Endpoint})",
                PreprocessEndpoint);
            throw;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static MultipartFormDataContent BuildMultipartContent(byte[] imageData)
    {
        var formData = new MultipartFormDataContent();

        var imageContent = new ByteArrayContent(imageData);
        imageContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        formData.Add(imageContent, "image", "page.jpg");
        formData.Add(new StringContent("photo-camera"), "preprocessing_mode");

        return formData;
    }

    private static PageOrientation ParseOrientation(string? orientation) =>
        orientation?.ToLowerInvariant() switch
        {
            "portrait" => PageOrientation.Portrait,
            "landscape" => PageOrientation.Landscape,
            "rotated" => PageOrientation.Rotated,
            _ => PageOrientation.Unknown,
        };

    // ── DTO (internal — only used by this class) ──────────────────────────────

    /// <summary>
    /// JSON contract for the smoldocling /api/v1/preprocess response.
    /// Uses explicit <see cref="JsonPropertyNameAttribute"/> attributes because
    /// the Python service returns snake_case and no global naming policy is configured.
    /// </summary>
    private sealed record SmoldoclingPreprocessDto(
        [property: JsonPropertyName("processed_image_base64")]
        string ProcessedImageBase64,

        [property: JsonPropertyName("extracted_text")]
        string ExtractedText,

        [property: JsonPropertyName("confidence")]
        double Confidence,

        [property: JsonPropertyName("orientation")]
        string Orientation,

        [property: JsonPropertyName("is_blank")]
        bool IsBlank,

        [property: JsonPropertyName("warnings")]
        string[]? Warnings);
}
