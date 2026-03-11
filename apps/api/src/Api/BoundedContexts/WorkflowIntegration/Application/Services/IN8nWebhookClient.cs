using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Services;

/// <summary>
/// Client for sending webhook triggers to n8n workflows.
/// Issue #58: API → n8n trigger for calendar events.
/// </summary>
public interface IN8nWebhookClient
{
    /// <summary>
    /// Trigger an n8n workflow via webhook. Fire-and-forget with circuit breaker.
    /// </summary>
    Task TriggerWorkflowAsync(string webhookPath, object payload, CancellationToken ct = default);
}

public sealed class N8nWebhookClientOptions
{
    public const string SectionName = "N8n";

    /// <summary>Base URL of the n8n instance (e.g., http://localhost:5678).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5678";

    /// <summary>Shared secret for HMAC-SHA256 signature on outgoing webhooks.</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Enable/disable n8n integration.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Circuit breaker: max consecutive failures before cooldown.</summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>Circuit breaker: cooldown duration in seconds.</summary>
    public int CircuitBreakerCooldownSeconds { get; set; } = 60;
}

internal sealed class N8nWebhookClient : IN8nWebhookClient
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly N8nWebhookClientOptions _options;
    private readonly ILogger<N8nWebhookClient> _logger;

    // Simple circuit breaker state (thread-safe via Interlocked)
    private int _consecutiveFailures;
    private long _circuitOpenUntilTicks = DateTime.MinValue.Ticks;

    public N8nWebhookClient(
        IHttpClientFactory httpClientFactory,
        IOptions<N8nWebhookClientOptions> options,
        ILogger<N8nWebhookClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("N8nWebhook");
        _options = options.Value;
        _logger = logger;
    }

    public async Task TriggerWorkflowAsync(string webhookPath, object payload, CancellationToken ct = default)
    {
        if (!_options.IsEnabled)
        {
            _logger.LogDebug("n8n integration disabled, skipping webhook trigger for {Path}", webhookPath);
            return;
        }

        // Circuit breaker check
        var circuitOpenUntil = new DateTime(Interlocked.Read(ref _circuitOpenUntilTicks), DateTimeKind.Utc);
        if (_consecutiveFailures >= _options.CircuitBreakerThreshold && DateTime.UtcNow < circuitOpenUntil)
        {
            _logger.LogWarning("n8n circuit breaker open until {Until}, skipping webhook {Path}", circuitOpenUntil, webhookPath);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload, CamelCaseOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add HMAC signature
            if (!string.IsNullOrEmpty(_options.WebhookSecret))
            {
                var signature = ComputeHmacSha256(json, _options.WebhookSecret);
                content.Headers.Add("X-Webhook-Signature", signature);
            }

            var url = $"{_options.BaseUrl.TrimEnd('/')}/webhook/{webhookPath.TrimStart('/')}";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10s timeout

            var response = await _httpClient.PostAsync(url, content, cts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                Interlocked.Exchange(ref _consecutiveFailures, 0);
                _logger.LogInformation("n8n webhook triggered: {Path}", webhookPath);
            }
            else
            {
                _logger.LogWarning("n8n webhook {Path} returned {StatusCode}", webhookPath, response.StatusCode);
                IncrementFailures();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "n8n webhook {Path} failed (fire-and-forget)", webhookPath);
            IncrementFailures();
        }
    }

    private void IncrementFailures()
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        if (failures >= _options.CircuitBreakerThreshold)
        {
            var newOpenUntil = DateTime.UtcNow.AddSeconds(_options.CircuitBreakerCooldownSeconds);
            Interlocked.Exchange(ref _circuitOpenUntilTicks, newOpenUntil.Ticks);
            _logger.LogWarning("n8n circuit breaker opened for {Seconds}s after {Failures} failures",
                _options.CircuitBreakerCooldownSeconds, failures);
        }
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }
}
