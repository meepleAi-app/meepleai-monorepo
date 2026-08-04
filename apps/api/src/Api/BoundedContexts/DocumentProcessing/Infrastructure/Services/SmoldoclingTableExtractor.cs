using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// HTTP client adapter for the smoldocling <c>POST /api/v1/extract-image</c> crop-discriminator
/// (#3435 SP4). Sends one PNG crop as multipart form data and maps the snake_case JSON response to
/// <see cref="CropTableExtractionResult"/>. Named client <see cref="NamedClientKey"/> (registered in
/// <c>DocumentProcessingServiceExtensions</c>). Mirrors <see cref="SmoldoclingPhotoPreprocessor"/>.
/// </summary>
internal sealed class SmoldoclingTableExtractor : ISmolDoclingTableExtractor
{
    public const string NamedClientKey = "smoldocling-table-extractor";
    private const string ExtractImageEndpoint = "/api/v1/extract-image";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SmoldoclingTableExtractor> _logger;

    public SmoldoclingTableExtractor(
        IHttpClientFactory factory,
        ILogger<SmoldoclingTableExtractor> logger)
    {
        _httpClient = factory.CreateClient(NamedClientKey);
        _logger = logger;
    }

    public async Task<CropTableExtractionResult> ExtractTableAsync(
        byte[] cropImage,
        bool? prefilter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cropImage);

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(cropImage);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", "crop.png");
        if (prefilter.HasValue)
        {
            content.Add(new StringContent(prefilter.Value ? "true" : "false"), "prefilter");
        }

        try
        {
            var response = await _httpClient
                .PostAsync(ExtractImageEndpoint, content, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var dto = await response.Content
                .ReadFromJsonAsync<ExtractImageDto>(cancellationToken: cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Empty response from smoldocling extract-image endpoint.");

            return new CropTableExtractionResult(
                IsTable: dto.IsTable,
                Reason: dto.Reason ?? string.Empty,
                Markdown: dto.Markdown ?? string.Empty,
                Confidence: dto.Confidence,
                Prefiltered: dto.Prefiltered,
                Degenerated: dto.Degenerated);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "smoldocling extract-image request failed (endpoint: {Endpoint})", ExtractImageEndpoint);
            throw;
        }
    }

    /// <summary>JSON contract for the smoldocling /api/v1/extract-image response (snake_case).</summary>
    private sealed record ExtractImageDto(
        [property: JsonPropertyName("is_table")] bool IsTable,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("markdown")] string? Markdown,
        [property: JsonPropertyName("confidence")] double Confidence,
        [property: JsonPropertyName("prefiltered")] bool Prefiltered,
        [property: JsonPropertyName("degenerated")] bool Degenerated);
}
